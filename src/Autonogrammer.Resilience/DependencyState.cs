namespace Autonogrammer.Resilience;

/// <summary>
/// How healthy the breaker of one data-plane component says the dependency is. The status service of the API maps
/// <see cref="Healthy"/> to healthy, <see cref="Degraded"/> to degraded and <see cref="Open"/> to unavailable.
/// </summary>
public enum DependencyState
{
    /// <summary>The breaker is closed and no call failed within the sampling window.</summary>
    Healthy = 0,

    /// <summary>The breaker is closed or half-open and recent calls failed, so the dependency is still serving but is suspect.</summary>
    Degraded = 1,

    /// <summary>The breaker is open: calls fail fast until the open window has passed and a probe succeeds.</summary>
    Open = 2,
}
