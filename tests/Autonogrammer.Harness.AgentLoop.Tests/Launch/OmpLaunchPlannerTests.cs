using Autonogrammer.Contracts.Agents;
using Autonogrammer.Harness.AgentLoop.Launch;
using Autonogrammer.Harness.AgentLoop.Plugins;
using Autonogrammer.Harness.AgentLoop.Tests.Plugins;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.AgentLoop.Tests.Launch;

/// <summary>
/// Verifies the launch plan: exact argument and environment tables for each combination of enabled, missing and unhealthy
/// plugins, the substrate rules, secret redaction and that planning creates nothing.
/// </summary>
public sealed class OmpLaunchPlannerTests
{
    private const string Token = "tok-9f3c-very-secret";
    private const string SessionDir = "/state/sessions/s1";
    private static readonly Uri Endpoint = new("http://127.0.0.1:7410");
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

    private static AgentLoopSettings Settings() => new()
    {
        PluginsRoot = PlannerFixtures.PluginsRoot,
        StateRoot = PlannerFixtures.StateRoot,
        UltrathinkEnabled = true,
        SwarmEnabled = true,
        SubstrateEnabled = true,
        GsdEnabled = true,
        SubstrateEndpoint = Endpoint,
        SubstrateToken = Token,
        SubstrateTokensDirectory = "/tokens",
    };

    private static OmpLaunchPlan Plan(AgentLoopSettings settings, IReadOnlyList<ResolvedPlugin>? plugins = null, string? graphId = null) =>
        OmpLaunchPlanner.Plan(
            settings,
            plugins ?? PlannerFixtures.AllReady(),
            "s1",
            "/work/space",
            graphId,
            new FixedTimeProvider(Now));

    private static List<string> Env(OmpLaunchPlan plan) =>
        plan.Environment.Select(pair => pair.Key + "=" + pair.Value).Order(StringComparer.Ordinal).ToList();

    private static readonly string[] UltrathinkArgs = ["-e", "/p/claude-ultrathink"];
    private static readonly string[] SwarmArgs = ["-e", "/p/agent-swarm/omp"];
    private static readonly string[] SubstrateArgs =
    [
        "-e", "/p/agent-substrate/packages/projector/omp/substrate-status.ts",
        "-e", "/p/agent-substrate/packages/projector/omp/substrate-skills.ts",
    ];
    private static readonly string[] GsdArgs = ["-e", "/p/gsd-core-omp"];

    private static string[] Args(params string[][] groups) => ["--no-extensions", .. groups.SelectMany(group => group)];

    private static string[] BaseEnv() =>
    [
        "AUTONOGRAMMER_CONNECTION_FILE=/state/sessions/s1/connection/report.json",
        "AUTONOGRAMMER_STEER_DIR=/state/sessions/s1/steer",
        "SUBSTRATE_OMP_PENDING_DIR=/state/sessions/s1/substrate-pending",
        "SWARM_CLAUDE_STATE_DIR=/state/sessions/s1/claude-state",
        "SWARM_DIR=/state/sessions/s1/swarm",
        "SWARM_GSD_STATE_FILE=/state/sessions/s1/swarm/gsd-state.md",
        "ULTRATHINK_STATE_DIR=/state/sessions/s1/ultrathink",
    ];

    private static string[] SubstrateEnv() =>
    [
        .. BaseEnv(),
        "SUBSTRATE_TOKENS_DIR=/tokens",
        "SUBSTRATE_URL=http://127.0.0.1:7410",
    ];

    [Fact]
    public void AllEnabledPlansTheExactArgvEnvironmentAndSubstrateMcpServer()
    {
        var plan = Plan(Settings());

        plan.ExtraArguments.ShouldBe(Args(UltrathinkArgs, SwarmArgs, SubstrateArgs, GsdArgs));
        Env(plan).ShouldBe(SubstrateEnv().Order(StringComparer.Ordinal));
        plan.McpServers.Count.ShouldBe(1);
        plan.McpServers[0].Name.ShouldBe("substrate");
        plan.McpServers[0].Url.ToString().ShouldBe("http://127.0.0.1:7410/mcp");
        plan.McpServers[0].Headers.Count.ShouldBe(1);
        plan.McpServers[0].Headers["Authorization"].ShouldBe("Bearer " + Token);
        plan.WorkingDirectory.ShouldBe("/work/space");
        plan.ConfigOverlayPath.ShouldBeNull();
        plan.PlanningEvents.ShouldBeEmpty();
    }

