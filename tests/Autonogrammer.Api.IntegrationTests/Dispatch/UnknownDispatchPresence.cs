using Autonogrammer.Coordination;

namespace Autonogrammer.Api.IntegrationTests.Dispatch;

/// <summary>A presence seam that is always unreachable, so the presence-unknown answer can be tested. It never reports offline.</summary>
internal sealed class UnknownDispatchPresence : IDispatchPresence
{
    /// <inheritdoc />
    public Task SetAsync(string accountId, string deviceId, string holder, TimeSpan timeToLive, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task<bool> RefreshAsync(string accountId, string deviceId, string holder, TimeSpan timeToLive, CancellationToken cancellationToken) => Task.FromResult(false);

    /// <inheritdoc />
    public Task RemoveAsync(string accountId, string deviceId, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task<DispatchPresence> GetAsync(string accountId, string deviceId, CancellationToken cancellationToken) => Task.FromResult(DispatchPresence.Unknown);
}
