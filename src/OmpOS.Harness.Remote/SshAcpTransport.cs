using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OmpOS.Contracts.Environments;
using OmpOS.Harness.Abstractions;

namespace OmpOS.Harness.Remote;

/// <summary>
/// Carries the ACP stdio protocol of <c>omp acp</c> running on a remote machine through an <c>ssh</c> child process: one JSON object
/// per line in each direction. It differs from the local stdio transport in three ways that ssh forces on it. A login shell can print a
/// banner or rc-file output on stdout before the first frame, so lines before the first JSON object are kept as noise instead of
/// faulting; after the first frame the stream is strict and a line that is not a JSON object faults. The stdin preamble (environment and
/// files) is written before the first frame. A failed ssh run is explained by a <see cref="RemoteDiagnosis"/> built from its exit
/// code and stderr. The process is started without a shell from an argument list and no secret is ever in it.
/// </summary>
public sealed class SshAcpTransport : IAcpTransport
{
    /// <summary>The number of stderr lines retained. Older lines are dropped first.</summary>
    public const int StderrCapacity = 256;

    /// <summary>The number of stdout lines before the first frame that are tolerated before the transport gives up.</summary>
    public const int MaxNoiseLines = 200;

    /// <summary>The number of characters of stdout before the first frame that are tolerated before the transport gives up.</summary>
    public const int MaxNoiseCharacters = 64 * 1024;

    private const int NoisePreviewCharacters = 512;
    private const int FramePreviewBytes = 2048;
    private const int MinimumRedactionLength = 4;
    private const string Redacted = "[redacted]";

