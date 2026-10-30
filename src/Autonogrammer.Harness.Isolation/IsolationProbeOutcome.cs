namespace Autonogrammer.Harness.Isolation;

/// <summary>What the probe achieved, and the degraded-state code when it did not reach filesystem isolation.</summary>
/// <param name="AchievedLevel">The level the probe actually reached.</param>
/// <param name="DegradedCode">The named degraded state, or null when the probe succeeded.</param>
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

    /// <summary>Gets a successful filesystem-isolation outcome.</summary>
    public static IsolationProbeOutcome Succeeded { get; } = new(IsolationLevel.FilesystemIsolated, null);

    /// <summary>Gets a value indicating whether the probe reached filesystem isolation with no degraded code.</summary>
    public bool ReachedFilesystemIsolation =>
        AchievedLevel == IsolationLevel.FilesystemIsolated && DegradedCode is null;
}
