using System.Collections.Concurrent;

namespace Autonogrammer.Api.Cloud;

/// <summary>
/// The cancellation handles of the cloud runs in flight on this instance, by session. A cancel or stop request signals the handle; the run's harness
/// session releases it when it is disposed. Like the hosted run registry, it is per process: a cancel reaches only a run this instance is executing.
/// </summary>
internal sealed class CloudRunTracker : IDisposable
{
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _runs = new(StringComparer.Ordinal);

    /// <summary>Registers a run and returns the token that stops it.</summary>
    /// <param name="sessionId">The session of the run.</param>
    /// <returns>The token a cancel request signals.</returns>
    public CancellationToken Register(string sessionId)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        var source = new CancellationTokenSource();
        if (_runs.TryRemove(sessionId, out var previous))
        {
            previous.Dispose();
        }

        _runs[sessionId] = source;
        return source.Token;
    }

    /// <summary>Signals the run of a session to stop.</summary>
    /// <param name="sessionId">The session of the run.</param>
    /// <returns>True when a run was registered and has been signalled.</returns>
    public bool TryCancel(string sessionId)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        if (!_runs.TryGetValue(sessionId, out var source))
        {
            return false;
        }

        try
        {
            source.Cancel();
        }
        catch (ObjectDisposedException)
        {
            return false;
        }

        return true;
    }

    /// <summary>Releases the handle of a run that ended.</summary>
    /// <param name="sessionId">The session of the run.</param>
    public void Complete(string sessionId)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        if (_runs.TryRemove(sessionId, out var source))
        {
            source.Dispose();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var source in _runs.Values)
        {
            source.Dispose();
        }

        _runs.Clear();
    }
}
