namespace Autonogrammer.Harness.Isolation;

/// <summary>
/// What the probe achieved, and the degraded-state code when it did not reach filesystem isolation. A passing namespace
/// self-test only shows that bubblewrap can start its namespaces; it never names a session level. Filesystem isolation
/// is reported only by a session canary that ran the exact launched argument vector and passed, and by a policy that
/// verifies. <see cref="ForVerifiedPolicy"/> is the planner's gate for both.
/// </summary>
/// <param name="AchievedLevel">The level the outcome reports. Filesystem isolation only after a passing session canary.</param>
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
    /// The session canary did not pass: the launched argument vector ran, but the policy was not enforced inside the
    /// sandbox (a deny is reachable, a socket is connectable, the scratch directory is not writable, or the sandbox
    /// does not start). The session needs consent to continue unconfined.
    /// </summary>
    public const string SessionPolicyUnenforced = "session_policy_unenforced";

    /// <summary>
    /// Gets the namespace probe's success. It names no session level: the namespaces start, but no filesystem policy has
    /// been checked. The planner treats it as <see cref="SessionPolicyUnenforced"/> until a session canary passes.
    /// </summary>
    public static IsolationProbeOutcome Succeeded { get; } = new(IsolationLevel.None, null);

    /// <summary>Gets the outcome of a session canary that ran the exact launched argument vector and every check passed.</summary>
    public static IsolationProbeOutcome SessionCanaryPassed { get; } = new(IsolationLevel.FilesystemIsolated, null);

    /// <summary>Gets the outcome of a session canary that failed, timed out, or could not start the launched vector.</summary>
    public static IsolationProbeOutcome SessionCanaryFailed { get; } = new(IsolationLevel.None, SessionPolicyUnenforced);

    /// <summary>Gets a value indicating whether the outcome reports filesystem isolation with no degraded code.</summary>
    public bool ReachedFilesystemIsolation =>
        AchievedLevel == IsolationLevel.FilesystemIsolated && DegradedCode is null;

    /// <summary>Gets the level this outcome reports for a session, given whether its policy verified.</summary>
    /// <param name="policyVerified">True when <see cref="BubblewrapArgvBuilder.Verify"/> accepted the session policy.</param>
    /// <returns>
    /// This outcome when it already degraded, and otherwise this outcome unchanged when the policy verified. A policy
    /// that did not verify returns <see cref="IsolationLevel.None"/> with <see cref="PolicyUnverified"/>.
    /// </returns>
    public IsolationProbeOutcome ForVerifiedPolicy(bool policyVerified)
    {
        if (DegradedCode is not null)
        {
            return this;
        }

        return policyVerified ? this : new IsolationProbeOutcome(IsolationLevel.None, PolicyUnverified);
    }
}
