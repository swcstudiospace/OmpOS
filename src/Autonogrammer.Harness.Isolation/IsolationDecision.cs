using Autonogrammer.Harness.Abstractions;

namespace Autonogrammer.Harness.Isolation;

/// <summary>What a session start should do with isolation: the level, the wrapper or none, the degraded code, and whether consent is required.</summary>
/// <param name="Level">The level to report. <see cref="IsolationLevel.None"/> when there is no wrapper.</param>
/// <param name="Wrapper">The wrapper to apply, or null when the child starts directly.</param>
/// <param name="DegradedCode">The named degraded state, or null when the probe succeeded or isolation is off.</param>
/// <param name="NeedsConsent">True when a failed probe must be answered before the session starts. The session does not start until then.</param>
public sealed record IsolationDecision(
    IsolationLevel Level,
    IProcessLaunchWrapper? Wrapper,
    string? DegradedCode,
    bool NeedsConsent);
