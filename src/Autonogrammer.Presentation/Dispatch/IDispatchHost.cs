namespace Autonogrammer.Presentation.Dispatch;

/// <summary>
/// What the Dispatch surfaces need from the head: the account's devices, their renames and revocations, sending and cancelling commands,
/// and the feed. A head maps its calls onto this interface; the presentation never sees a wire type or a token. Every expected failure is a
/// <see cref="DispatchHostException"/>.
/// </summary>
public interface IDispatchHost
{
    /// <summary>Lists the account's devices.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The devices, in any order.</returns>
    /// <exception cref="DispatchHostException">The call failed.</exception>
    Task<IReadOnlyList<DispatchDevice>> ListDevicesAsync(CancellationToken cancellationToken);

    /// <summary>Renames a device.</summary>
    /// <param name="deviceId">The device.</param>
    /// <param name="name">The new name.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the service accepted the rename.</returns>
    /// <exception cref="DispatchHostException">The call failed.</exception>
    Task RenameDeviceAsync(string deviceId, string name, CancellationToken cancellationToken);

    /// <summary>Revokes one device, so it no longer receives commands.</summary>
    /// <param name="deviceId">The device.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the service accepted the revocation.</returns>
    /// <exception cref="DispatchHostException">The call failed.</exception>
    Task RevokeDeviceAsync(string deviceId, CancellationToken cancellationToken);

    /// <summary>Revokes every device on the account.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the service accepted the revocations.</returns>
    /// <exception cref="DispatchHostException">The call failed.</exception>
    Task RevokeAllDevicesAsync(CancellationToken cancellationToken);

    /// <summary>Sends a command to a device.</summary>
    /// <param name="deviceId">The device.</param>
    /// <param name="operation">The operation, from the closed set.</param>
    /// <param name="text">The task text.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The identifier of the new command.</returns>
    /// <exception cref="DispatchHostException">The call failed or was refused.</exception>
    Task<string> SendAsync(string deviceId, string operation, string text, CancellationToken cancellationToken);

    /// <summary>Cancels a command that has not finished.</summary>
    /// <param name="commandId">The command.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the service accepted the cancellation.</returns>
    /// <exception cref="DispatchHostException">The call failed.</exception>
    Task CancelAsync(string commandId, CancellationToken cancellationToken);

    /// <summary>Watches the feed of command changes until the token is cancelled.</summary>
    /// <param name="cancellationToken">Ends the watch.</param>
    /// <returns>The changes, in order.</returns>
    IAsyncEnumerable<DispatchFeedChange> WatchFeedAsync(CancellationToken cancellationToken);
}
