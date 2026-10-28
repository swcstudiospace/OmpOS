using System.Threading.Channels;
using Autonogrammer.AgentWorker.Hooks;
using Autonogrammer.AgentWorker.Security;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Harness.Acp;
using Autonogrammer.Harness.AgentLoop.Hooks;

namespace Autonogrammer.AgentWorker.Bridge;

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
    private readonly Func<CloudHooksLaunch, AcpStdioTransport> _createTransport;
    private readonly CloudHooksSession? _hooks;
    private readonly IDisposable _tokenTracking;
    private readonly WorkerSecretRedactor _redactor;
    private readonly ILogger<OmpAttachment> _logger;

    private AcpStdioTransport? _transport;
    private IDisposable? _hooksTokenTracking;
    private Task _stderrDrain = Task.CompletedTask;
    private int _disposed;

    /// <summary>Initializes a new instance of the <see cref="OmpAttachment"/> class.</summary>
    /// <param name="host">The host that owns the claim.</param>
    /// <param name="sessionId">The claimed session subject.</param>
    /// <param name="workingDirectory">The session directory the child runs in.</param>
    /// <param name="createTransport">Creates the omp transport once the hooks (if any) are started, given what they add to the launch.</param>
    /// <param name="hooks">The hooks of the connection, or null when the worker runs none.</param>
    /// <param name="tokenTracking">The handle that keeps the connection's token redacted.</param>
    /// <param name="redactor">Scrubs the child's stderr lines.</param>
    /// <param name="logger">Receives the stderr lines.</param>
    internal OmpAttachment(
        OmpProcessHost host,
        string sessionId,
        string workingDirectory,
        Func<CloudHooksLaunch, AcpStdioTransport> createTransport,
        CloudHooksSession? hooks,
        IDisposable tokenTracking,
        WorkerSecretRedactor redactor,
        ILogger<OmpAttachment> logger)
    {
        _host = host;
        SessionId = sessionId;
        _workingDirectory = workingDirectory;
        _createTransport = createTransport;
        _hooks = hooks;
        _tokenTracking = tokenTracking;
        _redactor = redactor;
        _logger = logger;
    }

    /// <summary>Gets the claimed session subject.</summary>
    public string SessionId { get; }

    /// <summary>Gets the transport to the omp child: one JSON-RPC message per frame. It exists once <see cref="StartAsync"/> has returned.</summary>
    /// <exception cref="InvalidOperationException">The attachment was not started.</exception>
    public IAcpTransport Transport => _transport ?? throw new InvalidOperationException("The attachment was not started.");

    /// <summary>
    /// Gets the frames the hooks want sent to the client (<c>_autonogrammer/hook</c> notifications), or null when the worker runs no hooks. The channel
    /// completes when the attachment is disposed.
    /// </summary>
    public ChannelReader<ReadOnlyMemory<byte>>? HookFrames => _hooks?.Frames;

    /// <summary>Creates the session directory, starts the hooks and then the omp child.</summary>
    /// <param name="cancellationToken">Cancels the start.</param>
    /// <exception cref="ProcessStartHarnessException">The directory or the process could not be created, or hooks that must block cannot be enforced.</exception>
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

        var launch = _hooks is null ? CloudHooksLaunch.None : await _hooks.StartAsync(cancellationToken).ConfigureAwait(false);
        if (launch.Environment.TryGetValue(HookBridgeEnvironment.Token, out var bridgeToken))
        {
            // The bridge token reaches only the omp child: keep it out of the log like the connection's own token.
            _hooksTokenTracking = _redactor.Track(bridgeToken);
        }

        _transport = _createTransport(launch);
        await _transport.ConnectAsync(cancellationToken).ConfigureAwait(false);
        _stderrDrain = DrainStderrAsync(_transport);
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
            if (_transport is not null)
            {
                await _transport.DisposeAsync().ConfigureAwait(false);
            }

            await _stderrDrain.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            if (_hooks is not null)
            {
                await _hooks.DisposeAsync().ConfigureAwait(false);
            }

            _hooksTokenTracking?.Dispose();
            _tokenTracking.Dispose();
            _host.Release(SessionId);
        }
    }

    private async Task DrainStderrAsync(AcpStdioTransport transport)
    {
        await foreach (var line in transport.StandardError.ReadAllAsync().ConfigureAwait(false))
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
