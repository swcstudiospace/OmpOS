namespace Autonogrammer.Harness.Abstractions;

/// <summary>
/// The window a permission request waits for its decision. The ACP session that answers the agent and the run that registers the
/// request both read this one value, so both sides end the wait together. The side that denies settles the stored request before
/// it answers, so a decision that arrives after the window cannot be recorded as a grant.
/// </summary>
public static class PermissionDecisionWindow
{
    /// <summary>Gets the default decision window: 30 seconds.</summary>
    public static TimeSpan Default { get; } = TimeSpan.FromSeconds(30);
}