    [Fact]
    public void PluginsAreLoadedInCompositionOrderWhateverOrderTheyAreGiven()
    {
        var reversed = PlannerFixtures.AllReady().Reverse().ToList();

        Plan(Settings(), reversed).ExtraArguments.ShouldBe(Args(UltrathinkArgs, SwarmArgs, SubstrateArgs, GsdArgs));
    }

    [Fact]
    public void ArgumentsStartWithNoExtensionsAndNeverUsePluginDir()
    {
        var plan = Plan(Settings());

        plan.ExtraArguments[0].ShouldBe("--no-extensions");
        plan.ExtraArguments.ShouldNotContain("--plugin-dir", StringComparer.Ordinal);
        plan.ExtraArguments.Where(argument => string.Equals(argument, "-e", StringComparison.Ordinal)).Count().ShouldBe(5);
    }

    public static TheoryData<string, string[], string[]> DisabledCases() => new()
    {
        { "ultrathink", Args(SwarmArgs, SubstrateArgs, GsdArgs), [.. SubstrateEnv(), "ULTRATHINK_UPLIFT=0"] },
        { "swarm", Args(UltrathinkArgs, SubstrateArgs, GsdArgs), SubstrateEnv() },
        { "substrate", Args(UltrathinkArgs, SwarmArgs, GsdArgs), BaseEnv() },
        { "gsd", Args(UltrathinkArgs, SwarmArgs, SubstrateArgs), SubstrateEnv() },
    };

    [Theory]
    [MemberData(nameof(DisabledCases))]
    public void ADisabledPluginIsNotLoadedAndIsReportedAsSkipped(string disabledId, string[] expectedArguments, string[] expectedEnvironment)
    {
        var settings = disabledId switch
        {
            "ultrathink" => Settings() with { UltrathinkEnabled = false },
            "swarm" => Settings() with { SwarmEnabled = false },
            "substrate" => Settings() with { SubstrateEnabled = false },
            _ => Settings() with { GsdEnabled = false },
        };

        var plan = Plan(settings);

        plan.ExtraArguments.ShouldBe(expectedArguments);
        Env(plan).ShouldBe(expectedEnvironment.Order(StringComparer.Ordinal));
        plan.McpServers.Count.ShouldBe(string.Equals(disabledId, "substrate", StringComparison.Ordinal) ? 0 : 1);
        plan.PlanningEvents.Count.ShouldBe(1);
        var stageEvent = plan.PlanningEvents[0];
        stageEvent.Status.ShouldBe(StageStatus.Skipped);
        stageEvent.Source.ShouldBe(PlannerFixtures.Descriptor(disabledId).StageSource);
        stageEvent.Detail.ShouldNotBeNull().ShouldContain("switched off");
    }

    public static TheoryData<string, string[], string[]> UninitializedCases() => new()
    {
        { "ultrathink", Args(SwarmArgs, SubstrateArgs, GsdArgs), SubstrateEnv() },
        { "swarm", Args(UltrathinkArgs, SubstrateArgs, GsdArgs), SubstrateEnv() },
        { "substrate", Args(UltrathinkArgs, SwarmArgs, GsdArgs), BaseEnv() },
        { "gsd", Args(UltrathinkArgs, SwarmArgs, SubstrateArgs), SubstrateEnv() },
    };

