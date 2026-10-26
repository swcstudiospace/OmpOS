using OmpOS.Contracts.Agents;
using OmpOS.Harness.AgentLoop.Launch;
using OmpOS.Harness.AgentLoop.Plugins;
using OmpOS.Harness.AgentLoop.Tests.Plugins;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.AgentLoop.Tests.Launch;

/// <summary>
/// Verifies the launch of an interactive omp derived from a plan: the exact argument array and environment, where the resume
/// reference and the first prompt go, that a plugin the planner left out stays out with its planning event, that the omp
/// <c>acp</c> subcommand never appears, and that the plan itself is not changed.
/// </summary>
public sealed class OmpTerminalCommandTests
{
    private const string SessionDir = "/state/sessions/s1";
    private const string Token = "token-token-token-token";
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

    private static readonly string[] AllPluginArguments =
    [
        "--no-extensions",
        "-e", "/p/claude-ultrathink",
        "-e", "/p/agent-swarm/omp",
        "-e", "/p/agent-substrate/packages/projector/omp/substrate-status.ts",
        "-e", "/p/agent-substrate/packages/projector/omp/substrate-skills.ts",
        "-e", "/p/gsd-core-omp",
    ];

    private static AgentLoopSettings Settings() => new()
    {
        PluginsRoot = PlannerFixtures.PluginsRoot,
        StateRoot = PlannerFixtures.StateRoot,
        SubstrateEnabled = true,
        SubstrateEndpoint = new Uri("http://127.0.0.1:7410"),
        SubstrateToken = Token,
    };

    private static OmpLaunchPlan Plan(AgentLoopSettings? settings = null, IReadOnlyList<ResolvedPlugin>? plugins = null) =>
        OmpLaunchPlanner.Plan(
            settings ?? Settings(),
            plugins ?? PlannerFixtures.AllReady(),
            "s1",
            "/work/space",
            "ut-1-abc",
            new FixedTimeProvider(Now));

    [Fact]
    public void WithoutOptionsTheArgumentsAreExactlyThePluginArgumentsOfTheAcpPlan()
    {
        var plan = Plan();

        var command = plan.ForTerminal();

        command.Arguments.ShouldBe(AllPluginArguments);
        command.Arguments.ShouldBe(plan.ExtraArguments);
        command.Arguments.ShouldNotContain("acp", StringComparer.Ordinal);
        command.Arguments.ShouldNotContain("--plugin-dir", StringComparer.Ordinal);
        command.Arguments.ShouldNotContain("--resume", StringComparer.Ordinal);
        command.Arguments.ShouldNotContain("--continue", StringComparer.Ordinal);
        command.Arguments.ShouldNotContain("-c", StringComparer.Ordinal);
    }

    [Fact]
    public void NoEnvironmentValueIsEmptyBecauseThePtyBackendWouldRemoveTheVariable()
    {
        var command = Plan().ForTerminal();

        command.Environment.ShouldAllBe(pair => pair.Value.Length > 0);
        command.Environment["SWARM_GSD_STATE_FILE"].ShouldBe(SessionDir + "/swarm/gsd-state.md");
    }

    [Fact]
    public void EnvironmentAndWorkingDirectoryAreThoseOfThePlanIncludingTheSteerAndConnectionVariables()
    {
        var plan = Plan();

        var command = plan.ForTerminal();

        command.Environment.ShouldBe(plan.Environment);
        command.Environment["OMPOS_STEER_DIR"].ShouldBe(SessionDir + "/steer");
        command.Environment["OMPOS_CONNECTION_FILE"].ShouldBe(SessionDir + "/connection/report.json");
        command.Environment["ULTRATHINK_STATE_DIR"].ShouldBe(SessionDir + "/ultrathink");
        command.WorkingDirectory.ShouldBe("/work/space");
    }

    [Fact]
    public void AResumeReferenceFollowsThePluginArgumentsAsResumeAndNeverContinue()
    {
        var command = Plan().ForTerminal(new OmpTerminalOptions { NativeSessionId = "0198f4b2-7c1a" });

        command.Arguments.ShouldBe([.. AllPluginArguments, "--resume", "0198f4b2-7c1a"]);
    }

    [Fact]
    public void TheInitialPromptIsTheLastPositionalArgument()
    {
        var command = Plan().ForTerminal(new OmpTerminalOptions { InitialPrompt = "List all .ts files in src/" });

        command.Arguments.ShouldBe([.. AllPluginArguments, "List all .ts files in src/"]);
    }

    [Fact]
    public void ResumeComesBeforeThePromptAndTheOverlayArgumentsStayInPlace()
    {
        var plan = Plan(Settings() with { DisableProjectMcpConfig = true });

        var command = plan.ForTerminal(new OmpTerminalOptions { NativeSessionId = "native-1", InitialPrompt = "continue the refactor" });

        command.Arguments.ShouldBe(
        [
            .. AllPluginArguments,
            "--config", SessionDir + "/omp-overlay.yml",
            "--resume", "native-1",
            "continue the refactor",
        ]);
        command.ConfigOverlayPath.ShouldBe(SessionDir + "/omp-overlay.yml");
    }

    [Fact]
    public void ABlankPromptAndANullResumeAddNothing()
    {
        var command = Plan().ForTerminal(new OmpTerminalOptions { NativeSessionId = null, InitialPrompt = "   " });

        command.Arguments.ShouldBe(AllPluginArguments);
    }

