namespace Autonogrammer.Presentation.Dispatch;

/// <summary>
/// The Dispatch host of a head that has no Dispatch service configured. It refuses every call with
/// <see cref="DispatchHostFailureKind.NotConfigured"/> and never touches the network.
/// </summary>
public sealed class NotConfiguredDispatchHost : IDispatchHost, IThisDeviceHost
{
    /// <inheritdoc/>
    public Task<IReadOnlyList<DispatchDevice>> ListDevicesAsync(CancellationToken cancellationToken) =>
        RefuseAsync<IReadOnlyList<DispatchDevice>>();

    /// <inheritdoc/>
    public Task<IReadOnlyList<DispatchCommand>> ListCommandsAsync(CancellationToken cancellationToken) =>
        RefuseAsync<IReadOnlyList<DispatchCommand>>();

    /// <inheritdoc/>
    public Task RenameDeviceAsync(string deviceId, string name, CancellationToken cancellationToken) => RefuseAsync<object>();

    /// <inheritdoc/>
    public Task RevokeDeviceAsync(string deviceId, CancellationToken cancellationToken) => RefuseAsync<object>();

    /// <inheritdoc/>
    public Task RevokeAllDevicesAsync(CancellationToken cancellationToken) => RefuseAsync<object>();

    /// <inheritdoc/>
    public Task<string> SendAsync(string deviceId, string operation, string text, CancellationToken cancellationToken) => RefuseAsync<string>();

    /// <inheritdoc/>
    public Task CancelAsync(string commandId, CancellationToken cancellationToken) => RefuseAsync<object>();

    /// <inheritdoc/>
    public IAsyncEnumerable<DispatchFeedChange> WatchFeedAsync(CancellationToken cancellationToken) => throw RefusedException();

    /// <inheritdoc/>
    public Task<DispatchThisDevice> GetThisDeviceAsync(CancellationToken cancellationToken) => RefuseAsync<DispatchThisDevice>();

    /// <inheritdoc/>
    public Task SetCapabilityAsync(string capability, bool enabled, CancellationToken cancellationToken) => RefuseAsync<object>();

    /// <inheritdoc/>
    public Task SetPausedAsync(bool paused, CancellationToken cancellationToken) => RefuseAsync<object>();

    /// <inheritdoc/>
    public Task ResolveApprovalAsync(string approvalId, bool approved, CancellationToken cancellationToken) => RefuseAsync<object>();

    private static Task<T> RefuseAsync<T>() => Task.FromException<T>(RefusedException());

    private static DispatchHostException RefusedException() =>
        new(DispatchHostFailureKind.NotConfigured, DispatchCopy.NotConfigured);
}
