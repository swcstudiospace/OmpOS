using Autonogrammer.Harness.AgentLoop.Safety;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.AgentLoop.Tests.Safety;

/// <summary>The decision matrix: mode, kind of call, allow and deny rules, read boundary, and the audit record each decision carries.</summary>
public sealed class RunModePolicyTests
{
    private static RunModeDecision Decide(RunModePolicy policy, RunModeRequest request) => policy.Decide(request);

    // ---- shell -------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(RunMode.AutoReview, "git status", RunModeOutcome.Allow, RunModeStage.AllowRule)]
    [InlineData(RunMode.Allowlist, "git status", RunModeOutcome.Allow, RunModeStage.AllowRule)]
    [InlineData(RunMode.AutoReview, "git status -s", RunModeOutcome.Allow, RunModeStage.AllowRule)]
    [InlineData(RunMode.Allowlist, "git push", RunModeOutcome.Ask, RunModeStage.Default)]
    [InlineData(RunMode.AutoReview, "git push", RunModeOutcome.Ask, RunModeStage.Default)]
    [InlineData(RunMode.Allowlist, "git status && git push", RunModeOutcome.Ask, RunModeStage.Default)]
    [InlineData(RunMode.Allowlist, "git status; npm test", RunModeOutcome.Allow, RunModeStage.AllowRule)]
    [InlineData(RunMode.Allowlist, "git status > out.txt", RunModeOutcome.Ask, RunModeStage.Default)]
    [InlineData(RunMode.Allowlist, "git status $(rm x)", RunModeOutcome.Ask, RunModeStage.Default)]
    [InlineData(RunMode.Allowlist, "CI=1 npm test", RunModeOutcome.Ask, RunModeStage.Default)]
    [InlineData(RunMode.Allowlist, "gitk", RunModeOutcome.Ask, RunModeStage.Default)]
    [InlineData(RunMode.RunEverything, "git push --force", RunModeOutcome.Allow, RunModeStage.Mode)]
    public void AShellCommandRunsOnlyWhenEverySimpleCommandMatchesAnAllowRule(RunMode mode, string command, RunModeOutcome outcome, RunModeStage stage)
    {
        using var rig = new SafetyRig();
        var policy = rig.PolicyWith(mode, """{ "terminalAllowlist": ["git status", "npm:test"] }""");

        var decision = Decide(policy, RunModeRequest.ForShell(command));

        decision.Outcome.ShouldBe(outcome);
        decision.Stage.ShouldBe(stage);
        decision.Reason.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void AnAllowDecisionNamesTheRuleAndTheFileThatMatched()
    {
        using var rig = new SafetyRig();
        var policy = rig.PolicyWith(RunMode.Allowlist, """{ "terminalAllowlist": ["git"] }""");

        var decision = policy.Decide(RunModeRequest.ForShell("git log"));

        var rule = decision.Rule.ShouldNotBeNull();
        rule.Token.Text.ShouldBe("Shell(git)");
        rule.Source.ShouldBe(rig.InProject(".autonogrammer/permissions.json"));
        decision.Reason.ShouldContain("Shell(git)", Case.Sensitive);
    }

    [Fact]
    public void AShellRequestWithNoCommandAsksEvenWhenEverythingIsAllowed()
    {
        using var rig = new SafetyRig();
        var policy = rig.PolicyWith(RunMode.Allowlist, """{ "permissions": { "allow": ["Shell(*)"] } }""");

        policy.Decide(RunModeRequest.ForShell(null)).Stage.ShouldBe(RunModeStage.UnknownRequest);
        policy.Decide(RunModeRequest.ForShell("   ")).Outcome.ShouldBe(RunModeOutcome.Ask);
    }

    [Theory]
    [InlineData("rm -rf build")]
    [InlineData("sudo rm -rf build")]
    [InlineData("/bin/rm build")]
    [InlineData("echo $(rm build)")]
    [InlineData("bash -c 'rm build'")]
    [InlineData("ls && (rm build)")]
    public void ADenyRuleBeatsAnAllowRuleThroughWrappersAndSubstitutions(string command)
    {
        using var rig = new SafetyRig();
        rig.WriteHome(".autonogrammer/permissions.json", """{ "permissions": { "deny": ["Shell(rm)"] } }""");
        var policy = rig.PolicyWith(RunMode.AutoReview, """{ "terminalAllowlist": ["rm", "sudo", "echo", "bash", "ls", "/bin/rm"] }""");

        var decision = policy.Decide(RunModeRequest.ForShell(command));

        decision.Outcome.ShouldBe(RunModeOutcome.Deny);
        decision.Stage.ShouldBe(RunModeStage.DenyRule);
        decision.Rule.ShouldNotBeNull().Source.ShouldBe(rig.InHome(".autonogrammer/permissions.json"));
    }

    [Theory]
    [InlineData("echo rm", RunModeOutcome.Allow)]
    [InlineData("npm run rm-all", RunModeOutcome.Allow)]
    [InlineData("git push origin main", RunModeOutcome.Deny)]
    [InlineData("git push", RunModeOutcome.Deny)]
    [InlineData("git pull", RunModeOutcome.Allow)]
    public void ADenyRuleMatchesWordsNotSubstrings(string command, RunModeOutcome expected)
    {
        using var rig = new SafetyRig();
        var policy = rig.PolicyWith(RunMode.Allowlist, """{ "terminalAllowlist": ["echo", "npm", "git"], "permissions": { "deny": ["Shell(rm)", "Shell(git push)"] } }""");

        policy.Decide(RunModeRequest.ForShell(command)).Outcome.ShouldBe(expected);
    }

    [Fact]
    public void RunEverythingAllowsAnyCallExceptWhatADenyRuleNames()
    {
        using var rig = new SafetyRig();
        var policy = rig.PolicyWith(RunMode.RunEverything, """{ "permissions": { "deny": ["Shell(curl)", "WebFetch(evil.example)", "Write(**/*.key)"] } }""");

        policy.Decide(RunModeRequest.ForShell("make")).Outcome.ShouldBe(RunModeOutcome.Allow);
        policy.Decide(RunModeRequest.ForShell("curl https://x")).Outcome.ShouldBe(RunModeOutcome.Deny);
        policy.Decide(RunModeRequest.ForFetch("https://evil.example/a")).Outcome.ShouldBe(RunModeOutcome.Deny);
        policy.Decide(RunModeRequest.ForFetch("https://good.example/a")).Outcome.ShouldBe(RunModeOutcome.Allow);
        policy.Decide(RunModeRequest.ForWrite("id.key")).Outcome.ShouldBe(RunModeOutcome.Deny);
        policy.Decide(new RunModeRequest(RunModeToolKind.Other, "think")).Outcome.ShouldBe(RunModeOutcome.Allow);
        policy.Decide(RunModeRequest.ForWrite("/etc/hosts")).Stage.ShouldBe(RunModeStage.Mode);
    }

    // ---- the known-safe list (Auto-review only) ----------------------------------------------------------------

    [Theory]
    [InlineData(RunMode.AutoReview, "ls", RunModeOutcome.Allow)]
    [InlineData(RunMode.Allowlist, "ls", RunModeOutcome.Ask)]
    [InlineData(RunMode.AutoReview, "ls -la src", RunModeOutcome.Allow)]
    [InlineData(RunMode.AutoReview, "git diff HEAD~1", RunModeOutcome.Allow)]
    [InlineData(RunMode.AutoReview, "git status | head -n 5", RunModeOutcome.Allow)]
    [InlineData(RunMode.AutoReview, "grep -rn needle src", RunModeOutcome.Allow)]
    [InlineData(RunMode.AutoReview, "git diff --output=x.patch", RunModeOutcome.Ask)]
    [InlineData(RunMode.AutoReview, "git diff --ext-diff", RunModeOutcome.Ask)]
    [InlineData(RunMode.AutoReview, "git -c core.pager=x log", RunModeOutcome.Ask)]
    [InlineData(RunMode.AutoReview, "git commit -m x", RunModeOutcome.Ask)]
    [InlineData(RunMode.AutoReview, "cat a.txt > b.txt", RunModeOutcome.Ask)]
    [InlineData(RunMode.AutoReview, "ls; rm x", RunModeOutcome.Ask)]
    [InlineData(RunMode.AutoReview, "echo $(id)", RunModeOutcome.Ask)]
    [InlineData(RunMode.AutoReview, "date -s 2020-01-01", RunModeOutcome.Ask)]
    [InlineData(RunMode.AutoReview, "find . -delete", RunModeOutcome.Ask)]
    public void AutoReviewRunsAShortListOfReadOnlyCommandsAndAllowlistDoesNot(RunMode mode, string command, RunModeOutcome expected)
    {
        using var rig = new SafetyRig();

        var decision = rig.Policy(mode).Decide(RunModeRequest.ForShell(command));

        decision.Outcome.ShouldBe(expected);
        if (expected == RunModeOutcome.Allow)
        {
            decision.Stage.ShouldBe(RunModeStage.KnownSafe);
        }
    }

    [Fact]
    public void AKnownSafeCommandMustStayInsideTheReadBoundaryAndOutOfAReadDenyRule()
    {
        using var rig = new SafetyRig();
        File.WriteAllText(Path.Combine(rig.Outside, "secret.txt"), "x");
        var outside = Path.Combine(rig.Outside, "secret.txt");

        var system = rig.PolicyWith(RunMode.AutoReview, """{ "permissions": { "deny": ["Read(.env*)"] } }""");
        system.Decide(RunModeRequest.ForShell("cat " + outside)).Outcome.ShouldBe(RunModeOutcome.Allow, "the system boundary reads anywhere");
        system.Decide(RunModeRequest.ForShell("cat .env.local")).Outcome.ShouldBe(RunModeOutcome.Ask, "a denied file is never known-safe");
        system.Decide(RunModeRequest.ForShell("cat README.md")).Outcome.ShouldBe(RunModeOutcome.Allow);

        var workspace = rig.PolicyWith(RunMode.AutoReview, """{ }""", """{ "readBoundary": "workspace" }""");
        workspace.Decide(RunModeRequest.ForShell("cat " + outside)).Outcome.ShouldBe(RunModeOutcome.Ask);
        workspace.Decide(RunModeRequest.ForShell("cat ../outside/secret.txt")).Outcome.ShouldBe(RunModeOutcome.Ask);
        workspace.Decide(RunModeRequest.ForShell("cat README.md")).Outcome.ShouldBe(RunModeOutcome.Allow);
        workspace.Decide(RunModeRequest.ForShell("ls", rig.Outside)).Outcome.ShouldBe(RunModeOutcome.Ask, "a working directory outside the boundary is not known-safe");
        workspace.Decide(RunModeRequest.ForShell("ls", rig.InProject("src"))).Outcome.ShouldBe(RunModeOutcome.Allow);
    }

    // ---- reads -------------------------------------------------------------------------------------------------

    [Fact]
    public void ReadsFollowTheBoundaryTheReadAllowRulesAndTheDenyRules()
    {
        using var rig = new SafetyRig();
        var outside = Path.Combine(rig.Outside, "notes.txt");
        var policy = rig.PolicyWith(
            RunMode.Allowlist,
            """{ "permissions": { "allow": ["Read(OUTSIDE/**)"], "deny": ["Read(secrets/**)", "Read(**/*.pem)"] } }""".Replace("OUTSIDE", rig.Outside.Replace('\\', '/'), StringComparison.Ordinal),
            """{ "readBoundary": "workspace" }""");

        policy.Decide(RunModeRequest.ForRead("src/a.ts")).Outcome.ShouldBe(RunModeOutcome.Allow);
        policy.Decide(RunModeRequest.ForRead("src/a.ts")).Stage.ShouldBe(RunModeStage.ReadBoundary);
        policy.Decide(RunModeRequest.ForRead("/etc/passwd")).Outcome.ShouldBe(RunModeOutcome.Ask);
        policy.Decide(RunModeRequest.ForRead("/etc/passwd")).Stage.ShouldBe(RunModeStage.ReadBoundary);
        policy.Decide(RunModeRequest.ForRead(outside)).Outcome.ShouldBe(RunModeOutcome.Allow);
        policy.Decide(RunModeRequest.ForRead(outside)).Stage.ShouldBe(RunModeStage.AllowRule);
        policy.Decide(RunModeRequest.ForRead("secrets/key.txt")).Outcome.ShouldBe(RunModeOutcome.Deny);
        policy.Decide(RunModeRequest.ForRead("a/b/c.pem")).Outcome.ShouldBe(RunModeOutcome.Deny);
        policy.Decide(RunModeRequest.ForRead("src/a.ts", "/etc/passwd")).Outcome.ShouldBe(RunModeOutcome.Ask, "one path outside makes the whole call ask");
        policy.Decide(RunModeRequest.ForRead("src/a.ts", "secrets/key.txt")).Outcome.ShouldBe(RunModeOutcome.Deny, "one denied path makes the whole call denied");
    }

    [Fact]
    public void TheSystemBoundaryReadsAnywhereButStillAsksWhenNoFileIsNamed()
    {
        using var rig = new SafetyRig();
        var policy = rig.Policy(RunMode.Allowlist);

        policy.Decide(RunModeRequest.ForRead("/etc/passwd")).Outcome.ShouldBe(RunModeOutcome.Allow);
        policy.Decide(new RunModeRequest(RunModeToolKind.Read, "read")).Stage.ShouldBe(RunModeStage.UnknownRequest);
        policy.Decide(new RunModeRequest(RunModeToolKind.Read, "read", Paths: [])).Outcome.ShouldBe(RunModeOutcome.Ask);
    }

    [Fact]
    public void ASymlinkInsideTheWorkspaceDoesNotLetAReadEscapeTheBoundaryOrAWriteEscapeTheWorkspace()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Creating symbolic links needs a privilege on Windows.");
        using var rig = new SafetyRig();
        SafetyRig.LinkFolder(rig.InProject("link"), rig.Outside);
        var policy = rig.PolicyWith(RunMode.Allowlist, """{ "permissions": { "allow": ["Write(src/**)", "Read(src/**)"] } }""", """{ "readBoundary": "workspace" }""");

        policy.Decide(RunModeRequest.ForRead("link/a.txt")).Outcome.ShouldBe(RunModeOutcome.Ask);
        policy.Decide(RunModeRequest.ForWrite("link/a.txt")).Outcome.ShouldBe(RunModeOutcome.Ask);
        policy.Decide(RunModeRequest.ForWrite("src/a.txt")).Outcome.ShouldBe(RunModeOutcome.Allow);
    }

