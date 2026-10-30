namespace Autonogrammer.Coordination;

/// <summary>
/// Best-effort wake-up signals keyed by device id. A signal names the device that may have a command waiting. It never
/// carries the command, so a lost signal only delays delivery until the next poll.
/// </summary>
public interface IDispatchSignals
{
    /// <summary>Publishes a wake-up for a device to every subscriber, including subscribers in other processes.</summary>
    /// <param name="deviceId">The device that may have work waiting. This is a hint, never a command.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task PublishAsync(string deviceId, CancellationToken cancellationToken);

    /// <summary>Subscribes a handler to wake-ups. Dispose the returned handle to unsubscribe.</summary>
    /// <param name="onSignal">Called with the device id of each signal and nothing else.</param>
    /// <param name="cancellationToken">Cancels the subscribe call. It does not end a subscription that has been established.</param>
    /// <returns>A handle that unsubscribes when disposed.</returns>
    Task<IAsyncDisposable> SubscribeAsync(Func<string, CancellationToken, ValueTask> onSignal, CancellationToken cancellationToken);
}
