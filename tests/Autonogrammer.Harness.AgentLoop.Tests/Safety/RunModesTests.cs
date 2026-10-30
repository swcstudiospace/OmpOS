using Autonogrammer.Harness.AgentLoop.Safety;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.AgentLoop.Tests.Safety;

/// <summary>The names of the run modes in files.</summary>
public sealed class RunModesTests
{
    [Theory]
    [InlineData("auto-review", RunMode.AutoReview)]
    [InlineData("Auto-Review", RunMode.AutoReview)]
    [InlineData("auto_review", RunMode.AutoReview)]
    [InlineData("autoreview", RunMode.AutoReview)]
    [InlineData(" allowlist ", RunMode.Allowlist)]
    [InlineData("unrestricted", RunMode.RunEverything)]
    [InlineData("run-everything", RunMode.RunEverything)]
    [InlineData("Run Everything", RunMode.RunEverything)]
    public void ANameIsParsed(string text, RunMode expected)
    {
        RunModes.TryParse(text, out var mode).ShouldBeTrue();
        mode.ShouldBe(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("yolo")]
    [InlineData("ask")]
    public void AnUnknownNameIsRefusedAndTheDefaultIsReturned(string? text)
    {
        RunModes.TryParse(text, out var mode).ShouldBeFalse();
        mode.ShouldBe(RunModes.Default);
        RunModes.Default.ShouldBe(RunMode.AutoReview);
    }

    [Theory]
    [InlineData(RunMode.AutoReview, "auto-review")]
    [InlineData(RunMode.Allowlist, "allowlist")]
    [InlineData(RunMode.RunEverything, "unrestricted")]
    public void EveryModeRoundTripsThroughItsWireName(RunMode mode, string name)
    {
        RunModes.WireName(mode).ShouldBe(name);
        RunModes.TryParse(name, out var parsed).ShouldBeTrue();
        parsed.ShouldBe(mode);
        Should.Throw<ArgumentOutOfRangeException>(() => RunModes.WireName((RunMode)5));
    }
}
