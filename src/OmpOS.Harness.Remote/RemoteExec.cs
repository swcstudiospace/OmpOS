using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using OmpOS.Contracts.Environments;

namespace OmpOS.Harness.Remote;

/// <summary>
/// Runs short POSIX shell scripts on one remote machine through the user's own <c>ssh</c>, without a terminal and without a prompt: the same
/// hardening as <see cref="SshCommandBuilder"/> (no shell on this machine, <c>BatchMode</c>, one quoted script line, <c>--</c> before the
/// destination, no secret on a command line). It is the transport of the remote file system and of the remote convention discovery. At most
/// four ssh processes run at once; the others wait their turn.
/// </summary>
public sealed class RemoteExec : IDisposable
{
    private const int MaxConcurrentCalls = 4;
    private const int ChunkBytes = 16 * 1024;
    private const int MaxErrorCharacters = 64 * 1024;

    private static readonly TimeSpan DrainGrace = TimeSpan.FromSeconds(5);
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    private readonly SshProfile _profile;
    private readonly SshClientOptions _options;
    private readonly SshCommandBuilder _builder;
    private readonly TimeProvider _clock;
    private readonly SemaphoreSlim _slots = new(MaxConcurrentCalls, MaxConcurrentCalls);

    /// <summary>Initializes a new instance of the <see cref="RemoteExec"/> class.</summary>
    /// <param name="profile">The remote machine. It must pass <see cref="EnvironmentProfileValidator"/>.</param>
    /// <param name="options">How ssh is run, or <see langword="null"/> for the defaults.</param>
    /// <param name="clock">The clock the time limit uses, or <see langword="null"/> for the system clock.</param>
    /// <exception cref="ArgumentNullException"><paramref name="profile"/> is null.</exception>
    /// <exception cref="ArgumentException">The profile or the client options are not valid.</exception>
    public RemoteExec(SshProfile profile, SshClientOptions? options = null, TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var issues = EnvironmentProfileValidator.Validate(profile);
        if (issues.Count > 0)
        {
            throw new ArgumentException("The ssh profile is not valid.", nameof(profile));
        }

        _profile = profile;
        _options = options ?? new SshClientOptions();
        _builder = new SshCommandBuilder(_options);
        _clock = clock ?? TimeProvider.System;
    }

    /// <summary>Gets the remote machine the scripts run on.</summary>
    public SshProfile Profile => _profile;

    /// <summary>Runs one script on the remote machine and waits for it.</summary>
    /// <param name="script">One line of POSIX shell. It is passed to <c>sh -c</c> on the remote machine; every dynamic value in it must be quoted with <see cref="PosixQuoting"/>.</param>
    /// <param name="input">The bytes written to the script's stdin, then stdin is closed. Empty for none.</param>
    /// <param name="maxOutputBytes">The most stdout bytes kept; the rest is drained and dropped, and the result says so.</param>
    /// <param name="cancellationToken">Cancels the run and stops ssh.</param>
    /// <returns>What the script did. A failing machine is a result, not an exception.</returns>
    /// <exception cref="ArgumentException">The script is empty or spans several lines, or the output cap is negative.</exception>
    public async Task<RemoteExecResult> RunAsync(string script, ReadOnlyMemory<byte> input, int maxOutputBytes, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(script);
        ArgumentOutOfRangeException.ThrowIfNegative(maxOutputBytes);

        var invocation = _builder.BuildProbe(_profile, script);
        await _slots.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await RunProcessAsync(invocation, input, maxOutputBytes, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _ = _slots.Release();
        }
    }

    /// <summary>Resolves the home directory of the remote user (<c>printf %s "$HOME"</c>).</summary>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>The absolute home directory, or <see langword="null"/> when it could not be read or is not an absolute path.</returns>
    public async Task<string?> ResolveHomeDirectoryAsync(CancellationToken cancellationToken)
    {
        var result = await RunAsync("printf %s \"$HOME\"", ReadOnlyMemory<byte>.Empty, 4096, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            return null;
        }

        var home = result.OutputText().Trim();
        return SshProfileRules.IsValidAbsoluteRemotePath(home) ? home : null;
    }

    /// <summary>Releases the lock that limits the concurrent ssh processes.</summary>
    public void Dispose() => _slots.Dispose();

