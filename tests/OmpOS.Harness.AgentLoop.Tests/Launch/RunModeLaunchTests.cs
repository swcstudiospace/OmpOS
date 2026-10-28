using OmpOS.Harness.AgentLoop.Launch;
using OmpOS.Harness.AgentLoop.Safety;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.AgentLoop.Tests.Launch;

/// <summary>The planner passes omp its approval mode and the shell deny rules, and passes nothing when no run mode is given.</summary>
public sealed class RunModeLaunchTests
{
    private static AgentLoopSettings Settings() => new() { PluginsRoot = PlannerFixtures.PluginsRoot, StateRoot = PlannerFixtures.StateRoot };

    private static OmpLaunchPlan Plan(RunModeLaunch? runMode, string? bashOverlay = null, IReadOnlyList<string>? directories = null) =>
        OmpLaunchPlanner.Plan(Settings(), PlannerFixtures.AllReady(), "s1", "/work/a", null, additionalDirectories: directories, runMode: runMode, bashOverlayYaml: bashOverlay);

    [Fact]
    public void WithoutARunModeNoApprovalArgumentIsPassedAndNoOverlayIsPlanned()
    {
        var plan = Plan(runMode: null);

        plan.ExtraArguments.Contains("--approval-mode", StringComparer.Ordinal).ShouldBeFalse();
        plan.ConfigOverlayPath.ShouldBeNull();
    }

    [Theory]
    [InlineData(RunMode.AutoReview, "always-ask-cached")]
    [InlineData(RunMode.Allowlist, "always-ask-cached")]
    [InlineData(RunMode.RunEverything, "yolo")]
    public void TheApprovalModeFollowsTheRunMode(RunMode mode, string expected)
    {
        var plan = Plan(RunModeLaunchMapper.Map(mode));

        var index = plan.ExtraArguments.ToList().IndexOf("--approval-mode");
        index.ShouldBeGreaterThanOrEqualTo(0);
        plan.ExtraArguments[index + 1].ShouldBe(expected);
    }

    [Fact]
    public void ReadGatingFilesSwitchTheModeToTheStrictOne()
    {
        var plan = Plan(RunModeLaunchMapper.Map(RunMode.AutoReview, readsNeedGate: false));

        plan.ExtraArguments.ToList().SkipWhile(static argument => !string.Equals(argument, "--approval-mode", StringComparison.Ordinal)).Skip(1).First().ShouldBe("always-ask");
    }

    [Fact]
    public void ABashOverlayPlansTheOverlayFileAndItsConfigArgument()
    {
        var plan = Plan(RunModeLaunchMapper.Map(RunMode.RunEverything), "bash:\n  patterns:\n    - pattern: \"rm *\"\n");

        plan.ConfigOverlayPath.ShouldBe(SessionStateLayout.ConfigOverlayFile(SessionStateLayout.SessionDirectory(PlannerFixtures.StateRoot, "s1")));
        var arguments = plan.ExtraArguments.ToList();
        arguments[arguments.IndexOf("--config") + 1].ShouldBe(plan.ConfigOverlayPath);
    }

    [Fact]
    public void AddedDirectoriesStayAfterEveryOtherArgument()
    {
        var plan = Plan(RunModeLaunchMapper.Map(RunMode.AutoReview), "bash:\n  patterns: []\n", ["/work/b"]);

        plan.ExtraArguments.ToList().IndexOf("--add-dir").ShouldBeGreaterThan(plan.ExtraArguments.ToList().IndexOf("--config"));
    }

    [Fact]
    public void TheOverlayCarriesTheBashBlockBesideTheProjectMcpSwitch()
    {
        var settings = Settings() with { DisableProjectMcpConfig = true };

        var text = ConfigOverlayWriter.Render(settings, null, "bash:\n  patterns:\n    - pattern: \"rm *\"\n");

        text.ShouldBe("mcp:\n  enableProjectConfig: false\nbash:\n  patterns:\n    - pattern: \"rm *\"\n");
    }
}