    private static readonly TimeSpan ExitGracePeriod = TimeSpan.FromSeconds(2);
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);
    private static readonly byte[] NewLine = [(byte)'\n'];
    private static readonly Action<ILogger, Exception?> StderrDrainEnded =
        LoggerMessage.Define(
            LogLevel.Debug,
            new EventId(1, "SshStderrDrainEnded"),
            "The ssh stderr drain ended on a pipe failure.");

    private readonly SshInvocation _invocation;
    private readonly string[] _redactions;
    private readonly ILogger<SshAcpTransport> _logger;
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private readonly System.Threading.Lock _gate = new();
    private readonly Queue<string> _stderr = new(StderrCapacity);
    private readonly List<string> _noise = [];

    private Process? _process;
    private Task? _stderrDrain;
    private RemoteDiagnosis? _diagnosis;
    private bool _connected;
    private bool _closed;

    /// <summary>Initializes a new instance of the <see cref="SshAcpTransport"/> class.</summary>
    /// <param name="invocation">The ssh process to start, from <see cref="SshCommandBuilder.BuildAcp"/>.</param>
    /// <param name="redactions">Secret values that are removed from every diagnostic text. Values shorter than four characters are ignored.</param>
    /// <param name="logger">Receives stderr drain failures at Debug. Null discards them.</param>
    public SshAcpTransport(SshInvocation invocation, IEnumerable<string>? redactions = null, ILogger<SshAcpTransport>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        _invocation = invocation;
        _redactions = redactions is null
            ? []
            : [.. redactions.Where(value => value is not null && value.Length >= MinimumRedactionLength).OrderByDescending(value => value.Length)];
        _logger = logger ?? NullLogger<SshAcpTransport>.Instance;
    }

    /// <summary>Gets the most recent stderr lines of the ssh process (at most <see cref="StderrCapacity"/>), with secrets removed.</summary>
    public IReadOnlyList<string> StandardErrorTail
    {
        get
        {
            lock (_gate)
            {
                return [.. _stderr];
            }
        }
    }

    /// <summary>Gets the lines the remote shell printed on stdout before the first protocol frame (a banner, rc-file output), with secrets removed.</summary>
    public IReadOnlyList<string> PreHandshakeNoise
    {
        get
        {
            lock (_gate)
            {
                return [.. _noise];
            }
        }
    }

    /// <summary>Gets the explanation of the failure that ended the connection, or <see langword="null"/> while it is up or when the cause is not recognised.</summary>
    public RemoteDiagnosis? LastDiagnosis
    {
        get
        {
            lock (_gate)
            {
                return _diagnosis;
            }
        }
    }

    /// <inheritdoc />
    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        Process process;
        lock (_gate)
        {
            if (_closed)
            {
                throw new DisconnectedHarnessException("The ssh transport is closed.", "ssh");
            }

            if (_connected)
            {
                throw new InvalidOperationException("The ssh transport is already connected.");
            }

            process = StartProcess();
            _process = process;
            _stderrDrain = DrainStderrAsync(process);
            _connected = true;
        }

        await WritePreambleAsync(process).ConfigureAwait(false);
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
            throw new DisconnectedHarnessException("Failed to write to the ssh connection.", "ssh", ex);
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ReadOnlyMemory<byte>> ReceiveAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var process = ConnectedProcess();
        var sawFrame = false;
        var noiseCharacters = 0;
        var noiseLines = 0;
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
            if (!IsJsonObject(frame))
            {
                if (sawFrame)
                {
                    throw Malformed(frame, "not a JSON object");
                }

                noiseCharacters += line.Length;
                noiseLines++;
                RecordNoise(line);
                if (noiseLines > MaxNoiseLines || noiseCharacters > MaxNoiseCharacters)
                {
                    throw new MalformedFrameHarnessException(
                        "The remote shell printed text on stdout before omp answered, and no protocol frame followed. Check the remote ~/.bashrc and ~/.profile for output in non-interactive sessions. First lines: "
                        + string.Join(" | ", PreHandshakeNoise.Take(3)),
                        "ssh");
                }

                continue;
            }

            sawFrame = true;
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

    private static bool IsJsonObject(byte[] frame)
    {
        var reader = new Utf8JsonReader(frame, isFinalBlock: true, state: default);
        try
        {
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
            {
                return false;
            }

            while (reader.Read())
            {
            }

            return true;
        }
        catch (JsonException)
        {
            return false;
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

    private Process StartProcess()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _invocation.FileName,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardInputEncoding = Utf8,
            StandardOutputEncoding = Utf8,
            StandardErrorEncoding = Utf8,
        };
        foreach (var argument in _invocation.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        try
        {
            return Process.Start(startInfo) ?? throw MissingClient(null);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or UnauthorizedAccessException)
        {
            throw MissingClient(ex);
        }
    }

    private ProcessStartHarnessException MissingClient(Exception? inner)
    {
        var diagnosis = RemoteDiagnosisCatalog.Create(RemoteDiagnosisKind.SshMissing);
        _diagnosis = diagnosis;
        return new ProcessStartHarnessException(diagnosis.Summary + " " + diagnosis.FixText, "ssh", inner);
    }

    private async Task WritePreambleAsync(Process process)
    {
        if (_invocation.Preamble is not { } preamble)
        {
            return;
        }

        await _writeGate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            var stdin = process.StandardInput.BaseStream;
            await stdin.WriteAsync(preamble.Bytes, CancellationToken.None).ConfigureAwait(false);
            await stdin.FlushAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            // ssh exited before it read the preamble; the exit code and stderr explain why and surface from ReceiveAsync.
            _ = ex;
        }
        finally
        {
            _writeGate.Release();
        }
    }

    private async Task DrainStderrAsync(Process process)
    {
        try
        {
            while (await process.StandardError.ReadLineAsync().ConfigureAwait(false) is { } line)
            {
                lock (_gate)
                {
                    if (_stderr.Count == StderrCapacity)
                    {
                        _ = _stderr.Dequeue();
                    }

                    _stderr.Enqueue(Redact(line));
                }
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            // Diagnostics only: a broken stderr pipe ends the drain without affecting the protocol stream.
            StderrDrainEnded(_logger, ex);
        }
    }

    private Stream WritableStdin()
    {
        lock (_gate)
        {
            if (_closed || !_connected || _process is null)
            {
                throw new DisconnectedHarnessException("The ssh transport is not open.", "ssh");
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
                throw new DisconnectedHarnessException("The ssh transport is not open.", "ssh");
            }

            return _process;
        }
    }

    private void RecordNoise(string line)
    {
        var preview = Redact(line.Length > NoisePreviewCharacters ? line[..NoisePreviewCharacters] : line);
        lock (_gate)
        {
            if (_noise.Count < MaxNoiseLines)
            {
                _noise.Add(preview);
            }
        }
    }

    private string Redact(string text)
    {
        var result = text;
        foreach (var secret in _redactions)
        {
            result = result.Replace(secret, Redacted, StringComparison.Ordinal);
        }

        return result;
    }

    private MalformedFrameHarnessException Malformed(byte[] frame, string detail)
    {
        var preview = Redact(Encoding.UTF8.GetString(frame.AsSpan(0, Math.Min(frame.Length, FramePreviewBytes))));
        return new MalformedFrameHarnessException($"Malformed frame from omp acp over ssh ({detail}): {preview}", "ssh");
    }

    private async Task<DisconnectedHarnessException> ExitedAsync(Process process)
    {
        using var grace = new CancellationTokenSource(ExitGracePeriod);
        try
        {
            await process.WaitForExitAsync(grace.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return new DisconnectedHarnessException("ssh closed its output.", "ssh");
        }

        Task? drain;
        lock (_gate)
        {
            drain = _stderrDrain;
        }

        if (drain is not null)
        {
            await WaitForDrainAsync(drain).ConfigureAwait(false);
        }

        var code = process.ExitCode;
        var tail = string.Join('\n', StandardErrorTail);
        var diagnosis = RemoteDiagnosisCatalog.FromFailure(code, tail);
        lock (_gate)
        {
            _diagnosis = diagnosis;
        }

        var message = string.Create(CultureInfo.InvariantCulture, $"ssh exited with code {code}.");
        if (diagnosis is not null)
        {
            message += " " + diagnosis.Summary;
        }
        else if (RemoteDiagnosisCatalog.Excerpt(tail) is { } excerpt)
        {
            message += " " + excerpt.Replace('\n', ' ');
        }

        return new DisconnectedHarnessException(message, "ssh");
    }
}
