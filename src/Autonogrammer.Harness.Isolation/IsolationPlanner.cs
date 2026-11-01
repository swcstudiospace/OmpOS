using Autonogrammer.Harness.Abstractions;

namespace Autonogrammer.Harness.Isolation;

/// <summary>
/// Decides the wrapper for one session. Code needs no probe and gets no wrapper. A failed Cowork probe needs consent
/// before the session continues unconfined. A successful probe returns the platform wrapper. Windows stays unconfined:
/// the job object is type-only until a post-start hook exists.
/// </summary>
public static class IsolationPlanner
{
    /// <summary>Decides the launch wrapper and the level to show.</summary>
    /// <param name="policy">The session policy. A Code policy ignores the probe.</param>
    /// <param name="probe">The probe outcome. Ignored when <paramref name="policy"/> is off.</param>
    /// <param name="continueWithoutIsolation">True when the user already answered the continue-without-isolation question.</param>
    /// <returns>The decision. A failed probe without consent has no wrapper and <see cref="IsolationDecision.NeedsConsent"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="policy"/> or <paramref name="probe"/> is null.</exception>
    public static IsolationDecision Decide(IsolationPolicy policy, IsolationProbeOutcome probe, bool continueWithoutIsolation)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(probe);
        if (!policy.Enabled)
        {
            return new IsolationDecision(IsolationLevel.None, null, null, false);
        }

        if (policy.Platform == IsolationPlatform.Windows)
        {
            return Unconfined(IsolationProbeOutcome.PlatformUnsupported, continueWithoutIsolation);
        }

        if (probe.DegradedCode is not null)
        {
            return Unconfined(probe.DegradedCode, continueWithoutIsolation);
        }

        IProcessLaunchWrapper wrapper = policy.Platform == IsolationPlatform.MacOs
            ? new SeatbeltIsolator(policy)
            : new BubblewrapIsolator(policy);
        return new IsolationDecision(probe.AchievedLevel, wrapper, null, false);
    }

    private static IsolationDecision Unconfined(string code, bool continueWithoutIsolation) =>
        new(IsolationLevel.None, null, code, !continueWithoutIsolation);
}
