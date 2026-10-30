namespace Autonogrammer.Coordination;

/// <summary>
/// Device presence used for display and queueing, never for authorisation. A missing key is offline. An unreachable store
/// is <see cref="DispatchPresence.Status.Unknown"/>, never offline.
/// </summary>
public interface IDispatchPresence
{
    /// <summary>Writes the holder for a device with the given lifetime, replacing any previous holder.</summary>
    /// <param name="accountId">The authenticated account. The Dragonfly implementation hashes it before it joins a key.</param>
    /// <param name="deviceId">The device id.</param>
    /// <param name="holder">The holder, <c>instanceId:connectionId</c>.</param>
    /// <param name="timeToLive">How long the key lives. The hub uses 45 seconds. The lifetime is written in the same command as the value.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A task that completes when the write has been attempted. An unreachable store does not throw.</returns>
    Task SetAsync(string accountId, string deviceId, string holder, TimeSpan timeToLive, CancellationToken cancellationToken);

    /// <summary>
    /// Extends the lifetime only when the stored holder still equals <paramref name="holder"/>. Another connection, or a
    /// missing key, leaves the key unchanged and returns <see langword="false"/>.
    /// </summary>
    /// <param name="accountId">The authenticated account.</param>
    /// <param name="deviceId">The device id.</param>
    /// <param name="holder">The holder that must still own the key.</param>
    /// <param name="timeToLive">The new lifetime, written only when the compare succeeds.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns><see langword="true"/> when this holder still owned the key and the lifetime was extended.</returns>
    Task<bool> RefreshAsync(string accountId, string deviceId, string holder, TimeSpan timeToLive, CancellationToken cancellationToken);

    /// <summary>Deletes the presence key. Used on revocation. An unreachable store does not throw.</summary>
    /// <param name="accountId">The authenticated account.</param>
    /// <param name="deviceId">The device id.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A task that completes when the delete has been attempted.</returns>
    Task RemoveAsync(string accountId, string deviceId, CancellationToken cancellationToken);

    /// <summary>Reads presence. A missing key is offline. An unreachable store is unknown, never offline.</summary>
    /// <param name="accountId">The authenticated account.</param>
    /// <param name="deviceId">The device id.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>Online with the holder, offline, or unknown.</returns>
    Task<DispatchPresence> GetAsync(string accountId, string deviceId, CancellationToken cancellationToken);
}
