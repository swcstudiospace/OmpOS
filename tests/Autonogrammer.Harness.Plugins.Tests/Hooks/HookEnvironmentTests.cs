using Autonogrammer.Harness.Plugins.Hooks;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Plugins.Tests.Hooks;

/// <summary>
/// Verifies the scrubbed environment hooks run in: an allow-list instead of the parent environment, the variables Cursor
/// documents, the sessionStart environment and protection of the identity variables.
/// </summary>
public sealed class HookEnvironmentTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static IReadOnlyDictionary<string, string> Build(EffectiveHook hook, HookSession session, bool inherit = false) =>
        HookEnvironment.Build(DispatcherRig.ParentEnvironment, inherit, hook, session, "preToolUse");

    [Fact]
    public void OnlyAllowListedParentVariablesSurviveAndSecretsDoNot()
    {
        var environment = Build(HookFixtures.Hook(), HookFixtures.Session());

        environment["PATH"].ShouldBe("/usr/bin:/bin");
        environment["HOME"].ShouldBe("/home/ada");
        environment["LC_ALL"].ShouldBe("C.UTF-8");
        environment.ContainsKey("ANTHROPIC_API_KEY").ShouldBeFalse();
        environment.ContainsKey("AUTONOGRAMMER_CANARY_SECRET").ShouldBeFalse();
        environment.ContainsKey("AUTONOGRAMMER_HOOKS_TOKEN").ShouldBeFalse();
        environment.Values.ShouldNotContain("sk-canary", StringComparer.Ordinal);
        environment.Values.ShouldNotContain("bridge-token-canary", StringComparer.Ordinal);
    }

    [Fact]
    public void TheDocumentedCursorVariablesAreSetWithTheirAliases()
    {
        var session = new HookSession("conv-7", ["/work/app"])
        {
            CursorVersion = "autonogrammer-0.15.0",
            UserEmail = "ada@example.com",
            TranscriptPath = "/t/x.jsonl",
            IsRemote = true,
        };

        var environment = Build(HookFixtures.Hook(), session);

        environment["CURSOR_PROJECT_DIR"].ShouldBe("/work/app");
        environment["CLAUDE_PROJECT_DIR"].ShouldBe("/work/app");
        environment["AUTONOGRAMMER_PROJECT_DIR"].ShouldBe("/work/app");
        environment["CURSOR_VERSION"].ShouldBe("autonogrammer-0.15.0");
        environment["CURSOR_USER_EMAIL"].ShouldBe("ada@example.com");
        environment["CURSOR_TRANSCRIPT_PATH"].ShouldBe("/t/x.jsonl");
        environment["CURSOR_CODE_REMOTE"].ShouldBe("true");
        environment["AUTONOGRAMMER_HOOK_EVENT"].ShouldBe("preToolUse");
        environment["AUTONOGRAMMER_SESSION_ID"].ShouldBe("conv-7");
        environment.ContainsKey("CURSOR_PLUGIN_ROOT").ShouldBeFalse();
    }

    [Fact]
    public void OptionalCursorVariablesAreAbsentWhenUnknown()
    {
        var environment = Build(HookFixtures.Hook(), HookFixtures.Session());

        environment.ContainsKey("CURSOR_USER_EMAIL").ShouldBeFalse();
        environment.ContainsKey("CURSOR_TRANSCRIPT_PATH").ShouldBeFalse();
        environment.ContainsKey("CURSOR_CODE_REMOTE").ShouldBeFalse();
    }

    [Fact]
    public void PluginHooksGetThePluginRoot()
    {
        var hook = HookFixtures.Hook(scope: HookScope.Plugin, workingDirectory: "/opt/plugin");

        var environment = Build(hook, HookFixtures.Session());

        environment["CURSOR_PLUGIN_ROOT"].ShouldBe("/opt/plugin");
        environment["AUTONOGRAMMER_PLUGIN_ROOT"].ShouldBe("/opt/plugin");
    }

    [Fact]
    public void SessionEnvironmentThenTheHooksOwnEnvironmentOverlayTheBase()
    {
        var session = HookFixtures.Session();
        session.MergeEnvironment(new Dictionary<string, string>(StringComparer.Ordinal) { ["MODE"] = "session", ["ONLY_SESSION"] = "s" });
        var hook = HookFixtures.Hook(env: new Dictionary<string, string>(StringComparer.Ordinal) { ["MODE"] = "hook", ["PATH"] = "/custom" });

        var environment = Build(hook, session);

        environment["MODE"].ShouldBe("hook");
        environment["ONLY_SESSION"].ShouldBe("s");
        environment["PATH"].ShouldBe("/custom");
    }

    [Theory]
    [InlineData("AUTONOGRAMMER_PROJECT_DIR")]
    [InlineData("AUTONOGRAMMER_HOOKS_TOKEN")]
    [InlineData("CURSOR_PROJECT_DIR")]
    [InlineData("cursor_version")]
    [InlineData("CLAUDE_PROJECT_DIR")]
    public void HookAndSessionEnvironmentCannotForgeIdentityVariables(string name)
    {
        var session = HookFixtures.Session();
        session.MergeEnvironment(new Dictionary<string, string>(StringComparer.Ordinal) { [name] = "forged" });
        var hook = HookFixtures.Hook(env: new Dictionary<string, string>(StringComparer.Ordinal) { [name] = "forged" });

        var environment = Build(hook, session);

        environment.Values.ShouldNotContain("forged", StringComparer.Ordinal);
        HookEnvironment.IsReserved(name).ShouldBeTrue();
    }

    [Fact]
    public void InheritingTheEnvironmentKeepsOrdinaryVariablesButNeverTheAutonogrammerOnes()
    {
        var environment = Build(HookFixtures.Hook(), HookFixtures.Session(), inherit: true);

        environment["ANTHROPIC_API_KEY"].ShouldBe("sk-canary");
        environment.ContainsKey("AUTONOGRAMMER_HOOKS_TOKEN").ShouldBeFalse();
        environment.ContainsKey("AUTONOGRAMMER_CANARY_SECRET").ShouldBeFalse();
    }

    [Fact]
    public async Task AHooksProcessIsNeverHandedTheParentSecretsOrTheBridgeToken()
    {
        var runner = new ScriptedCommandRunner();
        using var rig = await DispatcherRig.CreateAsync([HookFixtures.Hook(HookEvent.PreToolUse)], Token, runner);

        await rig.DispatchAsync(HookEvent.PreToolUse, "{}", Token);

        var environment = runner.Invocations.Single().Environment;
        environment.ContainsKey("ANTHROPIC_API_KEY").ShouldBeFalse();
        environment.ContainsKey("AUTONOGRAMMER_CANARY_SECRET").ShouldBeFalse();
        environment.ContainsKey("AUTONOGRAMMER_HOOKS_TOKEN").ShouldBeFalse();
        environment["CURSOR_PROJECT_DIR"].ShouldBe("/work/project");
    }

    [Fact]
    public async Task ASessionStartHooksEnvironmentReachesEveryLaterHookOfTheSession()
    {
        var start = HookFixtures.Hook(HookEvent.SessionStart, id: "start", command: "./start.sh");
        var later = HookFixtures.Hook(HookEvent.BeforeShellExecution, id: "later", command: "./later.sh");
        var runner = new ScriptedCommandRunner((invocation, _) => Task.FromResult(
            string.Equals(invocation.Command, "./start.sh", StringComparison.Ordinal)
                ? ScriptedCommandRunner.Exit(0, """{ "env": { "PROJECT_MODE": "strict", "AUTONOGRAMMER_SESSION_ID": "forged" } }""")
                : ScriptedCommandRunner.Exit(0, """{ "permission": "allow" }""")));
        using var rig = await DispatcherRig.CreateAsync([start, later], Token, runner);

        await rig.DispatchAsync(HookEvent.SessionStart, """{ "session_id": "conv-1" }""", Token);
        await rig.DispatchAsync(HookEvent.BeforeShellExecution, """{ "command": "ls" }""", Token);

        var environment = runner.Invocations[1].Environment;
        environment["PROJECT_MODE"].ShouldBe("strict");
        environment["AUTONOGRAMMER_SESSION_ID"].ShouldBe("conv-1");
        rig.Session.SessionEnvironment["PROJECT_MODE"].ShouldBe("strict");
    }

    [Fact]
    public async Task TheWorkingDirectoryAndShellFormReachTheRunner()
    {
        var hook = HookFixtures.Hook(HookEvent.PreToolUse, command: "./check.sh --strict", workingDirectory: "/work/project");
        var args = HookFixtures.Hook(HookEvent.PostToolUse, id: "args", command: null, args: ["fmt", "--write"]);
        var runner = new ScriptedCommandRunner();
        using var rig = await DispatcherRig.CreateAsync([hook, args], Token, runner);

        await rig.DispatchAsync(HookEvent.PreToolUse, "{}", Token);
        await rig.DispatchAsync(HookEvent.PostToolUse, "{}", Token);

        runner.Invocations[0].Command.ShouldBe("./check.sh --strict");
        runner.Invocations[0].Shell.ShouldBe(HookShell.PosixSh);
        runner.Invocations[0].WorkingDirectory.ShouldBe("/work/project");
        runner.Invocations[0].MaxStandardOutputBytes.ShouldBe(new HookDispatcherOptions().MaxStandardOutputBytes);
        runner.Invocations[1].Shell.ShouldBe(HookShell.None);
        runner.Invocations[1].Args!.ShouldBe(["fmt", "--write"], ignoreOrder: false);
        runner.Invocations[1].Command.ShouldBeNull();
    }
}