    [Theory]
    [MemberData(nameof(UninitializedCases))]
    public void APluginThatIsNotInitializedIsOmittedWithAnUnavailableEventAndThePlanIsStillProduced(
        string brokenId,
        string[] expectedArguments,
        string[] expectedEnvironment)
    {
        var fix = "git submodule update --init --recursive plugins/" + PlannerFixtures.Descriptor(brokenId).RelativeDirectory.Split('/')[0];
        var plugins = OmpPluginCatalog.BuiltIn
            .Select(plugin => string.Equals(plugin.Id, brokenId, StringComparison.Ordinal)
                ? PlannerFixtures.Broken(brokenId, PluginHealth.SubmoduleNotInitialized, "Not initialized. Run: " + fix)
                : PlannerFixtures.Ready(plugin.Id))
            .ToList();

        var plan = Plan(Settings(), plugins, "ut-1-abc");

        plan.ExtraArguments.ShouldBe(expectedArguments);
        Env(plan).ShouldBe(expectedEnvironment.Order(StringComparer.Ordinal));
        plan.McpServers.Count.ShouldBe(string.Equals(brokenId, "substrate", StringComparison.Ordinal) ? 0 : 1);
        plan.PlanningEvents.Count.ShouldBe(1);
        var stageEvent = plan.PlanningEvents[0];
        stageEvent.Status.ShouldBe(StageStatus.Unavailable);
        stageEvent.Source.ShouldBe(PlannerFixtures.Descriptor(brokenId).StageSource);
        stageEvent.SessionId.ShouldBe("s1");
        stageEvent.GraphId.ShouldBe("ut-1-abc");
        stageEvent.Timestamp.ShouldBe(Now);
        stageEvent.Detail.ShouldNotBeNull().ShouldContain(fix);
    }

    [Fact]
    public void ARequiredPluginIsDescribedAsNeededAndAnOptionalOneAsOptional()
    {
        var plugins = new List<ResolvedPlugin>
        {
            PlannerFixtures.Broken("swarm", PluginHealth.EntryMissing, "swarm gone."),
            PlannerFixtures.Broken("gsd", PluginHealth.EntryMissing, "gsd gone."),
        };

        var plan = Plan(Settings() with { SubstrateEnabled = false }, plugins);

        plan.ExtraArguments.ShouldBe(["--no-extensions"]);
        plan.PlanningEvents.Select(e => (e.Source, e.Status)).ShouldBe(
            [(StageSource.Swarm, StageStatus.Degraded), (StageSource.Gsd, StageStatus.Degraded)]);
        plan.PlanningEvents[0].Detail.ShouldBe("swarm gone. It is needed for the full agent loop; the session runs without it.");
        plan.PlanningEvents[1].Detail.ShouldBe("gsd gone. It is optional; the session runs without it.");
    }

    [Fact]
    public void AnUnhealthySubstrateIsLeftOutEntirelyAndReportedAsDegraded()
    {
        var settings = Settings() with { SubstrateReachable = false, SubstrateUnreachableReason = "Health check returned HTTP 503." };

        var plan = Plan(settings);

        plan.ExtraArguments.ShouldBe(Args(UltrathinkArgs, SwarmArgs, GsdArgs));
        plan.ExtraArguments.ShouldNotContain(argument => argument.Contains("substrate", StringComparison.Ordinal));
        plan.Environment.ContainsKey("SUBSTRATE_URL").ShouldBeFalse();
        plan.Environment.ContainsKey("SUBSTRATE_TOKENS_DIR").ShouldBeFalse();
        plan.McpServers.ShouldBeEmpty();
        var stageEvent = plan.PlanningEvents.ShouldHaveSingleItem();
        stageEvent.Source.ShouldBe(StageSource.Substrate);
        stageEvent.Status.ShouldBe(StageStatus.Degraded);
        stageEvent.Detail.ShouldNotBeNull().ShouldStartWith("Health check returned HTTP 503.");
    }

    [Fact]
    public void ASubstrateWithoutAnEndpointIsLeftOutBecauseOmpWouldPollTheDefaultPort()
    {
        var plan = Plan(Settings() with { SubstrateEndpoint = null });

        plan.ExtraArguments.ShouldBe(Args(UltrathinkArgs, SwarmArgs, GsdArgs));
        plan.Environment.ContainsKey("SUBSTRATE_URL").ShouldBeFalse();
        plan.McpServers.ShouldBeEmpty();
        plan.PlanningEvents.ShouldHaveSingleItem().Status.ShouldBe(StageStatus.Unavailable);
    }

