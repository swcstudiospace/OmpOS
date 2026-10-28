using OmpOS.Contracts.Agents;
using OmpOS.Harness.AgentLoop.Hooks;
using OmpOS.Harness.AgentLoop.Launch;
using OmpOS.Harness.AgentLoop.Plugins;
using OmpOS.Harness.AgentLoop.Tests.Plugins;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.AgentLoop.Tests.Launch;

/// <summary>The hook bridge in the launch plan: where its <c>-e</c> goes, which variables the child gets, what a missing extension does, and that a terminal launch carries the same wiring.</summary>
public sealed class HookBridgeLaunchTests
{
    private const string Token = "hook-token-very-secret";
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 9, 0, 0, TimeSpan.Zero);

    private static readonly Dictionary<string, string> BridgeEnvironment = new(StringComparer.Ordinal)
    {
        [HookBridgeEnvironment.Url] = "http://127.0.0.1:49152/v1/hook",
        [HookBridgeEnvironment.Token] = Token,
        [HookBridgeEnvironment.StartupFile] = "/state/sessions/s1/hooks/startup.json",
    };

    private static AgentLoopSettings Settings() => new()
    {
        PluginsRoot = PlannerFixtures.PluginsRoot,
        StateRoot = PlannerFixtures.StateRoot,
        UltrathinkEnabled = true,
        SwarmEnabled = true,
        GsdEnabled = true,
    };

    private static ResolvedPlugin ReadyBridge() => new()
    {
        Descriptor = HookBridgePlugin.Descriptor,
        AbsoluteDirectory = "/p/ompos-hooks-omp",
        EntryPaths = ["/p/ompos-hooks-omp"],
        Health = PluginHealth.Ready,
        Message = "Hooks bridge is ready.",
    };

    private static ResolvedPlugin MissingBridge() => new()
    {
        Descriptor = HookBridgePlugin.Descriptor,
        AbsoluteDirectory = "/p/ompos-hooks-omp",
        EntryPaths = [],
        Health = PluginHealth.EntryMissing,
        Message = "Hooks bridge is not usable: the plugin directory '/p/ompos-hooks-omp' does not exist.",
    };

    private static OmpLaunchPlan Plan(HookBridgeEndpoint? bridge, IReadOnlyList<string>? additionalDirectories = null) =>
        OmpLaunchPlanner.Plan(
            Settings(),
            PlannerFixtures.AllReady(),
            "s1",
            "/work/space",
            null,
            new FixedTimeProvider(Now),
            additionalDirectories,
            bridge);

    /// <summary>The extension is loaded after every other plugin, and the child gets the three variables.</summary>
    [Fact]
    public void ABridgeIsLoadedAfterThePluginsAndGetsItsEnvironment()
    {
        var plan = Plan(new HookBridgeEndpoint(BridgeEnvironment, ReadyBridge()));

        plan.HooksBridgeLoaded.ShouldBeTrue();
        plan.ExtraArguments.ShouldBe(
        [
            "--no-extensions",
            "-e", "/p/claude-ultrathink",
            "-e", "/p/agent-swarm/omp",
            "-e", "/p/gsd-core-omp",
            "-e", "/p/ompos-hooks-omp",
        ]);
        plan.Environment[HookBridgeEnvironment.Url].ShouldBe("http://127.0.0.1:49152/v1/hook");
        plan.Environment[HookBridgeEnvironment.Token].ShouldBe(Token);
        plan.Environment[HookBridgeEnvironment.StartupFile].ShouldBe("/state/sessions/s1/hooks/startup.json");
        plan.PlanningEvents.Where(static planning => planning.Source != StageSource.Substrate).ShouldBeEmpty("Substrate is off in these settings and says so; the bridge adds no planning event of its own.");
    }

    /// <summary>Without a bridge the plan is what it was: no argument, no variable.</summary>
    [Fact]
    public void WithoutABridgeNothingChanges()
    {
        var plan = Plan(null);

        plan.HooksBridgeLoaded.ShouldBeFalse();
        plan.ExtraArguments.ShouldNotContain("/p/ompos-hooks-omp", StringComparer.Ordinal);
        plan.Environment.Keys.ShouldNotContain(HookBridgeEnvironment.Url, StringComparer.Ordinal);
        plan.Environment.Keys.ShouldNotContain(HookBridgeEnvironment.Token, StringComparer.Ordinal);
        plan.Environment.Keys.ShouldNotContain(HookBridgeEnvironment.StartupFile, StringComparer.Ordinal);
    }

    /// <summary>A missing extension degrades the plan like any plugin: no <c>-e</c>, no variable (so the token never reaches a child that cannot use it), and a planning event that says what is lost.</summary>
    [Fact]
    public void AMissingExtensionLeavesOutTheArgumentAndTheVariablesAndSaysSo()
    {
        var plan = Plan(new HookBridgeEndpoint(BridgeEnvironment, MissingBridge()));

        plan.HooksBridgeLoaded.ShouldBeFalse();
        plan.ExtraArguments.ShouldNotContain("/p/ompos-hooks-omp", StringComparer.Ordinal);
        plan.Environment.Keys.ShouldNotContain(HookBridgeEnvironment.Token, StringComparer.Ordinal);
        var degraded = plan.PlanningEvents.Where(static planning => planning.Source != StageSource.Substrate).ShouldHaveSingleItem();
        degraded.Status.ShouldBe(StageStatus.Degraded);
        degraded.Source.ShouldBe(StageSource.Omp);
        degraded.Detail.ShouldNotBeNull().ShouldContain("does not exist");
        degraded.Detail.ShouldContain("cannot block or observe");
    }

    /// <summary>The extension's <c>-e</c> comes before <c>--add-dir</c>, which stays the last arguments of the plan.</summary>
    [Fact]
    public void TheExtensionComesBeforeTheAdditionalDirectories()
    {
        var plan = Plan(new HookBridgeEndpoint(BridgeEnvironment, ReadyBridge()), ["/work/other"]);

        plan.ExtraArguments.TakeLast(4).ShouldBe(["-e", "/p/ompos-hooks-omp", "--add-dir", "/work/other"]);
    }

    /// <summary>A terminal session gets the same extension and variables as the chat engine, and never the word acp.</summary>
    [Fact]
    public void ATerminalLaunchCarriesTheSameWiring()
    {
        var command = Plan(new HookBridgeEndpoint(BridgeEnvironment, ReadyBridge())).ForTerminal();

        command.Arguments.TakeLast(2).ShouldBe(["-e", "/p/ompos-hooks-omp"]);
        command.Arguments.ShouldNotContain("acp", StringComparer.Ordinal);
        command.Environment[HookBridgeEnvironment.Url].ShouldBe("http://127.0.0.1:49152/v1/hook");
        command.Environment[HookBridgeEnvironment.StartupFile].ShouldBe("/state/sessions/s1/hooks/startup.json");
    }

    /// <summary>The description of an endpoint and of a plan never shows the token.</summary>
    [Fact]
    public void NeitherTheEndpointNorThePlanPrintsTheToken()
    {
        var endpoint = new HookBridgeEndpoint(BridgeEnvironment, ReadyBridge());

        endpoint.ToString().ShouldNotContain(Token, Case.Sensitive);
        Plan(endpoint).ToString().ShouldNotContain(Token, Case.Sensitive);
    }

    /// <summary>An environment without the URL or the token is not an endpoint.</summary>
    [Theory]
    [InlineData("OMPOS_HOOKS_URL")]
    [InlineData("OMPOS_HOOKS_TOKEN")]
    public void AnEnvironmentWithoutTheUrlOrTheTokenIsRefused(string missing)
    {
        var environment = new Dictionary<string, string>(BridgeEnvironment, StringComparer.Ordinal);
        environment.Remove(missing);

        Should.Throw<ArgumentException>(() => new HookBridgeEndpoint(environment, ReadyBridge()));
    }

    /// <summary>The session's startup file and run log live in the hooks directory of the session state.</summary>
    [Fact]
    public void TheLayoutPutsTheHooksFilesInTheSessionDirectory()
    {
        var directory = SessionStateLayout.SessionDirectory("/state", "s1");

        SessionStateLayout.HooksDirectory(directory).ShouldBe(Path.Combine(directory, "hooks"));
        SessionStateLayout.HooksStartupFile(directory).ShouldBe(Path.Combine(directory, "hooks", "startup.json"));
        SessionStateLayout.HooksRunLogFile(directory).ShouldBe(Path.Combine(directory, "hooks", "runs.jsonl"));
    }
}
