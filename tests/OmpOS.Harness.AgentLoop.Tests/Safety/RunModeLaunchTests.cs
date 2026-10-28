using OmpOS.Harness.AgentLoop.Safety;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.AgentLoop.Tests.Safety;

/// <summary>The mapping from a run mode to omp's approval mode, and the deny patterns omp enforces itself.</summary>
public sealed class RunModeLaunchTests
{
    [Theory]
    [InlineData(RunMode.AutoReview, true, "always-ask-cached", true)]
    [InlineData(RunMode.Allowlist, true, "always-ask-cached", true)]
    [InlineData(RunMode.AutoReview, false, "always-ask", true)]
    [InlineData(RunMode.Allowlist, false, "always-ask", true)]
    [InlineData(RunMode.RunEverything, true, "yolo", false)]
    [InlineData(RunMode.RunEverything, false, "yolo", false)]
    public void AModeMapsToTheOmpApprovalModeItNeeds(RunMode mode, bool readsNeedGate, string approvalMode, bool ompAsks)
    {
        var launch = RunModeLaunchMapper.Map(mode, readsNeedGate);

        launch.Mode.ShouldBe(mode);
        launch.ApprovalMode.ShouldBe(approvalMode);
        launch.OmpAsksOmpOs.ShouldBe(ompAsks);
        launch.Arguments.ShouldBe(["--approval-mode", approvalMode]);
    }

    [Fact]
    public void ADefinedValueIsRequired()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => RunModeLaunchMapper.Map((RunMode)7));
    }

    [Fact]
    public void ReadsNeedTheGateForAWorkspaceBoundaryOrAReadDenyRuleOnly()
    {
        using var rig = new SafetyRig();

        RunModeLaunchMapper.ReadsNeedGate(rig.Permissions(), rig.Sandbox()).ShouldBeFalse();

        rig.WriteProject(".ompos/permissions.json", """{ "permissions": { "deny": ["Shell(rm)", "Write(**/*.key)"], "allow": ["Read(src/**)"] } }""");
        RunModeLaunchMapper.ReadsNeedGate(rig.Permissions(), rig.Sandbox()).ShouldBeFalse("a Shell or Write deny and a Read allow need no read gate");

        rig.WriteProject(".ompos/permissions.json", """{ "permissions": { "deny": ["Read(.env*)"] } }""");
        RunModeLaunchMapper.ReadsNeedGate(rig.Permissions(), rig.Sandbox()).ShouldBeTrue();

        rig.WriteProject(".ompos/permissions.json", "{}");
        rig.WriteProject(".ompos/sandbox.json", """{ "readBoundary": "workspace" }""");
        RunModeLaunchMapper.ReadsNeedGate(rig.Permissions(), rig.Sandbox()).ShouldBeTrue();
    }

    [Fact]
    public void ShellDenyRulesBecomeOmpBashDenyPatterns()
    {
        using var rig = new SafetyRig();
        rig.WriteHome(".ompos/permissions.json", """
            { "permissions": { "deny": ["Shell(rm)", "Shell(git push)", "Shell(curl:*)", "Shell(npm:publish --tag *)", "Read(x)", "Shell(rm)"] } }
            """);

        var patterns = RunModeOverlay.BashDenyPatterns(rig.Permissions());

        patterns.Select(pattern => pattern.Match).ToArray().ShouldBe(["rm", "rm *", "git push", "git push *", "curl", "curl *", "npm publish --tag *"]);
        patterns.ShouldAllBe(pattern => string.Equals(pattern.Approval, "deny", StringComparison.Ordinal));
    }

    [Fact]
    public void TheOverlayIsRenderedAsABashBlockWithQuotedMatches()
    {
        var yaml = RunModeOverlay.RenderYaml([new OmpBashPattern("rm *", "deny"), new OmpBashPattern("echo \"hi\" \\", "deny")]);

        yaml.ShouldBe("bash:\n  patterns:\n    - match: \"rm *\"\n      approval: deny\n    - match: \"echo \\\"hi\\\" \\\\\"\n      approval: deny\n");
        RunModeOverlay.RenderYaml([]).ShouldBeNull();
    }

    [Fact]
    public void NoDenyRuleGivesNoPattern()
    {
        RunModeOverlay.BashDenyPatterns(EffectivePermissions.Empty).ShouldBeEmpty();
        Should.Throw<ArgumentNullException>(() => RunModeOverlay.BashDenyPatterns(null!));
    }
}
