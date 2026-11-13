using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Harness.Isolation.Tests.Support;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Isolation.Tests;

/// <summary>The planner covers Code, a failed probe, consent and a successful probe, and the contributor returns that wrapper.</summary>
public sealed class IsolationPlannerTests
{
    /// <summary>Code needs no probe and gets no wrapper, even when the probe failed.</summary>
    [Fact]
    public void CodeNeedsNoProbeAndGetsNoWrapper()
    {
        var decision = IsolationPlanner.Decide(
            IsolationPolicy.ForCode(),
            new IsolationProbeOutcome(IsolationLevel.None, IsolationProbeOutcome.BwrapMissing),
            false);

        decision.Level.ShouldBe(IsolationLevel.None);
        decision.Wrapper.ShouldBeNull();
        decision.NeedsConsent.ShouldBeFalse();
        decision.DegradedCode.ShouldBeNull();
        Contribute(decision).ProcessWrapper.ShouldBeNull();
    }

    /// <summary>A failed Cowork probe without consent needs an answer and gets no wrapper.</summary>
    [Fact]
    public void FailedProbeWithoutConsentNeedsConsent()
    {
        var decision = IsolationPlanner.Decide(Cowork(), Failed(), false);

        decision.NeedsConsent.ShouldBeTrue();
        decision.Level.ShouldBe(IsolationLevel.None);
        decision.Wrapper.ShouldBeNull();
        decision.DegradedCode.ShouldBe(IsolationProbeOutcome.BwrapMissing);
        Contribute(decision).ProcessWrapper.ShouldBeNull();
    }

    /// <summary>Consent continues at level None with no wrapper.</summary>
    [Fact]
    public void ConsentContinuesUnconfined()
    {
        var decision = IsolationPlanner.Decide(
            Cowork(),
            new IsolationProbeOutcome(IsolationLevel.None, IsolationProbeOutcome.UsernsDisabled),
            true);

        decision.NeedsConsent.ShouldBeFalse();
        decision.Level.ShouldBe(IsolationLevel.None);
        decision.Wrapper.ShouldBeNull();
        decision.DegradedCode.ShouldBe(IsolationProbeOutcome.UsernsDisabled);
    }

    /// <summary>A passing session canary over a verified policy returns the wrapper and the achieved level.</summary>
    [Fact]
    public async Task SuccessfulProbeReturnsTheWrapper()
    {
        using var fixture = new TempIsolationFixture();
        var policy = fixture.CoworkPolicy();
        var probe = new IsolationProbe(new FakeProcessRunner(), "linux-x64", TimeSpan.FromSeconds(5));

        var outcome = await probe.ProbeSessionAsync(policy, CancellationToken.None).ConfigureAwait(true);
        var decision = IsolationPlanner.Decide(policy, outcome, false);

        decision.NeedsConsent.ShouldBeFalse();
        decision.Level.ShouldBe(IsolationLevel.FilesystemIsolated);
        decision.DegradedCode.ShouldBeNull();
        decision.Wrapper.ShouldBeOfType<BubblewrapIsolator>();
        Contribute(decision).ProcessWrapper.ShouldBeSameAs(decision.Wrapper);
    }

    /// <summary>Windows stays unconfined with platform_unsupported even when a probe claims success.</summary>
    [Fact]
    public void WindowsReportsPlatformUnsupported()
    {
        var policy = IsolationPolicy.ForCowork("/home/ada", "/tmp/scratch", platform: IsolationPlatform.Windows);

        var decision = IsolationPlanner.Decide(policy, IsolationProbeOutcome.Succeeded, false);

        decision.Level.ShouldBe(IsolationLevel.None);
        decision.Wrapper.ShouldBeNull();
        decision.DegradedCode.ShouldBe(IsolationProbeOutcome.PlatformUnsupported);
        decision.NeedsConsent.ShouldBeTrue();
    }

    private static IsolationPolicy Cowork() =>
        IsolationPolicy.ForCowork("/home/ada", "/tmp/scratch", [new IsolationGrant("/home/ada/project", true)]);

    private static IsolationProbeOutcome Failed() =>
        new(IsolationLevel.None, IsolationProbeOutcome.BwrapMissing);

    private static LaunchContribution Contribute(IsolationDecision decision) =>
        new IsolationContributor(decision).Contribute(
            new LaunchRequest
            {
                Surface = "cowork",
                SessionId = "s",
                WorkingDirectory = "/home/ada/project",
                TrustedFolders = [],
            },
            CancellationToken.None);
}
