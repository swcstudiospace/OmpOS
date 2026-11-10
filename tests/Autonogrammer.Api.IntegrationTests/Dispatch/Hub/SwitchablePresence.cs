using Autonogrammer.Coordination;
using Autonogrammer.Resilience;

namespace Autonogrammer.Api.IntegrationTests.Dispatch;

/// <summary>Presence that fails like an unreachable Dragonfly while the switch is down: every write throws and every read answers unknown (D-34).</summary>
internal sealed class SwitchablePresence : IDispatchPresence
{
    private readonly IDispatchPresence _inner;
    private readonly FaultSwitch _faults;

    /// <summary>Initializes a new instance of the <see cref="SwitchablePresence"/> class.</summary>
    /// <param name="inner">The healthy presence seam.</param>
    /// <param name="faults">The switch that makes the seam fail.</param>
    public SwitchablePresence(IDispatchPresence inner, FaultSwitch faults)
    {
        _inner = inner;
        _faults = faults;
    }

    /// <inheritdoc />
    public Task SetAsync(string accountId, string deviceId, string holder, TimeSpan timeToLive, CancellationToken cancellationToken)
    {
        return _faults.Down ? throw Unreachable() : _inner.SetAsync(accountId, deviceId, holder, timeToLive, cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> RefreshAsync(string accountId, string deviceId, string holder, TimeSpan timeToLive, CancellationToken cancellationToken)
    {
        return _faults.Down ? throw Unreachable() : _inner.RefreshAsync(accountId, deviceId, holder, timeToLive, cancellationToken);
    }

    /// <inheritdoc />
    public Task RemoveAsync(string accountId, string deviceId, CancellationToken cancellationToken)
    {
        return _faults.Down ? throw Unreachable() : _inner.RemoveAsync(accountId, deviceId, cancellationToken);
    }

    /// <inheritdoc />
    public Task<DispatchPresence> GetAsync(string accountId, string deviceId, CancellationToken cancellationToken)
    {
        return _faults.Down ? Task.FromResult(DispatchPresence.Unknown) : _inner.GetAsync(accountId, deviceId, cancellationToken);
    }

    private static DataPlaneTimeoutException Unreachable() => new("Dragonfly", DataPlaneTimeoutReason.Timeout);
}
