namespace Autonogrammer.Resilience;

/// <summary>What a <see cref="CircuitBreaker"/> answered to a request to make a call.</summary>
internal enum BreakerPermit
{
    /// <summary>The breaker is open, or half-open with its probe already out: the call must not be made.</summary>
    Rejected = 0,

    /// <summary>The breaker is closed: the call may be made.</summary>
    Allowed = 1,

    /// <summary>The breaker is half-open and this is its one probe: the call may be made and its outcome decides the breaker.</summary>
    Probe = 2,
}
