namespace Autonogrammer.Harness.Isolation;

/// <summary>
/// What the probe achieved, and the degraded-state code when it did not reach filesystem isolation. A successful probe
/// only shows that bubblewrap can start its namespaces. It never names a session level by itself: the level comes from
/// <see cref="ForVerifiedPolicy"/>, which the planner calls only after the session policy verifies.
/// </summary>
/// <param name="AchievedLevel">The level the outcome reports. Filesystem isolation only after policy verification.</param>
/// <param name="DegradedCode">The named degraded state, or null when the outcome is not degraded.</param>
public sealed record IsolationProbeOutcome(IsolationLevel AchievedLevel, string? DegradedCode)
{
    /// <summary>Bubblewrap is not on <c>PATH</c>.</summary>
    public const string BwrapMissing = "bwrap_missing";

    /// <summary>Bubblewrap started but the user-namespace self-test failed.</summary>
    public const string UsernsDisabled = "userns_disabled";

    /// <summary>The probe did not finish within its timeout.</summary>
    public const string ProbeTimeout = "probe_timeout";

    /// <summary>This runtime has no filesystem isolator wired.</summary>
    public const string PlatformUnsupported = "platform_unsupported";

    /// <summary>The session policy did not build into a complete, correctly ordered argument vector on this host.</summary>
    public const string PolicyUnverified = "policy_unverified";

    /// <summary>
    /// Gets the namespace probe's success. It is not a session level: <see cref="ForVerifiedPolicy"/> decides the level
    /// once the session policy verifies.
    /// </summary>
    public static IsolationProbeOutcome Succeeded { get; } = new(IsolationLevel.FilesystemIsolated, null);

    /// <summary>Gets a value indicating whether the outcome reports filesystem isolation with no degraded code.</summary>
    public bool ReachedFilesystemIsolation =>
        AchievedLevel == IsolationLevel.FilesystemIsolated && DegradedCode is null;

    /// <summary>Gets the level this outcome reports for a session, given whether its policy verified.</summary>
    /// <param name="policyVerified">True when <see cref="BubblewrapArgvBuilder.Verify"/> accepted the session policy.</param>
    /// <returns>
    /// This outcome when it already degraded. Otherwise filesystem isolation when the policy verified, and
    /// <see cref="IsolationLevel.None"/> with <see cref="PolicyUnverified"/> when it did not.
    /// </returns>
    public IsolationProbeOutcome ForVerifiedPolicy(bool policyVerified)
    {
        if (DegradedCode is not null)
        {
            return this;
        }

        return policyVerified
            ? this with { AchievedLevel = IsolationLevel.FilesystemIsolated }
            : new IsolationProbeOutcome(IsolationLevel.None, PolicyUnverified);
    }
}