    [Theory]
    [InlineData("ftp://127.0.0.1:7410")]
    [InlineData("http://user:pass@127.0.0.1:7410")]
    [InlineData("/relative/path")]
    public void AnEndpointThatIsNotAPlainHttpAddressIsLeftOutAndNeverEchoed(string endpoint)
    {
        var plan = Plan(Settings() with { SubstrateEndpoint = new Uri(endpoint, UriKind.RelativeOrAbsolute) });

        plan.McpServers.ShouldBeEmpty();
        plan.Environment.ContainsKey("SUBSTRATE_URL").ShouldBeFalse();
        var stageEvent = plan.PlanningEvents.ShouldHaveSingleItem();
        stageEvent.Status.ShouldBe(StageStatus.Unavailable);
        stageEvent.Detail.ShouldNotBeNull().ShouldNotContain("pass");
    }

    [Fact]
    public void TheSubstrateUrlHasNoTrailingSlashAndTheMcpAddressAppendsMcpToAPrefixPath()
    {
        var plan = Plan(Settings() with { SubstrateEndpoint = new Uri("https://substrate.example/api/") });

        plan.Environment["SUBSTRATE_URL"].ShouldBe("https://substrate.example/api");
        plan.McpServers.ShouldHaveSingleItem().Url.ToString().ShouldBe("https://substrate.example/api/mcp");
    }

    [Fact]
    public void WithoutATokenTheMcpServerHasNoHeadersAndWithoutATokensDirectoryNoTokensDirVariableIsSet()
    {
        var plan = Plan(Settings() with { SubstrateToken = null, SubstrateTokensDirectory = null });

        plan.McpServers.ShouldHaveSingleItem().Headers.ShouldBeEmpty();
        plan.Environment.ContainsKey("SUBSTRATE_TOKENS_DIR").ShouldBeFalse();
        plan.Environment["SUBSTRATE_URL"].ShouldBe("http://127.0.0.1:7410");
    }

    [Fact]
    public void ASubstratePluginThatIsNotReadyLeavesNoMcpServerEvenWhenTheEndpointIsHealthy()
    {
        var plugins = OmpPluginCatalog.BuiltIn
            .Select(plugin => string.Equals(plugin.Id, "substrate", StringComparison.Ordinal)
                ? PlannerFixtures.Broken("substrate", PluginHealth.ManifestInvalid, "bad entry")
                : PlannerFixtures.Ready(plugin.Id))
            .ToList();

        var plan = Plan(Settings(), plugins);

        plan.McpServers.ShouldBeEmpty();
        plan.Environment.ContainsKey("SUBSTRATE_URL").ShouldBeFalse();
        plan.PlanningEvents.ShouldHaveSingleItem().Status.ShouldBe(StageStatus.Degraded);
    }

    [Fact]
    public void AllPluginsDisabledStillPlansPlainOmpWithTheSessionVariables()
    {
        var settings = Settings() with { UltrathinkEnabled = false, SwarmEnabled = false, SubstrateEnabled = false, GsdEnabled = false };

        var plan = Plan(settings);

        plan.ExtraArguments.ShouldBe(["--no-extensions"]);
        Env(plan).ShouldBe(BaseEnv().Append("ULTRATHINK_UPLIFT=0").Order(StringComparer.Ordinal));
        plan.McpServers.ShouldBeEmpty();
        plan.PlanningEvents.Select(e => e.Status).ShouldAllBe(status => status == StageStatus.Skipped);
        plan.PlanningEvents.Count.ShouldBe(4);
    }

    [Fact]
    public void SwarmGsdStateFileIsAPerSessionPathNeverEmptyAndAgentIdentityVariablesAreNeverSet()
    {
        var combinations = new[]
        {
            Settings(),
            Settings() with { SwarmEnabled = false },
            Settings() with { SubstrateEnabled = false, GsdEnabled = false },
            Settings() with { Profile = AgentLoopProfile.Isolated, IsolatedHomeDirectory = "/iso" },
            Settings() with { SubstrateReachable = false },
        };

        foreach (var settings in combinations)
        {
            var plan = Plan(settings);

            plan.Environment["SWARM_GSD_STATE_FILE"].ShouldBe("/state/sessions/s1/swarm/gsd-state.md");
            plan.ForTerminal().Environment["SWARM_GSD_STATE_FILE"].ShouldBe("/state/sessions/s1/swarm/gsd-state.md");
            plan.Environment.ContainsKey("SWARM_AGENT").ShouldBeFalse();
            plan.Environment.ContainsKey("SWARM_TASK_ID").ShouldBeFalse();
            plan.Environment.ContainsKey("SWARM_ROOT").ShouldBeFalse();
            plan.Environment.Keys.ShouldNotContain(key => key.StartsWith("SUBSTRATE_TOKEN", StringComparison.Ordinal) && key != "SUBSTRATE_TOKENS_DIR");
        }
    }

