using Autonogrammer.Coordination;

namespace Autonogrammer.Api.Sse;

/// <summary>
/// Receives session signals published by other replicas and wakes this process's SSE streams for the signalled session.
/// A failed subscription is logged and the streams keep delivering through their poll, so signals only lower latency.
/// </summary>
internal sealed class SessionSignalSubscriber : IHostedService
{
    private static readonly Action<ILogger, string, Exception?> SubscribeFailed =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(2, "SessionSignalSubscribeFailed"),
            "Session signal subscription failed: {ErrorType}. Streams fall back to polling.");

    private readonly ISessionSignals _signals;
    private readonly SessionEventStreamer _streamer;
    private readonly ILogger<SessionSignalSubscriber> _logger;
    private IAsyncDisposable? _subscription;

    /// <summary>Initializes a new instance of the <see cref="SessionSignalSubscriber"/> class.</summary>
    /// <param name="signals">The cross-process session signal channel.</param>
    /// <param name="streamer">The SSE broadcaster of this process.</param>
    /// <param name="logger">The logger.</param>
    public SessionSignalSubscriber(ISessionSignals signals, SessionEventStreamer streamer, ILogger<SessionSignalSubscriber> logger)
    {
        _signals = signals;
        _streamer = streamer;
        _logger = logger;
    }

    /// <summary>Wakes this process's streams for a signalled session.</summary>
    /// <param name="sessionId">The session identifier carried by the signal.</param>
    /// <param name="cancellationToken">Cancels the delivery before the wake-up.</param>
    /// <returns>A completed task.</returns>
    public ValueTask OnSignalAsync(string sessionId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        cancellationToken.ThrowIfCancellationRequested();
        _streamer.Notify(sessionId);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _subscription = await _signals.SubscribeAsync(OnSignalAsync, cancellationToken).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // A failed subscription must not stop the host; polling keeps streams correct.
        catch (Exception ex)
        {
            SubscribeFailed(_logger, ex.GetType().FullName ?? "Unknown", null);
        }
#pragma warning restore CA1031
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        var subscription = _subscription;
        _subscription = null;
        if (subscription is not null)
        {
            await subscription.DisposeAsync().ConfigureAwait(false);
        }
    }
}
