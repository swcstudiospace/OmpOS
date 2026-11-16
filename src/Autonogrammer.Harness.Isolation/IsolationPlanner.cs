using Autonogrammer.Harness.Abstractions;

namespace Autonogrammer.Harness.Isolation;

/// <summary>
/// Decides the wrapper for one session. Code needs no probe and gets no wrapper. A failed Cowork probe needs consent
/// before the session continues unconfined. A successful probe returns the platform wrapper and filesystem isolation
/// only when the session policy verifies: a policy whose argument vector is incomplete or out of precedence degrades to
/// <see cref="IsolationProbeOutcome.PolicyUnverified"/>. Windows stays unconfined: the job object is type-only until a
/// post-start hook exists.
/// </summary>
public static class IsolationPlanner
{
    /// <summary>Decides the launch wrapper and the level to show.</summary>
    /// <param name="policy">The session policy. A Code policy ignores the probe.</param>
    /// <param name="probe">The probe outcome. Ignored when <paramref name="policy"/> is off.</param>
    /// <param name="continueWithoutIsolation">True when the user already answered the continue-without-isolation question.</param>
    /// <returns>The decision. A failed probe or unverified policy without consent has no wrapper and <see cref="IsolationDecision.NeedsConsent"/>.</returns>
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

        var outcome = probe.ForVerifiedPolicy(PolicyVerifies(policy));
        if (outcome.DegradedCode is not null)
        {
            return Unconfined(outcome.DegradedCode, continueWithoutIsolation);
        }

        IProcessLaunchWrapper wrapper = policy.Platform == IsolationPlatform.MacOs
            ? new SeatbeltIsolator(policy)
            : new BubblewrapIsolator(policy);
        return new IsolationDecision(outcome.AchievedLevel, wrapper, null, false);
    }

    private static bool PolicyVerifies(IsolationPolicy policy)
    {
        try
        {
            if (policy.Platform == IsolationPlatform.MacOs)
            {
                return SeatbeltVerifies(policy);
            }

            BubblewrapArgvBuilder.Verify(policy);
            return true;
        }
        catch (MalformedArgvException)
        {
            return false;
        }
    }

    private static bool SeatbeltVerifies(IsolationPolicy policy)
    {
        var profile = SeatbeltProfileBuilder.Build(policy);
        IEnumerable<string> writable = policy.Grants
            .Where(grant => grant.ReadWrite)
            .Select(grant => grant.Path)
            .Concat(policy.StateDirectories());
        if (!string.IsNullOrWhiteSpace(policy.ScratchDirectory))
        {
            writable = writable.Append(policy.ScratchDirectory);
        }

        return writable.All(path =>
            !string.IsNullOrWhiteSpace(path)
            && profile.Contains(
                "(allow file-write* (subpath " + SeatbeltProfileBuilder.Quote(path) + "))\n",
                StringComparison.Ordinal));
    }

    private static IsolationDecision Unconfined(string code, bool continueWithoutIsolation) =>
        new(IsolationLevel.None, null, code, !continueWithoutIsolation);
}