    [Fact]
    public void TheTokenAppearsOnlyInTheMcpHeaderNeverInArgumentsEnvironmentOrDescriptions()
    {
        var plan = Plan(Settings(), graphId: "ut-1-abc");

        plan.ExtraArguments.ShouldNotContain(argument => argument.Contains(Token, StringComparison.Ordinal));
        plan.Environment.Values.ShouldNotContain(value => value.Contains(Token, StringComparison.Ordinal));
        plan.PlanningEvents.ShouldAllBe(e => e.Detail == null || !e.Detail.Contains(Token, StringComparison.Ordinal));
        plan.ToString().ShouldNotContain(Token);
        plan.McpServers[0].ToString().ShouldNotContain(Token);
        plan.McpServers[0].Headers["Authorization"].ShouldContain(Token);
    }

    [Fact]
    public void PlanToStringShowsTheArgumentCountTheEnvironmentNamesAndTheServerNamesOnly()
    {
        var text = Plan(Settings()).ToString();

        text.ShouldBe(
            "OmpLaunchPlan { Arguments = 11, Environment = [AUTONOGRAMMER_CONNECTION_FILE, AUTONOGRAMMER_STEER_DIR, SUBSTRATE_OMP_PENDING_DIR, SUBSTRATE_TOKENS_DIR, SUBSTRATE_URL, " +
            "SWARM_CLAUDE_STATE_DIR, SWARM_DIR, SWARM_GSD_STATE_FILE, ULTRATHINK_STATE_DIR], McpServers = [substrate] }");
        text.ShouldNotContain("/state/sessions");
        text.ShouldNotContain("127.0.0.1");
    }

    [Fact]
    public void McpServerToStringShowsHeaderNamesAndDropsUserInformationQueryAndFragmentOfTheAddress()
    {
        var spec = new AgentMcpServerSpec
        {
            Name = "substrate",
            Url = new Uri("http://user:pw@127.0.0.1:7410/mcp?key=abc#frag"),
            Headers = new Dictionary<string, string>(StringComparer.Ordinal) { ["X-B"] = "b-secret", ["Authorization"] = "Bearer a-secret" },
        };

        spec.ToString().ShouldBe("AgentMcpServerSpec { Name = substrate, Url = http://127.0.0.1:7410/mcp, Headers = [Authorization, X-B] }");
    }

