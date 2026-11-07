using Autonogrammer.Contracts.Dispatch;

namespace Autonogrammer.Dispatch.Desktop;

/// <summary>
/// The reconnect schedule. A jittered delay is uniform in <c>[0, min(30 s, 1 s x 2^attempt))</c>. The attempt counter resets when a
/// connection stayed up for at least 60 seconds. The schedule has no attempt limit.
/// </summary>
public sealed class DispatchReconnectLoop
{
    /// <summary>The longest jittered delay.</summary>
    public static readonly TimeSpan Ceiling = TimeSpan.FromSeconds(30);

    /// <summary>How long a connection must stay up before the counter resets.</summary>
    public static readonly TimeSpan StableAfter = TimeSpan.FromSeconds(60);

    private const int MaxTrackedAttempt = 16;

    private readonly TimeProvider _clock;
    private readonly IJitterSource _jitter;
    private int _attempt;
    private DateTimeOffset? _connectedAt;

    /// <summary>Creates the schedule.</summary>
    /// <param name="clock">The clock that times the stable window.</param>
    /// <param name="jitter">The source of the uniform sample.</param>
    public DispatchReconnectLoop(TimeProvider clock, IJitterSource jitter)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _jitter = jitter ?? throw new ArgumentNullException(nameof(jitter));
    }

    /// <summary>Gets the number of jittered attempts since the last reset.</summary>
    public int Attempt => _attempt;

    /// <summary>Records that a connection is now up.</summary>
    public void MarkConnected() => _connectedAt = _clock.GetUtcNow();

    /// <summary>Records that the connection ended. Resets the counter when it stayed up for at least <see cref="StableAfter"/>.</summary>
    public void MarkDisconnected()
    {
        if (_connectedAt is { } connectedAt && _clock.GetUtcNow() - connectedAt >= StableAfter)
        {
            _attempt = 0;
        }

        _connectedAt = null;
    }

    /// <summary>Returns the next jittered delay and advances the counter.</summary>
    /// <returns>A delay in <c>[0, Ceiling)</c>.</returns>
    public TimeSpan NextJitteredDelay()
    {
        var cap = Math.Min(Ceiling.TotalSeconds, Math.Pow(2, _attempt));
        if (_attempt < MaxTrackedAttempt)
        {
            _attempt++;
        }

        return TimeSpan.FromSeconds(cap * _jitter.NextDouble());
    }
}
