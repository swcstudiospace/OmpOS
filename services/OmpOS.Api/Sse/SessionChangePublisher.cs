using OmpOS.Coordination;

namespace OmpOS.Api.Sse;

/// <summary>
/// Announces a committed session change: wakes this process's SSE streams and publishes the change to the other replicas
/// through <see cref="ISessionSignals"/>. Call it after every durable session event append, beside the local wake-up.
/// </summary>
/// <remarks>
/// Signals are best-effort. The write has already committed when this runs, so a failed publish is logged and never thrown;
/// the other replicas' streams still deliver the change through their poll within one poll interval.
/// </remarks>
internal sealed class SessionChangePublisher
{
    private static readonly Action<ILogger, string, string, Exception?> SignalPublishFailed =
        LoggerMessage.Define<string, string>(
            LogLevel.Warning,
            new EventId(1, "SessionSignalPublishFailed"),
            "Session signal publish failed for session {SessionId}: {ErrorType}. Polling delivers the change.");

    private readonly SessionEventStreamer _streamer;
    private readonly ISessionSignals _signals;
    private readonly ILogger<SessionChangePublisher> _logger;

    /// <summary>Initializes a new instance of the <see cref="SessionChangePublisher"/> class.</summary>
    /// <param name="streamer">The SSE broadcaster of this process.</param>
    /// <param name="signals">The cross-process session signal channel.</param>
    /// <param name="logger">The logger.</param>
    public SessionChangePublisher(SessionEventStreamer streamer, ISessionSignals signals, ILogger<SessionChangePublisher> logger)
    {
        _streamer = streamer;
        _signals = signals;
        _logger = logger;
    }

    /// <summary>Wakes this process's streams for <paramref name="sessionId"/> and publishes the change to other replicas.</summary>
    /// <param name="sessionId">The session whose log or status changed.</param>
    /// <returns>A task that completes after the publish attempt; it never faults.</returns>
    public async Task NotifyAsync(string sessionId)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        _streamer.Notify(sessionId);
        try
        {
            // The write already committed, so the publish is not tied to any request's cancellation.
            await _signals.PublishAsync(sessionId, CancellationToken.None).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Signals are best-effort: a failed publish must never fail a committed write.
        catch (Exception ex)
        {
            SignalPublishFailed(_logger, sessionId, ex.GetType().FullName ?? "Unknown", null);
        }
#pragma warning restore CA1031
    }
}
