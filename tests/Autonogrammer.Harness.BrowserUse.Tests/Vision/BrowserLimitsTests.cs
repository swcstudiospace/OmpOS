using Autonogrammer.Cowork.Abstractions;
using Autonogrammer.Harness.BrowserUse.Options;
using Autonogrammer.Harness.BrowserUse.Vision;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.BrowserUse.Tests.Vision;

/// <summary>Call limits, dispatch rules and the capability switch.</summary>
public sealed class BrowserLimitsTests
{
    [Fact]
    public void Rate_Session_Timeouts_AndScreenshotCap_Hold()
    {
        var limits = new BrowserLimits(new BrowserUseOptions());
        var now = DateTimeOffset.UnixEpoch;
        for (var i = 0; i < 60; i++)
        {
            limits.TryAdmit(now, out _).ShouldBeTrue();
        }

        limits.TryAdmit(now, out var rate).ShouldBeFalse();
        rate.ShouldBe("RateLimited");
        var session = new BrowserLimits(new BrowserUseOptions { MaxActionsPerMinute = 1000 });
        for (var i = 0; i < 500; i++)
        {
            session.TryAdmit(now, out _).ShouldBeTrue();
        }

        session.SessionWarning.ShouldBe("SessionCallWarning");
        session.TryAdmit(now, out var reached).ShouldBeFalse();
        reached.ShouldBe("SessionLimitReached");
        BrowserLimits.ToolTimeout.ShouldBe(TimeSpan.FromSeconds(60));
        BrowserLimits.PageLoadTimeout.ShouldBe(TimeSpan.FromSeconds(30));
        new BrowserLimits(new BrowserUseOptions { MaxScreenshotBytes = 9 * 1024 * 1024 }).MaxScreenshotBytes.ShouldBe(BrowserUseOptions.MaxScreenshotBytesCeiling);
        EncodingLength().ShouldBeLessThanOrEqualTo(1024);
    }

    [Fact]
    public void Dispatch_Blocks_OpenBecomesPrompt_AndAlwaysIsNotStored()
    {
        var blocked = new BrowserLimits(new BrowserUseOptions { AllowDispatchSessions = false });
        blocked.AllowsTurn(CoworkTurnOrigin.Dispatch).ShouldBeFalse();
        blocked.AllowsTurn(CoworkTurnOrigin.Local).ShouldBeTrue();
        var open = new BrowserLimits(new BrowserUseOptions { AllowDispatchSessions = true, PolicyMode = PolicyMode.Open });
        open.EffectivePolicy(CoworkTurnOrigin.Dispatch).ShouldBe(PolicyMode.PromptOnNewOrigin);
        open.EffectivePolicy(CoworkTurnOrigin.Local).ShouldBe(PolicyMode.Open);
        var (outcome, store) = BrowserLimits.NormalizeAlways(CoworkApprovalOutcome.AllowedAlways, CoworkTurnOrigin.Dispatch);
        outcome.ShouldBe(CoworkApprovalOutcome.Allowed);
        store.ShouldBeFalse();
        BrowserUseCapability.Name.ShouldBe("browser_use");
        BrowserUseCapability.IsOffered(new BrowserUseOptions()).ShouldBeFalse();
        BrowserUseCapability.IsOffered(new BrowserUseOptions { Enabled = true }).ShouldBeFalse();
        BrowserUseCapability.IsOffered(new BrowserUseOptions { AllowDispatchSessions = true }).ShouldBeFalse();
        BrowserUseCapability.IsOffered(new BrowserUseOptions { Enabled = true, AllowDispatchSessions = true }).ShouldBeTrue();
    }

    [Fact]
    public void VisionSwitch_WithholdsScreenshotPath_AndNamesTheChooser()
    {
        var authority = new Autonogrammer.Harness.BrowserUse.Tools.BrowserVisionAuthority();
        var blind = new CoworkSessionContext { SessionId = "s", WorkingDirectory = "/", TrustedFolders = [], ModelReadsImages = false };
        authority.Reads(blind).ShouldBeFalse();
        var seeing = blind with { ModelReadsImages = true };
        authority.Reads(seeing).ShouldBeTrue();
        authority.Set(false);
        authority.Reads(seeing).ShouldBeFalse();
        Autonogrammer.Harness.BrowserUse.Tools.BrowserScreenshotTool.ModelCannotSeeImages.ShouldContain("Choose a vision model");
    }

    private static int EncodingLength() => System.Text.Encoding.UTF8.GetByteCount(BrowserLoopGuidance.Fragment);
}
