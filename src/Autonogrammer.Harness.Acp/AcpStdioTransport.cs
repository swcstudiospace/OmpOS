using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Autonogrammer.Harness.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Autonogrammer.Harness.Acp;

/// <summary>
/// Runs <c>omp acp</c> as a child process and exchanges one JSON object per line over its stdin and stdout.
/// Standard error is drained continuously into a bounded buffer so the child never blocks on a full stderr pipe.
/// The process is started without a shell.
/// </summary>
public sealed class AcpStdioTransport : IAcpTransport
{
    /// <summary>The number of stderr lines retained. Older lines are dropped first.</summary>
    public const int StderrCapacity = 256;

    private static readonly TimeSpan ExitGracePeriod = TimeSpan.FromSeconds(2);
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);
    private static readonly byte[] NewLine = [(byte)'\n'];
    private static readonly Action<ILogger, Exception?> StderrDrainEnded =
        LoggerMessage.Define(
            LogLevel.Debug,
            new EventId(1, "AcpStderrDrainEnded"),
            "The omp acp stderr drain ended on a pipe failure.");

    private readonly string _ompPath;
    private readonly string? _workingDirectory;
    private readonly string[] _arguments;
    private readonly KeyValuePair<string, string>[] _environment;
    private readonly ILogger<AcpStdioTransport> _logger;
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private readonly System.Threading.Lock _gate = new();
    private readonly Channel<string> _stderr = Channel.CreateBounded<string>(new BoundedChannelOptions(StderrCapacity)
    {
        FullMode = BoundedChannelFullMode.DropOldest,
        SingleWriter = true,
    });

    private Process? _process;
    private Task? _stderrDrain;
    private bool _connected;
    private bool _closed;

    /// <summary>Initializes a new instance of the <see cref="AcpStdioTransport"/> class.</summary>
    /// <param name="ompPath">The omp executable. A bare name is resolved from <c>PATH</c>.</param>
    /// <param name="workingDirectory">The child's working directory, or <see langword="null"/> to inherit the current one.</param>
    /// <param name="logger">Receives stderr drain failures at Debug. Null discards them.</param>
    public AcpStdioTransport(string ompPath, string? workingDirectory = null, ILogger<AcpStdioTransport>? logger = null)
        : this(ompPath, workingDirectory, [], new Dictionary<string, string>(StringComparer.Ordinal), logger)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="AcpStdioTransport"/> class that passes extra arguments and environment variables to the child.</summary>
    /// <param name="ompPath">The omp executable. A bare name is resolved from <c>PATH</c>.</param>
    /// <param name="workingDirectory">The child's working directory, or <see langword="null"/> to inherit the current one.</param>
    /// <param name="arguments">The arguments appended after <c>acp</c>, one argv element each and in order.</param>
    /// <param name="environment">The environment variables set on the child on top of the inherited environment. Values may be secrets and are never logged.</param>
    /// <param name="logger">Receives stderr drain failures at Debug. Null discards them.</param>
    /// <exception cref="ArgumentException">An argument is null or an environment variable name is blank or contains <c>=</c> or NUL.</exception>
    public AcpStdioTransport(
        string ompPath,
        string? workingDirectory,
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string> environment,
        ILogger<AcpStdioTransport>? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ompPath);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(environment);
        foreach (var argument in arguments)
        {
            ArgumentNullException.ThrowIfNull(argument, nameof(arguments));
        }

        foreach (var (name, value) in environment)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Contains('=', StringComparison.Ordinal) || name.Contains('\0', StringComparison.Ordinal))
            {
                throw new ArgumentException("An environment variable name is blank or contains '=' or NUL.", nameof(environment));
            }

            ArgumentNullException.ThrowIfNull(value, nameof(environment));
        }

        _ompPath = ompPath;
        _logger = logger ?? NullLogger<AcpStdioTransport>.Instance;
        _workingDirectory = workingDirectory;
        _arguments = arguments.ToArray();
        _environment = environment.ToArray();
    }

    /// <summary>Gets the most recent stderr lines written by the child process.</summary>
    public ChannelReader<string> StandardError => _stderr.Reader;

    /// <inheritdoc />
    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (_closed)
            {
                throw new DisconnectedHarnessException("The ACP transport is closed.", _ompPath);
            }

            if (_connected)
            {
                throw new InvalidOperationException("The ACP transport is already connected.");
            }

            var process = StartProcess();
            _process = process;
            _stderrDrain = DrainStderrAsync(process);
            _connected = true;
        }
    }

    /// <inheritdoc />
    public async Task SendAsync(ReadOnlyMemory<byte> frame, CancellationToken cancellationToken)
    {
        var stdin = WritableStdin();
        try
        {
            await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                // Once a frame starts writing it must finish, or the stream framing breaks; only the gate wait honours cancellation.
                await stdin.WriteAsync(frame, CancellationToken.None).ConfigureAwait(false);
                await stdin.WriteAsync(NewLine, CancellationToken.None).ConfigureAwait(false);
                await stdin.FlushAsync(CancellationToken.None).ConfigureAwait(false);
            }
            finally
            {
                _writeGate.Release();
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            throw new DisconnectedHarnessException("Failed to write to omp acp.", _ompPath, ex);
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ReadOnlyMemory<byte>> ReceiveAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var process = ConnectedProcess();
        while (true)
        {
            var line = await ReadLineAsync(process, cancellationToken).ConfigureAwait(false);
            if (line is null)
            {
                break;
            }

            if (line.Length == 0)
            {
                continue;
            }

            var frame = Utf8.GetBytes(line);
            ValidateJsonObject(frame);
            yield return frame;
        }

        throw await ExitedAsync(process).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task CloseAsync(CancellationToken cancellationToken)
    {
        Process? process;
        Task? drain;
        lock (_gate)
        {
            if (_closed)
            {
                return;
            }

            _closed = true;
            process = _process;
            drain = _stderrDrain;
        }

        if (process is null)
        {
            return;
        }

        CloseStdin(process);
        await StopAsync(process, cancellationToken).ConfigureAwait(false);
        if (drain is not null)
        {
            // The drain ends at the child's stderr EOF, which follows the kill. A grandchild holding stderr open must not hang close.
            await WaitForDrainAsync(drain).ConfigureAwait(false);
        }

        process.Dispose();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await CloseAsync(CancellationToken.None).ConfigureAwait(false);
        _writeGate.Dispose();
    }

    private Process StartProcess()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _ompPath,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardInputEncoding = Utf8,
            StandardOutputEncoding = Utf8,
            StandardErrorEncoding = Utf8,
        };
        startInfo.ArgumentList.Add("acp");
        foreach (var argument in _arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        // The child inherits the parent environment; an explicit entry overrides the inherited variable of the same name.
        foreach (var (name, value) in _environment)
        {
            startInfo.Environment[name] = value;
        }

        if (_workingDirectory is not null)
        {
            startInfo.WorkingDirectory = _workingDirectory;
        }

        try
        {
            return Process.Start(startInfo) ?? throw new ProcessStartHarnessException("The omp acp process did not start.", _ompPath);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or UnauthorizedAccessException)
        {
            throw new ProcessStartHarnessException($"Could not start '{_ompPath} acp'.", _ompPath, ex);
        }
    }

    private async Task DrainStderrAsync(Process process)
    {
        try
        {
            while (await process.StandardError.ReadLineAsync().ConfigureAwait(false) is { } line)
            {
                _stderr.Writer.TryWrite(line);
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            // Diagnostics only: a broken stderr pipe ends the drain without affecting the protocol stream.
            StderrDrainEnded(_logger, ex);
        }
        finally
        {
            _stderr.Writer.TryComplete();
        }
    }

    private Stream WritableStdin()
    {
        lock (_gate)
        {
            if (_closed || !_connected || _process is null)
            {
                throw new DisconnectedHarnessException("The ACP transport is not open.", _ompPath);
            }

            return _process.StandardInput.BaseStream;
        }
    }

    private Process ConnectedProcess()
    {
        lock (_gate)
        {
            if (_closed || !_connected || _process is null)
            {
                throw new DisconnectedHarnessException("The ACP transport is not open.", _ompPath);
            }

            return _process;
        }
    }

    private static async Task<string?> ReadLineAsync(Process process, CancellationToken cancellationToken)
    {
        try
        {
            return await BoundedLineReader.ReadLineAsync(process.StandardOutput, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            // A killed or disposed child surfaces as end of stream; ExitedAsync reports it.
            _ = ex;
            return null;
        }
    }

    private async Task<DisconnectedHarnessException> ExitedAsync(Process process)
    {
        using var grace = new CancellationTokenSource(ExitGracePeriod);
        try
        {
            await process.WaitForExitAsync(grace.Token).ConfigureAwait(false);
            var code = process.ExitCode.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return new DisconnectedHarnessException($"omp acp exited with code {code}.", _ompPath);
        }
        catch (OperationCanceledException)
        {
            return new DisconnectedHarnessException("omp acp closed its output.", _ompPath);
        }
    }

    private static void ValidateJsonObject(byte[] frame)
    {
        var reader = new Utf8JsonReader(frame, isFinalBlock: true, state: default);
        try
        {
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
            {
                throw AcpFrameDiagnostics.Malformed(frame, "not a JSON object");
            }

            while (reader.Read())
            {
            }
        }
        catch (JsonException ex)
        {
            throw AcpFrameDiagnostics.Malformed(frame, "invalid JSON", ex);
        }
    }

    private static async Task WaitForDrainAsync(Task drain)
    {
        try
        {
            await drain.WaitAsync(ExitGracePeriod, CancellationToken.None).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            // Diagnostics only: stop waiting for stderr rather than hang close.
        }
    }

    private static void CloseStdin(Process process)
    {
        try
        {
            process.StandardInput.Close();
        }
        catch (IOException)
        {
            // The child already exited; the pipe is gone.
        }
    }

    private static async Task StopAsync(Process process, CancellationToken cancellationToken)
    {
        using var grace = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        grace.CancelAfter(ExitGracePeriod);
        try
        {
            await process.WaitForExitAsync(grace.Token).ConfigureAwait(false);
            return;
        }
        catch (OperationCanceledException)
        {
            // The child ignored stdin EOF within the grace period; it is killed below.
        }

        KillTree(process);
        await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
    }

    private static void KillTree(Process process)
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
}
