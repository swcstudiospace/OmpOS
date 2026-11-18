using Autonogrammer.Harness.AgentLoop.Safety;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.AgentLoop.Tests.Safety;

/// <summary>Command lines that hid a denied command from the deny reading, or a glob that reached a denied file from the known-safe list.</summary>
public sealed class ShellBypassRegressionTests
{
    [Theory]
    [InlineData("echo \"$(true)\"; rm -rf build")]
    [InlineData("echo \"$(true)\" && rm -rf build")]
    [InlineData("echo \"`true`\" && rm -rf build")]
    [InlineData("echo \"$(echo \"nested\")\" && rm -rf build")]
    public void ADenyRuleSeesACommandThatFollowsAQuotedSubstitution(string command)
    {
        using var rig = new SafetyRig();
        var policy = rig.PolicyWith(RunMode.Allowlist, """{ "permissions": { "deny": ["Shell(rm)"] } }""");

        policy.Decide(RunModeRequest.ForShell(command)).Outcome.ShouldBe(RunModeOutcome.Deny);
    }

    [Theory]
    [InlineData("r\\\nm -rf build")]
    [InlineData("echo ok && r\\\nm -rf build")]
    public void ABackslashNewlineIsALineContinuationThatDeniedCommandsSeeThrough(string command)
    {
        using var rig = new SafetyRig();
        var policy = rig.PolicyWith(RunMode.Allowlist, """{ "permissions": { "deny": ["Shell(rm)"] } }""");

        policy.Decide(RunModeRequest.ForShell(command)).Outcome.ShouldBe(RunModeOutcome.Deny);
    }

    [Fact]
    public void ABackslashNewlineInsideACommandReadsAsOneLineForAllow()
    {
        var reading = ShellCommandAnalysis.ReadForAllow("git status \\\n-s");

        reading.Opaque.ShouldBeFalse();
        reading.Segments.Select(words => string.Join('|', words)).ToArray().ShouldBe(["git|status|-s"]);
    }

    [Theory]
    [InlineData("env -S \"rm -rf build\"")]
    [InlineData("env --split-string=\"rm -rf build\"")]
    [InlineData("env --split-string \"rm -rf build\"")]
    [InlineData("env -iS\"rm -rf build\"")]
    public void ADenyRuleSeesTheCommandAnEnvSplitStringRuns(string command)
    {
        using var rig = new SafetyRig();
        var policy = rig.PolicyWith(RunMode.Allowlist, """{ "permissions": { "deny": ["Shell(rm)"] } }""");

        policy.Decide(RunModeRequest.ForShell(command)).Outcome.ShouldBe(RunModeOutcome.Deny);
    }

    [Theory]
    [InlineData("cat .e*")]
    [InlineData("cat .[e]nv")]
    [InlineData("cat {.env,README.md}")]
    [InlineData("grep --file=.e* README.md")]
    [InlineData("ls *.md")]
    public void AKnownSafeCommandWhoseGlobCouldReachADeniedFileIsNotAllowed(string command)
    {
        using var rig = new SafetyRig();
        var policy = rig.PolicyWith(RunMode.AutoReview, """{ "permissions": { "deny": ["Read(.env*)"] } }""");

        policy.Decide(RunModeRequest.ForShell(command)).Outcome.ShouldBe(RunModeOutcome.Ask);
    }

    [Theory]
    [InlineData("env -u HOME rm -rf build")]
    [InlineData("env -iu HOME rm -rf build")]
    [InlineData("env --unset HOME rm -rf build")]
    [InlineData("env -C /tmp rm -rf build")]
    [InlineData("env --chdir /tmp rm -rf build")]
    [InlineData("timeout -s KILL 5 rm -rf build")]
    [InlineData("timeout -k 1 -s TERM 5 rm -rf build")]
    [InlineData("ionice -c idle rm -rf build")]
    [InlineData("xargs -n 1 rm -rf build")]
    [InlineData("xargs -I {} rm -rf build")]
    [InlineData("stdbuf -o L rm -rf build")]
    [InlineData("sudo -iu root rm -rf build")]
    public void ADenyRuleSeesTheCommandAfterAWrapperOptionThatTakesAValue(string command)
    {
        using var rig = new SafetyRig();
        var policy = rig.PolicyWith(RunMode.Allowlist, """{ "permissions": { "deny": ["Shell(rm)"] } }""");

        policy.Decide(RunModeRequest.ForShell(command)).Outcome.ShouldBe(RunModeOutcome.Deny);
    }
}
