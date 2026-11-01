using Autonogrammer.Harness.ComputerUse.Abstractions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.ComputerUse.Tests.Abstractions;

/// <summary>Environment reader uses Cowork__ComputerUse__ names and never echoes values.</summary>
public sealed class ComputerUseOptionsReaderTests
{
    [Fact]
    public void RatePerMinuteVariableReachesTheOption()
    {
        ComputerUseOptionsReadResult result = ComputerUseOptionsReader.Read(name =>
            string.Equals(name, "Cowork__ComputerUse__RatePerMinute", StringComparison.Ordinal) ? "17" : null);
        result.Options.RatePerMinute.ShouldBe(17);
        result.Options.Enabled.ShouldBeFalse();
        result.Failures.ShouldBeEmpty();
    }

    [Fact]
    public void AbsentVariablesKeepDefaults()
    {
        ComputerUseOptionsReadResult result = ComputerUseOptionsReader.Read(_ => null);
        result.Options.Enabled.ShouldBeFalse();
        result.Options.AllowDispatchSessions.ShouldBeFalse();
        result.Options.KillChord.ShouldBe("Ctrl+Alt+Shift+Escape");
        result.Options.RatePerMinute.ShouldBe(60);
        result.Options.RateBurst.ShouldBe(10);
        result.Options.StepLimit.ShouldBe(300);
        result.Options.StepContinueGrant.ShouldBe(100);
        result.Options.FailurePause.ShouldBe(5);
        result.Options.WaitSecondsMax.ShouldBe(5);
        result.Options.TypeCharCap.ShouldBe(2000);
        result.Options.TakeoverPx.ShouldBe(24);
        result.Options.SettleMs.ShouldBe(400);
        result.Options.PostActionFrame.ShouldBe(PostActionFrame.Auto);
        result.Options.LongEdgePx.ShouldBe(1280);
        result.Options.MaxImageBytes.ShouldBe(3_145_728);
        result.Options.MaxImagesPerSession.ShouldBe(80);
        result.Options.AuditThumbnails.ShouldBeFalse();
        result.Failures.ShouldBeEmpty();
    }

    [Fact]
    public void UnparsableValueIsReportedByKeyOnly()
    {
        const string garbage = "not-a-number";
        ComputerUseOptionsReadResult result = ComputerUseOptionsReader.Read(name =>
            string.Equals(name, "Cowork__ComputerUse__RatePerMinute", StringComparison.Ordinal) ? garbage : null);
        result.Options.RatePerMinute.ShouldBe(60);
        string joined = string.Join('\n', result.Failures);
        joined.ShouldContain("Cowork:ComputerUse:RatePerMinute");
        joined.ShouldNotContain(garbage);
    }
}
