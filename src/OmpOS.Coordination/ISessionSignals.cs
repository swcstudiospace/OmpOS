namespace OmpOS.Coordination;

/// <summary>
/// Best-effort wake-up signals keyed by session id. A signal tells subscribers that a session changed. It never carries
/// the change, so a lost signal only delays delivery until the next read.
/// </summary>
public interface ISessionSignals
{
    /// <summary>Publishes a wake-up for a session to every subscriber, including subscribers in other processes.</summary>
    /// <param name="sessionId">The session that changed.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task PublishAsync(string sessionId, CancellationToken cancellationToken);

    /// <summary>Subscribes a handler to wake-ups. Dispose the returned handle to unsubscribe.</summary>
    /// <param name="onSignal">Called with the session id of each signal.</param>
    /// <param name="cancellationToken">Cancels the subscribe call. It does not end a subscription that has been established.</param>
    /// <returns>A handle that unsubscribes when disposed.</returns>
    Task<IAsyncDisposable> SubscribeAsync(Func<string, CancellationToken, ValueTask> onSignal, CancellationToken cancellationToken);
}