    [Fact]
    public void ThePluginsKeepCompositionOrderWhateverOrderThePlannerWasGiven()
    {
        var command = Plan(plugins: PlannerFixtures.AllReady().Reverse().ToList()).ForTerminal();

        command.Arguments.ShouldBe(AllPluginArguments);
    }

    [Fact]
    public void APluginThatIsNotReadyIsOmittedAndItsPlanningEventIsCarried()
    {
        var fix = "git submodule update --init --recursive plugins/agent-swarm";
        var plugins = OmpPluginCatalog.BuiltIn
            .Select(plugin => string.Equals(plugin.Id, "swarm", StringComparison.Ordinal)
                ? PlannerFixtures.Broken("swarm", PluginHealth.SubmoduleNotInitialized, "Not initialized. Run: " + fix)
                : PlannerFixtures.Ready(plugin.Id))
            .ToList();

        var command = Plan(plugins: plugins).ForTerminal(new OmpTerminalOptions { InitialPrompt = "hello" });

        command.Arguments.ShouldBe(
        [
            "--no-extensions",
            "-e", "/p/claude-ultrathink",
            "-e", "/p/agent-substrate/packages/projector/omp/substrate-status.ts",
            "-e", "/p/agent-substrate/packages/projector/omp/substrate-skills.ts",
            "-e", "/p/gsd-core-omp",
            "hello",
        ]);
        var stageEvent = command.PlanningEvents.ShouldHaveSingleItem();
        stageEvent.Source.ShouldBe(StageSource.Swarm);
        stageEvent.Status.ShouldBe(StageStatus.Unavailable);
        stageEvent.Detail.ShouldNotBeNull().ShouldContain(fix, Case.Sensitive);
    }

    [Fact]
    public void ADisabledPluginIsNotLoadedInATerminalEither()
    {
        var command = Plan(Settings() with { UltrathinkEnabled = false }).ForTerminal();

        command.Arguments.ShouldNotContain("/p/claude-ultrathink", StringComparer.Ordinal);
        command.Environment["ULTRATHINK_UPLIFT"].ShouldBe("0");
        command.PlanningEvents.ShouldHaveSingleItem().Status.ShouldBe(StageStatus.Skipped);
    }

    [Fact]
    public void NoMcpServerOrTokenReachesTheTerminalCommand()
    {
        var plan = Plan();
        plan.McpServers.ShouldHaveSingleItem();

        var command = plan.ForTerminal(new OmpTerminalOptions { NativeSessionId = "native-1" });

        command.Arguments.ShouldNotContain(argument => argument.Contains("/mcp", StringComparison.Ordinal));
        command.Arguments.ShouldNotContain(argument => argument.Contains(Token, StringComparison.Ordinal));
        command.Environment.Values.ShouldNotContain(value => value.Contains(Token, StringComparison.Ordinal));
    }

    [Fact]
    public void ForTerminalDoesNotChangeThePlan()
    {
        var plan = Plan();
        var before = plan.ExtraArguments.ToArray();

        _ = plan.ForTerminal(new OmpTerminalOptions { NativeSessionId = "native-1", InitialPrompt = "go" });

        plan.ExtraArguments.ShouldBe(before);
    }

    [Fact]
    public void TheDescriptionNamesNeitherTheResumeReferenceNorThePrompt()
    {
        var command = Plan().ForTerminal(new OmpTerminalOptions { NativeSessionId = "native-secret-id", InitialPrompt = "private prompt text" });

        var text = command.ToString();

        text.ShouldBe(
            "OmpTerminalCommand { Arguments = 14, Environment = [OMPOS_CONNECTION_FILE, OMPOS_STEER_DIR, SUBSTRATE_OMP_PENDING_DIR, SUBSTRATE_URL, " +
            "SWARM_CLAUDE_STATE_DIR, SWARM_DIR, SWARM_GSD_STATE_FILE, ULTRATHINK_STATE_DIR] }");
        text.ShouldNotContain("native-secret-id", Case.Sensitive);
        text.ShouldNotContain("private prompt text", Case.Sensitive);
    }

    [Theory]
    [InlineData("-x")]
    [InlineData("--model")]
    [InlineData(" ")]
    [InlineData("")]
    [InlineData("a\0b")]
    public void AnUnsafeResumeReferenceIsRejected(string resume)
    {
        var plan = Plan();

        Should.Throw<ArgumentException>(() => plan.ForTerminal(new OmpTerminalOptions { NativeSessionId = resume }));
    }

    [Theory]
    [InlineData("--goal x")]
    [InlineData("-p")]
    [InlineData("@prompt.md")]
    [InlineData("a\0b")]
    public void APromptThatOmpWouldReadAsAFlagOrAFileIsRejected(string prompt)
    {
        var plan = Plan();

        Should.Throw<ArgumentException>(() => plan.ForTerminal(new OmpTerminalOptions { InitialPrompt = prompt }));
    }

    [Fact]
    public void ThePlanListsExactlyTheLoadedPluginsInCompositionOrder()
    {
        Plan().LoadedPluginIds.ShouldBe(["ultrathink", "swarm", "substrate", "gsd"]);
        Plan(plugins: PlannerFixtures.AllReady().Reverse().ToList()).LoadedPluginIds.ShouldBe(["ultrathink", "swarm", "substrate", "gsd"]);
        Plan(Settings() with { SwarmEnabled = false, SubstrateReachable = false }).LoadedPluginIds.ShouldBe(["ultrathink", "gsd"]);
    }
}
