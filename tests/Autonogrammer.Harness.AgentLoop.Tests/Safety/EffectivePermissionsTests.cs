using Autonogrammer.Harness.AgentLoop.Safety;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.AgentLoop.Tests.Safety;

/// <summary>Merging the four permission files: lists join, a deny stands, an untrusted project adds only denies.</summary>
public sealed class EffectivePermissionsTests
{
    [Fact]
    public void ListsFromEveryFileAreJoinedInPrecedenceOrderWithoutDuplicates()
    {
        using var rig = new SafetyRig();
        rig.WriteHome(".cursor/permissions.json", """{ "terminalAllowlist": ["git", "npm"], "mcpAllowlist": ["a:*"] }""");
        rig.WriteHome(".autonogrammer/permissions.json", """{ "terminalAllowlist": ["npm", "pnpm"] }""");
        rig.WriteProject(".cursor/permissions.json", """{ "terminalAllowlist": ["cargo build"] }""");
        rig.WriteProject(".autonogrammer/permissions.json", """{ "mcpAllowlist": ["b:*"] }""");

        var permissions = rig.Permissions();

        permissions.TerminalAllowlist.Select(entry => entry.Text).ToArray().ShouldBe(["git", "npm", "pnpm", "cargo build"]);
        permissions.McpAllowlist.Select(entry => entry.Text).ToArray().ShouldBe(["a:*", "b:*"]);
        permissions.TerminalAllowlist[1].Source.ShouldBe(rig.InHome(".cursor/permissions.json"), "the first file that holds an entry is its source");
        permissions.TerminalAllowlist[3].Scope.ShouldBe(PermissionScope.Project);
        permissions.Files.Count.ShouldBe(4);
        permissions.AllowRules.Select(rule => rule.Token.Text).ToArray().ShouldBe(["Shell(git)", "Shell(npm)", "Mcp(a:*)", "Shell(pnpm)", "Shell(cargo build)", "Mcp(b:*)"]);
    }

    [Fact]
    public void ADenyRuleOfAnyFileStandsAndAProjectAllowNeverRemovesIt()
    {
        using var rig = new SafetyRig();
        rig.WriteHome(".autonogrammer/permissions.json", """{ "permissions": { "deny": ["Shell(rm)"] } }""");
        rig.WriteProject(".autonogrammer/permissions.json", """{ "terminalAllowlist": ["rm"], "permissions": { "allow": ["Shell(rm)"] } }""");

        var permissions = rig.Permissions();

        permissions.DenyRules.Select(rule => rule.Token.Text).ToArray().ShouldBe(["Shell(rm)"]);
        permissions.DenyRules.Single().Scope.ShouldBe(PermissionScope.User);
        permissions.AllowRules.Count.ShouldBe(1, "the allowlist entry and the allow token are the same rule");
        rig.Policy(RunMode.Allowlist).Decide(RunModeRequest.ForShell("rm -rf build")).Outcome.ShouldBe(RunModeOutcome.Deny);
    }

    [Fact]
    public void AnUntrustedProjectAddsDenyRulesAndBlockInstructionsOnly()
    {
        using var rig = new SafetyRig();
        rig.WriteHome(".autonogrammer/permissions.json", """{ "terminalAllowlist": ["git"] }""");
        rig.WriteProject(".autonogrammer/permissions.json", """
            { "terminalAllowlist": ["curl"], "mcpAllowlist": ["x:*"],
              "autoRun": { "allow_instructions": ["allow it"], "block_instructions": ["block it"] },
              "permissions": { "allow": ["Read(/etc/**)"], "deny": ["Shell(sudo)"] } }
            """);

        var permissions = rig.Permissions(trust: false);

        permissions.TerminalAllowlist.Select(entry => entry.Text).ToArray().ShouldBe(["git"]);
        permissions.McpAllowlist.ShouldBeEmpty();
        permissions.AllowInstructions.ShouldBeEmpty();
        permissions.BlockInstructions.Select(entry => entry.Text).ToArray().ShouldBe(["block it"]);
        permissions.AllowRules.Select(rule => rule.Token.Text).ToArray().ShouldBe(["Shell(git)"]);
        permissions.DenyRules.Select(rule => rule.Token.Text).ToArray().ShouldBe(["Shell(sudo)"]);
        permissions.Diagnostics.ShouldContain(problem => problem.Message.Contains("not trusted", StringComparison.Ordinal));
    }

    [Fact]
    public void AnInvalidFileIsIgnoredAndTheOthersStillApply()
    {
        using var rig = new SafetyRig();
        rig.WriteHome(".autonogrammer/permissions.json", "{ broken");
        rig.WriteProject(".autonogrammer/permissions.json", """{ "terminalAllowlist": ["ls"] }""");

        var permissions = rig.Permissions();

        permissions.TerminalAllowlist.Select(entry => entry.Text).ToArray().ShouldBe(["ls"]);
        permissions.Diagnostics.ShouldHaveSingleItem().Severity.ShouldBe(SafetyDiagnosticSeverity.Error);
    }

    [Fact]
    public void NoFilesGiveNoRules()
    {
        using var rig = new SafetyRig();

        var permissions = rig.Permissions();

        permissions.AllowRules.ShouldBeEmpty();
        permissions.DenyRules.ShouldBeEmpty();
        permissions.Files.Count.ShouldBe(4);
        permissions.Files.ShouldAllBe(file => !file.Exists);
        EffectivePermissions.Empty.Files.ShouldBeEmpty();
    }
}