    [Fact]
    public void AnAllowRuleForAFolderDoesNotFollowALinkOutOfIt()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Creating symbolic links needs a privilege on Windows.");
        using var rig = new SafetyRig();
        Directory.CreateDirectory(rig.InProject("src"));
        SafetyRig.LinkFolder(rig.InProject("src/escape"), rig.Outside);
        var policy = rig.PolicyWith(RunMode.Allowlist, """{ "permissions": { "allow": ["Read(src/**)"] } }""", """{ "readBoundary": "workspace" }""");

        // src/escape/x lexically matches src/**, but its real path is outside both src and the workspace.
        policy.Decide(RunModeRequest.ForRead("src/escape/x.txt")).Outcome.ShouldBe(RunModeOutcome.Ask);
        policy.Decide(RunModeRequest.ForRead("src/real.txt")).Outcome.ShouldBe(RunModeOutcome.Allow);
    }

    [Fact]
    public void ADenyRuleStillSeesAFileThroughALink()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Creating symbolic links needs a privilege on Windows.");
        using var rig = new SafetyRig();
        Directory.CreateDirectory(rig.InProject("secrets"));
        SafetyRig.LinkFolder(rig.InProject("alias"), rig.InProject("secrets"));
        var policy = rig.PolicyWith(RunMode.Allowlist, """{ "permissions": { "deny": ["Read(secrets/**)"] } }""");

        policy.Decide(RunModeRequest.ForRead("alias/key.txt")).Outcome.ShouldBe(RunModeOutcome.Deny);
    }

    // ---- writes ------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("src/a.ts", RunModeOutcome.Allow, RunModeStage.WriteScope)]
    [InlineData("new/dir/file.txt", RunModeOutcome.Allow, RunModeStage.WriteScope)]
    [InlineData("/etc/hosts", RunModeOutcome.Ask, RunModeStage.WriteScope)]
    [InlineData("../outside/a.txt", RunModeOutcome.Ask, RunModeStage.WriteScope)]
    [InlineData(".git/hooks/pre-commit", RunModeOutcome.Ask, RunModeStage.ProtectedPath)]
    [InlineData(".git/config", RunModeOutcome.Ask, RunModeStage.ProtectedPath)]
    [InlineData(".git/HEAD", RunModeOutcome.Allow, RunModeStage.WriteScope)]
    [InlineData(".autonogrammer/permissions.json", RunModeOutcome.Ask, RunModeStage.ProtectedPath)]
    [InlineData(".autonogrammer/sub/hooks.json", RunModeOutcome.Ask, RunModeStage.ProtectedPath)]
    [InlineData(".autonogrammer/rules/style.md", RunModeOutcome.Allow, RunModeStage.WriteScope)]
    [InlineData(".cursor/permissions.json", RunModeOutcome.Ask, RunModeStage.ProtectedPath)]
    [InlineData(".vscode/settings.json", RunModeOutcome.Ask, RunModeStage.ProtectedPath)]
    [InlineData("app.code-workspace", RunModeOutcome.Ask, RunModeStage.ProtectedPath)]
    [InlineData(".cursorignore", RunModeOutcome.Ask, RunModeStage.ProtectedPath)]
    public void AWriteInsideTheWorkspaceRunsUnlessItIsProtectedAndOutsideItAsks(string path, RunModeOutcome outcome, RunModeStage stage)
    {
        using var rig = new SafetyRig();

        var decision = rig.Policy(RunMode.AutoReview).Decide(RunModeRequest.ForWrite(path));

        decision.Outcome.ShouldBe(outcome);
        decision.Stage.ShouldBe(stage);
    }

    [Fact]
    public void AWriteAllowRuleOpensAFolderOutsideTheWorkspaceButNeverAProtectedPath()
    {
        using var rig = new SafetyRig();
        var policy = rig.PolicyWith(
            RunMode.Allowlist,
            """{ "permissions": { "allow": ["Write(OUTSIDE/**)", "Write(.git/hooks/**)", "Write(~/.autonogrammer/permissions.json)"], "deny": ["Write(OUTSIDE/**/*.key)"] } }""".Replace("OUTSIDE", rig.Outside.Replace('\\', '/'), StringComparison.Ordinal));

        policy.Decide(RunModeRequest.ForWrite(Path.Combine(rig.Outside, "a.txt"))).Stage.ShouldBe(RunModeStage.AllowRule);
        policy.Decide(RunModeRequest.ForWrite(Path.Combine(rig.Outside, "a.key"))).Outcome.ShouldBe(RunModeOutcome.Deny);
        policy.Decide(RunModeRequest.ForWrite(".git/hooks/pre-push")).Stage.ShouldBe(RunModeStage.ProtectedPath);
        policy.Decide(RunModeRequest.ForWrite(rig.InHome(".autonogrammer/permissions.json"))).Stage.ShouldBe(RunModeStage.ProtectedPath);
    }

    [Fact]
    public void AWriteWithNoFileAsks()
    {
        using var rig = new SafetyRig();

        rig.Policy(RunMode.Allowlist).Decide(new RunModeRequest(RunModeToolKind.Write, "edit")).Stage.ShouldBe(RunModeStage.UnknownRequest);
    }

    [Fact]
    public void RelativePathsAreResolvedAgainstTheRequestsWorkingDirectory()
    {
        using var rig = new SafetyRig();
        Directory.CreateDirectory(rig.InProject("pkg"));
        var policy = rig.Policy(RunMode.Allowlist);

        policy.Decide(RunModeRequest.ForWrite("../.git/config") with { WorkingDirectory = rig.InProject("pkg") }).Stage.ShouldBe(RunModeStage.ProtectedPath);
        policy.Decide(RunModeRequest.ForWrite("a.txt") with { WorkingDirectory = rig.Outside }).Outcome.ShouldBe(RunModeOutcome.Ask);
    }

    // ---- fetch and MCP -----------------------------------------------------------------------------------------

    [Theory]
    [InlineData("https://docs.example.com/page", RunModeOutcome.Allow)]
    [InlineData("https://a.b.example.com/", RunModeOutcome.Allow)]
    [InlineData("https://example.com/", RunModeOutcome.Ask)]
    [InlineData("https://evilexample.com/", RunModeOutcome.Ask)]
    [InlineData("https://api.github.com/x", RunModeOutcome.Allow)]
    [InlineData("https://tracker.evil.example.com/x", RunModeOutcome.Deny)]
    [InlineData("not a url at all", RunModeOutcome.Ask)]
    [InlineData(null, RunModeOutcome.Ask)]
    public void AFetchFollowsTheWebFetchRulesByHost(string? address, RunModeOutcome expected)
    {
        using var rig = new SafetyRig();
        var policy = rig.PolicyWith(RunMode.AutoReview, """{ "permissions": { "allow": ["WebFetch(*.example.com)", "WebFetch(api.github.com)"], "deny": ["WebFetch(tracker.*)"] } }""");

        policy.Decide(RunModeRequest.ForFetch(address)).Outcome.ShouldBe(expected);
    }

    [Theory]
    [InlineData("github", "list_issues", RunModeOutcome.Allow)]
    [InlineData("GitHub", "LIST_ISSUES", RunModeOutcome.Allow)]
    [InlineData("linear", "list_issues", RunModeOutcome.Allow)]
    [InlineData("linear", "save_issue", RunModeOutcome.Ask)]
    [InlineData("github", "delete_repo", RunModeOutcome.Deny)]
    [InlineData("other", "tool", RunModeOutcome.Ask)]
    [InlineData(null, "tool", RunModeOutcome.Ask)]
    [InlineData("github", null, RunModeOutcome.Ask)]
    public void AnMcpCallFollowsTheMcpRulesAndAMcpDenyBeatsAnAllow(string? server, string? tool, RunModeOutcome expected)
    {
        using var rig = new SafetyRig();
        var policy = rig.PolicyWith(RunMode.Allowlist, """{ "mcpAllowlist": ["github:*", "linear:list_issues"], "permissions": { "deny": ["Mcp(*:delete_*)"] } }""");

        policy.Decide(RunModeRequest.ForMcp(server, tool)).Outcome.ShouldBe(expected);
    }

    [Fact]
    public void ACallOfAnUnknownKindAsksInEveryModeButRunEverything()
    {
        using var rig = new SafetyRig();
        var request = new RunModeRequest(RunModeToolKind.Other, "think");

        rig.Policy(RunMode.AutoReview).Decide(request).Stage.ShouldBe(RunModeStage.UnknownRequest);
        rig.Policy(RunMode.Allowlist).Decide(request).Outcome.ShouldBe(RunModeOutcome.Ask);
        rig.Policy(RunMode.RunEverything).Decide(request).Outcome.ShouldBe(RunModeOutcome.Allow);
    }

    // ---- trust and precedence ----------------------------------------------------------------------------------

    [Fact]
    public void AnUntrustedProjectCannotAllowACommandButCanDenyOne()
    {
        using var rig = new SafetyRig();
        rig.WriteHome(".autonogrammer/permissions.json", """{ "terminalAllowlist": ["git status"] }""");
        rig.WriteProject(".autonogrammer/permissions.json", """{ "terminalAllowlist": ["curl"], "permissions": { "deny": ["Shell(git status)"] } }""");

        rig.Policy(RunMode.Allowlist, trust: true).Decide(RunModeRequest.ForShell("curl https://x")).Outcome.ShouldBe(RunModeOutcome.Allow);
        var untrusted = rig.Policy(RunMode.Allowlist, trust: false);
        untrusted.Decide(RunModeRequest.ForShell("curl https://x")).Outcome.ShouldBe(RunModeOutcome.Ask);
        untrusted.Decide(RunModeRequest.ForShell("git status")).Outcome.ShouldBe(RunModeOutcome.Deny);
    }

    [Fact]
    public void ThePolicyReadsFilesAtConstructionNotAtEveryDecision()
    {
        using var rig = new SafetyRig();
        var policy = rig.PolicyWith(RunMode.Allowlist, """{ "terminalAllowlist": ["ls"] }""");
        rig.WriteProject(".autonogrammer/permissions.json", """{ "terminalAllowlist": [] }""");

        policy.Decide(RunModeRequest.ForShell("ls")).Outcome.ShouldBe(RunModeOutcome.Allow);
        rig.Policy(RunMode.Allowlist).Decide(RunModeRequest.ForShell("ls")).Outcome.ShouldBe(RunModeOutcome.Ask, "a policy built after the change sees it");
    }

    [Fact]
    public void TheConstructorRejectsWhatItCannotUse()
    {
        using var rig = new SafetyRig();

        Should.Throw<ArgumentNullException>(() => new RunModePolicy(RunMode.Allowlist, null!, EffectiveSandbox.Default, rig.Project, null));
        Should.Throw<ArgumentNullException>(() => new RunModePolicy(RunMode.Allowlist, EffectivePermissions.Empty, null!, rig.Project, null));
        Should.Throw<ArgumentException>(() => new RunModePolicy(RunMode.Allowlist, EffectivePermissions.Empty, EffectiveSandbox.Default, " ", null));
        Should.Throw<ArgumentOutOfRangeException>(() => new RunModePolicy((RunMode)9, EffectivePermissions.Empty, EffectiveSandbox.Default, rig.Project, null));
        Should.Throw<ArgumentNullException>(() => new RunModePolicy(RunMode.Allowlist, EffectivePermissions.Empty, EffectiveSandbox.Default, rig.Project, null).Decide(null!));
    }
}