    [Fact]
    public void IsolatedProfileAddsTheHomeAndTheNeutralisingVariables()
    {
        var settings = Settings() with { Profile = AgentLoopProfile.Isolated, IsolatedHomeDirectory = "/iso" };

        var plan = Plan(settings);

        Env(plan).ShouldBe(
            SubstrateEnv().Concat(
            [
                "HOME=/iso/home",
                "PI_CODING_AGENT_DIR=/iso/agent",
                "XDG_CONFIG_HOME=/iso/home/.config",
                "XDG_DATA_HOME=/iso/home/.local/share",
                "XDG_CACHE_HOME=/iso/home/.cache",
                "XDG_STATE_HOME=/iso/home/.local/state",
                "ULTRATHINK_TRACK=0",
                "ULTRATHINK_SHIP=0",
                "ULTRATHINK_DECISIONS=0",
                "ULTRATHINK_HINDSIGHT=0",
                "ULTRATHINK_RAGFLOW=0",
                "ULTRATHINK_TEACH=0",
                "ULTRATHINK_GATEWAY=0",
                "ULTRATHINK_SWARM_ROOT=",
            ]).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void IsolatedProfileWithoutAnActiveSubstrateDisablesItsClients()
    {
        var settings = Settings() with { Profile = AgentLoopProfile.Isolated, IsolatedHomeDirectory = "/iso", SubstrateEnabled = false };

        var plan = Plan(settings);

        plan.Environment["SUBSTRATE_DISABLED"].ShouldBe("1");
        plan.Environment.ContainsKey("SUBSTRATE_URL").ShouldBeFalse();
    }

    [Fact]
    public void InheritProfileWithAnIsolatedHomeSetsOnlyHomeAndAgentDirectory()
    {
        var plan = Plan(Settings() with { IsolatedHomeDirectory = "/iso" });

        Env(plan).ShouldBe(SubstrateEnv().Concat(["HOME=/iso/home", "PI_CODING_AGENT_DIR=/iso/agent"]).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void InheritProfileWithoutAHomeDoesNotTouchHomeOrTheAgentDirectory()
    {
        var plan = Plan(Settings());

        plan.Environment.ContainsKey("HOME").ShouldBeFalse();
        plan.Environment.ContainsKey("PI_CODING_AGENT_DIR").ShouldBeFalse();
    }

    [Fact]
    public void IsolatedProfileWithoutAHomeDirectoryIsRejected()
    {
        Should.Throw<ArgumentException>(() => Plan(Settings() with { Profile = AgentLoopProfile.Isolated }));
    }

    [Fact]
    public void ProjectMcpOptOutAddsTheOverlayPathAndTheConfigArgumentLast()
    {
        var plan = Plan(Settings() with { DisableProjectMcpConfig = true });

        plan.ConfigOverlayPath.ShouldBe(SessionDir + "/omp-overlay.yml");
        plan.ExtraArguments.ShouldBe(Args(UltrathinkArgs, SwarmArgs, SubstrateArgs, GsdArgs, ["--config", SessionDir + "/omp-overlay.yml"]));
    }

    [Fact]
    public void EachSessionGetsItsOwnDirectoriesInTheEnvironment()
    {
        var first = OmpLaunchPlanner.Plan(Settings(), PlannerFixtures.AllReady(), "a", "/work/a", null);
        var second = OmpLaunchPlanner.Plan(Settings(), PlannerFixtures.AllReady(), "b", "/work/b", null);

        first.Environment["SWARM_DIR"].ShouldBe("/state/sessions/a/swarm");
        second.Environment["SWARM_DIR"].ShouldBe("/state/sessions/b/swarm");
        first.Environment["ULTRATHINK_STATE_DIR"].ShouldNotBe(second.Environment["ULTRATHINK_STATE_DIR"], StringComparer.Ordinal);
        first.WorkingDirectory.ShouldBe("/work/a");
        second.WorkingDirectory.ShouldBe("/work/b");
    }

    [Theory]
    [InlineData("..")]
    [InlineData("a/b")]
    [InlineData("")]
    public void AnUnsafeSessionIdIsRejected(string sessionId)
    {
        Should.Throw<ArgumentException>(
            () => OmpLaunchPlanner.Plan(Settings(), PlannerFixtures.AllReady(), sessionId, "/work", null));
    }

    [Fact]
    public void ARelativeWorkspaceIsRejected()
    {
        Should.Throw<ArgumentException>(
            () => OmpLaunchPlanner.Plan(Settings(), PlannerFixtures.AllReady(), "s1", "relative/work", null));
    }

    [Fact]
    public void PlanningCreatesNoFileAndNoDirectory()
    {
        using var fixture = new PluginFixtureDirectory();
        var settings = Settings() with
        {
            PluginsRoot = fixture.PluginsRoot,
            StateRoot = fixture.PathOf("state"),
            DisableProjectMcpConfig = true,
        };
        var before = Directory.EnumerateFileSystemEntries(fixture.Path, "*", SearchOption.AllDirectories).ToList();

        var plan = OmpLaunchPlanner.Plan(settings, PlannerFixtures.AllReady(), "s1", fixture.Path, null);

        plan.ConfigOverlayPath.ShouldNotBeNull();
        File.Exists(plan.ConfigOverlayPath).ShouldBeFalse();
        Directory.Exists(fixture.PathOf("state")).ShouldBeFalse();
        Directory.EnumerateFileSystemEntries(fixture.Path, "*", SearchOption.AllDirectories).ToList().ShouldBe(before);
    }
}