    private static Process? TryStart(ProcessStartInfo startInfo, out string? failure)
    {
        failure = null;
        try
        {
            var started = Process.Start(startInfo);
            if (started is null)
            {
                failure = "The process did not start.";
            }

            return started;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or UnauthorizedAccessException)
        {
            failure = ex.Message;
            return null;
        }
    }

    private static async Task WriteInputAsync(Stream stdin, ReadOnlyMemory<byte> input)
    {
        try
        {
            if (!input.IsEmpty)
            {
                await stdin.WriteAsync(input, CancellationToken.None).ConfigureAwait(false);
                await stdin.FlushAsync(CancellationToken.None).ConfigureAwait(false);
            }

            stdin.Close();
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            // The child exited before it read its input; its exit code and stderr explain why.
            _ = ex;
        }
    }

    private static async Task<(byte[] Data, bool Truncated)> ReadCappedAsync(Stream stream, int cap)
    {
        using var kept = new MemoryStream();
        var truncated = false;
        var buffer = new byte[ChunkBytes];
        try
        {
            int read;
            while ((read = await stream.ReadAsync(buffer.AsMemory(), CancellationToken.None).ConfigureAwait(false)) > 0)
            {
                // Keep draining past the cap so the child never blocks on a full pipe.
                var room = cap - (int)kept.Length;
                if (room > 0)
                {
                    await kept.WriteAsync(buffer.AsMemory(0, Math.Min(read, room)), CancellationToken.None).ConfigureAwait(false);
                }

                truncated |= read > room;
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            // A killed child ends the pipe abruptly; what was read is kept.
            _ = ex;
        }

        return (kept.ToArray(), truncated);
    }

    private static async Task<string> ReadErrorAsync(StreamReader reader)
    {
        var builder = new StringBuilder();
        var buffer = new char[4096];
        try
        {
            int read;
            while ((read = await reader.ReadAsync(buffer.AsMemory(), CancellationToken.None).ConfigureAwait(false)) > 0)
            {
                var room = MaxErrorCharacters - builder.Length;
                if (room > 0)
                {
                    _ = builder.Append(buffer, 0, Math.Min(read, room));
                }
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            _ = ex;
        }

        return builder.ToString();
    }

    private static void Kill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // Exited between the check and the kill.
        }
    }

    private static ProcessStartInfo StartInfoFor(SshInvocation invocation)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = invocation.FileName,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardErrorEncoding = Utf8,
        };
        foreach (var argument in invocation.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }

    private async Task<RemoteExecResult> RunProcessAsync(SshInvocation invocation, ReadOnlyMemory<byte> input, int maxOutputBytes, CancellationToken cancellationToken)
    {
        using var process = TryStart(StartInfoFor(invocation), out var startFailure);
        if (process is null)
        {
            return new RemoteExecResult(-1, ReadOnlyMemory<byte>.Empty, string.Empty, OutputTruncated: false, TimedOut: false, startFailure);
        }

        var writing = WriteInputAsync(process.StandardInput.BaseStream, input);
        var reading = ReadCappedAsync(process.StandardOutput.BaseStream, maxOutputBytes);
        var errors = ReadErrorAsync(process.StandardError);

        var timedOut = await WaitAsync(process, cancellationToken).ConfigureAwait(false);
        try
        {
            await Task.WhenAll(writing, reading, errors).WaitAsync(DrainGrace, _clock, CancellationToken.None).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            // A grandchild (a connection-sharing master) can keep the pipes open after the client exited; what was read is enough.
        }

        var output = reading.IsCompletedSuccessfully ? await reading.ConfigureAwait(false) : (Data: Array.Empty<byte>(), Truncated: false);
        var error = errors.IsCompletedSuccessfully ? await errors.ConfigureAwait(false) : string.Empty;
        return new RemoteExecResult(timedOut ? -1 : process.ExitCode, output.Data, error, output.Truncated, timedOut, StartFailure: null);
    }

    // Waits for the process; returns true when it was killed for exceeding the time limit. A cancelled run kills the process and throws.
    private async Task<bool> WaitAsync(Process process, CancellationToken cancellationToken)
    {
        using var limit = new CancellationTokenSource(_options.CommandTimeout, _clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, limit.Token);
        try
        {
            await process.WaitForExitAsync(linked.Token).ConfigureAwait(false);
            return false;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            Kill(process);
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException)
        {
            Kill(process);
            throw;
        }
    }
}
