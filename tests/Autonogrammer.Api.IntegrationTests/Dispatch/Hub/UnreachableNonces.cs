using Autonogrammer.Coordination;
using Autonogrammer.Resilience;

namespace Autonogrammer.Api.IntegrationTests.Dispatch;

/// <summary>A challenge-nonce seam whose Dragonfly cannot be reached, so every claim and consume fails closed (D-14).</summary>
internal sealed class UnreachableNonces : IDispatchAuthNonces
{
    /// <inheritdoc />
    public Task<bool> TryClaimAsync(string nonceSha256Hex, TimeSpan timeToLive, CancellationToken cancellationToken) =>
        throw new DataPlaneTimeoutException("Dragonfly", DataPlaneTimeoutReason.Timeout);

    /// <inheritdoc />
    public Task<bool> TryConsumeAsync(string nonceSha256Hex, CancellationToken cancellationToken) =>
        throw new DataPlaneTimeoutException("Dragonfly", DataPlaneTimeoutReason.Timeout);
}
