using OmpOS.Harness.Abstractions;
using OmpOS.Harness.Acp;
using OmpOS.AgentWorker.Security;

namespace OmpOS.AgentWorker.Bridge;

/// <summary>
/// One session's claim on the worker: its working directory, its <c>omp acp</c> child and the pump that copies the child's stderr into
/// the log with secrets removed. Disposing the attachment closes stdin, kills the whole process tree when the child does not exit, and
/// frees the session subject.
/// </summary>
internal sealed class OmpAttachment : IAsyncDisposable
{
    private const int MaxLoggedLineLength = 2048;

    private static readonly Action<ILogger, string, string, Exception?> StderrLine =
        LoggerMessage.Define<string, string>(
            LogLevel.Information,
            new EventId(10, "OmpStderrLine"),
            "omp[{SessionId}] stderr: {Line}");

    private readonly OmpProcessHost _host;
    private readonly string _workingDirectory;
    private readonly AcpStdioTransport _transport;
    private readonly IDisposable _tokenTracking;
    private readonly WorkerSecretRedactor _redactor;
    private readonly ILogger<OmpAttachment> _logger;

    private Task _stderrDrain = Task.CompletedTask;
    private int _disposed;

    /// <summary>Initializes a new instance of the <see cref="OmpAttachment"/> class.</summary>
    /// <param name="host">The host that owns the claim.</param>
    /// <param name="sessionId">The claimed session subject.</param>
    /// <param name="workingDirectory">The session directory the child runs in.</param>
    /// <param name="transport">The not yet connected omp transport.</param>
    /// <param name="tokenTracking">The handle that keeps the connection's token redacted.</param>
    /// <param name="redactor">Scrubs the child's stderr lines.</param>
    /// <param name="logger">Receives the stderr lines.</param>
    internal OmpAttachment(
        OmpProcessHost host,
        string sessionId,
        string workingDirectory,
        AcpStdioTransport transport,
        IDisposable tokenTracking,
        WorkerSecretRedactor redactor,
        ILogger<OmpAttachment> logger)
    {
        _host = host;
        SessionId = sessionId;
        _workingDirectory = workingDirectory;
        _transport = transport;
        _tokenTracking = tokenTracking;
        _redactor = redactor;
        _logger = logger;
    }

    /// <summary>Gets the claimed session subject.</summary>
    public string SessionId { get; }

    /// <summary>Gets the transport to the omp child: one JSON-RPC message per frame.</summary>
    public IAcpTransport Transport => _transport;

    /// <summary>Creates the session directory and starts the omp child.</summary>
    /// <param name="cancellationToken">Cancels the start.</param>
    /// <exception cref="ProcessStartHarnessException">The directory or the process could not be created.</exception>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            Directory.CreateDirectory(_workingDirectory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new ProcessStartHarnessException("Could not create the session directory.", SessionId, ex);
        }

        await _transport.ConnectAsync(cancellationToken).ConfigureAwait(false);
        _stderrDrain = DrainStderrAsync();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        try
        {
            await _transport.DisposeAsync().ConfigureAwait(false);
            await _stderrDrain.ConfigureAwait(false);
        }
        finally
        {
            _tokenTracking.Dispose();
            _host.Release(SessionId);
        }
    }

    private async Task DrainStderrAsync()
    {
        await foreach (var line in _transport.StandardError.ReadAllAsync().ConfigureAwait(false))
        {
            var scrubbed = _redactor.Redact(line);
            if (scrubbed.Length > MaxLoggedLineLength)
            {
                scrubbed = scrubbed[..MaxLoggedLineLength];
            }

            StderrLine(_logger, SessionId, scrubbed, null);
        }
    }
}
