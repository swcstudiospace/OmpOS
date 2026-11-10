using Autonogrammer.Coordination;

namespace Autonogrammer.Api.IntegrationTests.Dispatch;

/// <summary>A presence seam that delegates to an inner seam and records the lifetime each write asked for.</summary>
internal sealed class RecordingPresence : IDispatchPresence
{
    private readonly IDispatchPresence _inner;
    private readonly List<TimeSpan> _set = [];
    private readonly List<TimeSpan> _refresh = [];

    /// <summary>Initializes a new instance of the <see cref="RecordingPresence"/> class.</summary>
    /// <param name="inner">The seam that stores the presence.</param>
    public RecordingPresence(IDispatchPresence inner)
    {
        _inner = inner;
    }

    /// <summary>Gets the lifetimes passed to <see cref="SetAsync"/>, in order.</summary>
    public IReadOnlyList<TimeSpan> SetLifetimes => _set;

    /// <summary>Gets the lifetimes passed to <see cref="RefreshAsync"/>, in order.</summary>
    public IReadOnlyList<TimeSpan> RefreshLifetimes => _refresh;

    /// <inheritdoc />
    public Task SetAsync(string accountId, string deviceId, string holder, TimeSpan timeToLive, CancellationToken cancellationToken)
    {
        _set.Add(timeToLive);
        return _inner.SetAsync(accountId, deviceId, holder, timeToLive, cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> RefreshAsync(string accountId, string deviceId, string holder, TimeSpan timeToLive, CancellationToken cancellationToken)
    {
        _refresh.Add(timeToLive);
        return _inner.RefreshAsync(accountId, deviceId, holder, timeToLive, cancellationToken);
    }

    /// <inheritdoc />
    public Task RemoveAsync(string accountId, string deviceId, CancellationToken cancellationToken) => _inner.RemoveAsync(accountId, deviceId, cancellationToken);

    /// <inheritdoc />
    public Task<DispatchPresence> GetAsync(string accountId, string deviceId, CancellationToken cancellationToken) => _inner.GetAsync(accountId, deviceId, cancellationToken);
}
