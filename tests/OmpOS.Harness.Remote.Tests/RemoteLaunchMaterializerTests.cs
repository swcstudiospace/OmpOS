using OmpOS.Contracts.Agents;
using OmpOS.Contracts.Environments;
using OmpOS.Harness.AgentLoop.Launch;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Remote.Tests;

/// <summary>Rewriting a launch plan made for this machine into the launch of omp on a remote machine.</summary>
public sealed class RemoteLaunchMaterializerTests
{
    private const string SessionId = "session-42";
    private const string RemoteHome = "/home/dev";
    private const string RemoteState = "/home/dev/.ompos/sessions/session-42";

    private static readonly SshProfile Profile = new() { Alias = "build-box", RemoteWorkspaceRoot = "/srv/work space" };

    private static RemoteLaunchOptions Options(string stateRoot, IReadOnlyList<RemotePluginMapping>? mappings = null) =>
        new() { SessionId = SessionId, RemoteHomeDirectory = RemoteHome, LocalStateRoot = stateRoot, PluginMappings = mappings ?? [] };

    private static OmpLaunchPlan Plan(
        string stateRoot,
        string? overlayPath,
        IReadOnlyList<string>? arguments = null,
        IReadOnlyDictionary<string, string>? environment = null,
        IReadOnlyList<AgentMcpServerSpec>? servers = null)
    {
        var session = SessionStateLayout.SessionDirectory(stateRoot, SessionId);
        return new OmpLaunchPlan
        {
            ExtraArguments = arguments ?? ["--no-extensions", "-e", "/local/plugins/ultrathink/index.js", "-e", "/local/plugins/swarm/index.js"],
            Environment = environment ?? new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["HOME"] = "/local/isolated/home",
                ["PI_CODING_AGENT_DIR"] = "/local/isolated/agent",
                ["XDG_CONFIG_HOME"] = "/local/isolated/home/.config",
                ["ULTRATHINK_STATE_DIR"] = SessionStateLayout.UltrathinkDirectory(session),
                ["SUBSTRATE_OMP_PENDING_DIR"] = SessionStateLayout.SubstratePendingDirectory(session),
                ["SUBSTRATE_URL"] = "http://127.0.0.1:7410",
                ["ULTRATHINK_UPLIFT"] = "0",
                ["ULTRATHINK_SWARM_ROOT"] = string.Empty,
            },
            McpServers = servers ?? [],
            WorkingDirectory = "/local/ws",
            ConfigOverlayPath = overlayPath,
            PlanningEvents = [],
            LoadedPluginIds = ["ultrathink", "swarm"],
        };
    }

    private static string WriteOverlay(string stateRoot)
    {
        var path = SessionStateLayout.ConfigOverlayFile(SessionStateLayout.SessionDirectory(stateRoot, SessionId));
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "mcp:\n  enableProjectConfig: false\n");
        return path;
    }

    /// <summary>Without a mapping the plugin entries are left out, the rest of the arguments stay, and the degradation is reported.</summary>
    [Fact]
    public void PluginsWithoutAMappingAreLeftOutAndReported()
    {
        using var directory = TempDirectory.Create();
        var plan = Plan(directory.Path, overlayPath: null);

        var launch = new RemoteLaunchMaterializer(new ManualTimeProvider()).Materialize(plan, Profile, Options(directory.Path));

        launch.Arguments.ShouldBe(["--no-extensions"]);
        launch.LoadedPluginIds.ShouldBeEmpty();
        var degraded = launch.PlanningEvents.Single(evt => evt.StageId == "remote-launch:plugins");
        degraded.Status.ShouldBe(StageStatus.Degraded);
        degraded.SessionId.ShouldBe(SessionId);
        var detail = degraded.Detail.ShouldNotBeNull();
        detail.ShouldContain("2 plugin entry file(s)", Case.Sensitive);
        detail.ShouldContain("build-box", Case.Sensitive);
        launch.WorkingDirectory.ShouldBe("/srv/work space");
        launch.OmpPath.ShouldBe("omp");
        launch.StateDirectory.ShouldBe(RemoteState);
    }

    /// <summary>A mapping rewrites the entries under it; an entry outside every mapping is left out and the plugin list is emptied because the entry-to-plugin link is unknown.</summary>
    [Fact]
    public void MappedPluginsAreRewrittenAndOthersLeftOut()
    {
        using var directory = TempDirectory.Create();
        var plan = Plan(directory.Path, overlayPath: null);
        var mappings = new[] { new RemotePluginMapping("/local/plugins/ultrathink", "/opt/ompos/plugins/ultrathink") };

        var launch = new RemoteLaunchMaterializer().Materialize(plan, Profile, Options(directory.Path, mappings));

        launch.Arguments.ShouldBe(["--no-extensions", "-e", "/opt/ompos/plugins/ultrathink/index.js"]);
        launch.PlanningEvents.Single(evt => evt.StageId == "remote-launch:plugins").Detail.ShouldNotBeNull().ShouldContain("1 plugin entry file(s)", Case.Sensitive);
        launch.LoadedPluginIds.ShouldBeEmpty();
    }

    /// <summary>When every entry is mapped nothing is reported and the plugin list is kept. A sibling directory with the same prefix is not inside a mapping.</summary>
    [Fact]
    public void FullyMappedPluginsKeepTheirList()
    {
        using var directory = TempDirectory.Create();
        var plan = Plan(directory.Path, overlayPath: null);
        var mappings = new[] { new RemotePluginMapping("/local/plugins", "/opt/ompos/plugins/") };

        var launch = new RemoteLaunchMaterializer().Materialize(plan, Profile, Options(directory.Path, mappings));

        launch.Arguments.ShouldBe(["--no-extensions", "-e", "/opt/ompos/plugins/ultrathink/index.js", "-e", "/opt/ompos/plugins/swarm/index.js"]);
        launch.PlanningEvents.ShouldBeEmpty();
        launch.LoadedPluginIds.ShouldBe(["ultrathink", "swarm"]);

        var sibling = Plan(directory.Path, null, ["-e", "/local/plugins-other/x.js"]);
        new RemoteLaunchMaterializer().Materialize(sibling, Profile, Options(directory.Path, mappings)).Arguments.ShouldBeEmpty();
    }

    /// <summary>The settings overlay is read from the local file, delivered as a remote file and referenced by its remote path.</summary>
    [Fact]
    public void TheOverlayIsDeliveredAsARemoteFile()
    {
        using var directory = TempDirectory.Create();
        var overlay = WriteOverlay(directory.Path);
        var plan = Plan(directory.Path, overlay, ["--no-extensions", "--config", overlay]);

        var launch = new RemoteLaunchMaterializer().Materialize(plan, Profile, Options(directory.Path));

        launch.Arguments.ShouldBe(["--no-extensions", "--config", RemoteState + "/omp-overlay.yml"]);
        var file = launch.Files.ShouldHaveSingleItem();
        file.Path.ShouldBe(RemoteState + "/omp-overlay.yml");
        file.Content.ShouldBe("mcp:\n  enableProjectConfig: false\n", Case.Sensitive);
        launch.Arguments.ShouldNotContain(overlay);
    }

    /// <summary>A settings file that is not the overlay cannot exist remotely and is left out with a report.</summary>
    [Fact]
    public void AnotherLocalConfigFileIsLeftOut()
    {
        using var directory = TempDirectory.Create();
        var plan = Plan(directory.Path, null, ["--config", "/local/other.yml", "--no-extensions"]);

        var launch = new RemoteLaunchMaterializer().Materialize(plan, Profile, Options(directory.Path));

        launch.Arguments.ShouldBe(["--no-extensions"]);
        launch.PlanningEvents.ShouldContain(evt => evt.StageId == "remote-launch:config");
    }

    /// <summary>A plan whose overlay file is missing is refused instead of starting omp without the settings it expects.</summary>
    [Fact]
    public void AMissingOverlayFileIsRefused()
    {
        using var directory = TempDirectory.Create();
        var plan = Plan(directory.Path, directory.Combine("missing.yml"), ["--config", directory.Combine("missing.yml")]);

        Should.Throw<ArgumentException>(() => new RemoteLaunchMaterializer().Materialize(plan, Profile, Options(directory.Path)));
    }

    /// <summary>Local-only variables are dropped and reported, session directory values move to the remote session directory, and the substrate address goes through a forward.</summary>
    [Fact]
    public void EnvironmentIsRelocatedAndLoopbackIsForwarded()
    {
        using var directory = TempDirectory.Create();
        var plan = Plan(directory.Path, overlayPath: null);

        var launch = new RemoteLaunchMaterializer().Materialize(plan, Profile, Options(directory.Path));

        launch.Environment.Keys.ShouldNotContain("HOME");
        launch.Environment.Keys.ShouldNotContain("PI_CODING_AGENT_DIR");
        launch.Environment.Keys.ShouldNotContain("XDG_CONFIG_HOME");
        launch.Environment["ULTRATHINK_STATE_DIR"].ShouldBe(RemoteState + "/ultrathink");
        launch.Environment["SUBSTRATE_OMP_PENDING_DIR"].ShouldBe(RemoteState + "/substrate-pending");
        launch.Environment["ULTRATHINK_UPLIFT"].ShouldBe("0");
        launch.Environment["ULTRATHINK_SWARM_ROOT"].ShouldBe(string.Empty);

        var forward = launch.ReverseForwards.ShouldHaveSingleItem();
        forward.LocalHost.ShouldBe("127.0.0.1");
        forward.LocalPort.ShouldBe(7410);
        forward.RemotePort.ShouldBeInRange(41000, 42999);
        launch.Environment["SUBSTRATE_URL"].ShouldBe($"http://127.0.0.1:{forward.RemotePort}");
        launch.PlanningEvents.Single(evt => evt.StageId == "remote-launch:environment").Detail.ShouldNotBeNull()
            .ShouldBe("These local-only environment variables were not sent to the remote machine: HOME, PI_CODING_AGENT_DIR, XDG_CONFIG_HOME.", Case.Sensitive);
    }

    /// <summary>MCP servers on this machine's loopback are rewritten to the remote loopback port, share a forward with the same local service, keep their path and headers, and other servers are left alone.</summary>
    [Fact]
    public void McpServersAreForwarded()
    {
        using var directory = TempDirectory.Create();
        var servers = new[]
        {
            new AgentMcpServerSpec { Name = "substrate", Url = new Uri("http://127.0.0.1:7410/mcp?x=1"), Headers = new Dictionary<string, string>(StringComparer.Ordinal) { ["Authorization"] = "Bearer header-secret" } },
            new AgentMcpServerSpec { Name = "gateway", Url = new Uri("http://localhost:8090/gateway/mcp") },
            new AgentMcpServerSpec { Name = "remote-docs", Url = new Uri("https://docs.example.com/mcp") },
        };
        var plan = Plan(directory.Path, null, ["--no-extensions"], servers: servers);

        var launch = new RemoteLaunchMaterializer().Materialize(plan, Profile, Options(directory.Path));

        launch.ReverseForwards.Count.ShouldBe(2);
        var substratePort = launch.ReverseForwards.Single(forward => forward.LocalPort == 7410).RemotePort;
        var gatewayPort = launch.ReverseForwards.Single(forward => forward.LocalPort == 8090);
        gatewayPort.LocalHost.ShouldBe("localhost");
        gatewayPort.RemotePort.ShouldNotBe(substratePort);
        launch.Environment["SUBSTRATE_URL"].ShouldBe($"http://127.0.0.1:{substratePort}");

        launch.McpServers.Count.ShouldBe(3);
        launch.McpServers[0].Url.ToString().ShouldBe($"http://127.0.0.1:{substratePort}/mcp?x=1", Case.Sensitive);
        launch.McpServers[0].Headers["Authorization"].ShouldBe("Bearer header-secret", Case.Sensitive);
        launch.McpServers[1].Url.ToString().ShouldBe($"http://127.0.0.1:{gatewayPort.RemotePort}/gateway/mcp", Case.Sensitive);
        launch.McpServers[2].Url.ToString().ShouldBe("https://docs.example.com/mcp", Case.Sensitive);
        launch.McpServers[0].ToString().ShouldNotContain("header-secret", Case.Sensitive);
    }

    /// <summary>An address with user information is never forwarded or sent: it is left out with a report.</summary>
    [Fact]
    public void AddressesWithUserInformationAreLeftOut()
    {
        using var directory = TempDirectory.Create();
        var servers = new[] { new AgentMcpServerSpec { Name = "creds", Url = new Uri("http://user:pass@127.0.0.1:7410/mcp") } };
        var environment = new Dictionary<string, string>(StringComparer.Ordinal) { ["SUBSTRATE_URL"] = "http://user:pass@127.0.0.1:7410" };
        var plan = Plan(directory.Path, null, ["--no-extensions"], environment, servers);

        var launch = new RemoteLaunchMaterializer().Materialize(plan, Profile, Options(directory.Path));

        launch.McpServers.ShouldBeEmpty();
        launch.Environment.ShouldBeEmpty();
        launch.ReverseForwards.ShouldBeEmpty();
        launch.PlanningEvents.ShouldContain(evt => evt.StageId == "remote-launch:mcp:creds");
    }

    /// <summary>Provider tokens are part of the environment, flagged secret, never in an argument, and never printed.</summary>
    [Fact]
    public void SecretsAreEnvironmentOnly()
    {
        using var directory = TempDirectory.Create();
        var plan = Plan(directory.Path, overlayPath: null);
        var secrets = new Dictionary<string, string>(StringComparer.Ordinal) { ["ANTHROPIC_API_KEY"] = "sk-canary-0123456789" };

        var launch = new RemoteLaunchMaterializer().Materialize(plan, Profile, Options(directory.Path), secrets);

        launch.Environment["ANTHROPIC_API_KEY"].ShouldBe("sk-canary-0123456789", Case.Sensitive);
        launch.SecretEnvironmentNames.ShouldBe(["ANTHROPIC_API_KEY"]);
        launch.GetRedactionValues().ShouldBe(["sk-canary-0123456789"]);
        launch.Arguments.Any(argument => argument.Contains("sk-canary", StringComparison.Ordinal)).ShouldBeFalse();
        launch.ToString().ShouldNotContain("sk-canary", Case.Sensitive);
        launch.ToString().ShouldContain("ANTHROPIC_API_KEY", Case.Sensitive);
        launch.PlanningEvents.Any(evt => (evt.Detail ?? string.Empty).Contains("sk-canary", StringComparison.Ordinal)).ShouldBeFalse();
        var invocation = new SshCommandBuilder().BuildAcp(Profile, launch);
        invocation.Arguments.Any(argument => argument.Contains("sk-canary", StringComparison.Ordinal)).ShouldBeFalse();
    }

    /// <summary>The same session gets the same remote ports every time, so a reconnect finds its forwards where they were.</summary>
    [Fact]
    public void ForwardPortsAreStablePerSession()
    {
        using var directory = TempDirectory.Create();
        var plan = Plan(directory.Path, overlayPath: null);
        var materializer = new RemoteLaunchMaterializer();

        var first = materializer.Materialize(plan, Profile, Options(directory.Path));
        var second = materializer.Materialize(plan, Profile, Options(directory.Path));

        second.ReverseForwards.ShouldBe(first.ReverseForwards);
    }

    /// <summary>Unsafe inputs are refused: a session id that is not a path segment, a relative remote home, a bad port range, a mapping with a relative directory.</summary>
    [Fact]
    public void UnsafeInputsAreRefused()
    {
        using var directory = TempDirectory.Create();
        var plan = Plan(directory.Path, overlayPath: null);
        var materializer = new RemoteLaunchMaterializer();
        var baseline = Options(directory.Path);

        Should.Throw<ArgumentException>(() => materializer.Materialize(plan, Profile, baseline with { SessionId = "../escape" }));
        Should.Throw<ArgumentException>(() => materializer.Materialize(plan, Profile, baseline with { RemoteHomeDirectory = "relative" }));
        Should.Throw<ArgumentException>(() => materializer.Materialize(plan, Profile, baseline with { ReversePortBase = 80 }));
        Should.Throw<ArgumentException>(() => materializer.Materialize(plan, Profile, baseline with { ReversePortBase = 65000, ReversePortSpan = 2000 }));
        Should.Throw<ArgumentException>(() => materializer.Materialize(plan, Profile, baseline with { PluginMappings = [new RemotePluginMapping("relative", "/opt/x")] }));
        Should.Throw<ArgumentException>(() => materializer.Materialize(plan, Profile with { Alias = "-oBad" }, baseline));
        Should.Throw<ArgumentNullException>(() => materializer.Materialize(null!, Profile, baseline));
    }

    /// <summary>The planning events of the plan are kept in front of the ones the remote launch adds.</summary>
    [Fact]
    public void PlanningEventsAreKept()
    {
        using var directory = TempDirectory.Create();
        var earlier = new StageEvent
        {
            SessionId = SessionId,
            Source = StageSource.Substrate,
            StageId = "plugin",
            Title = "Substrate plugin",
            Status = StageStatus.Degraded,
            Timestamp = new DateTimeOffset(2026, 10, 27, 8, 0, 0, TimeSpan.Zero),
        };
        var plan = Plan(directory.Path, overlayPath: null) with { PlanningEvents = [earlier] };

        var launch = new RemoteLaunchMaterializer(new ManualTimeProvider()).Materialize(plan, Profile, Options(directory.Path));

        launch.PlanningEvents[0].ShouldBe(earlier);
        launch.PlanningEvents.Count.ShouldBeGreaterThan(1);
        launch.PlanningEvents[1].Timestamp.ShouldBe(new DateTimeOffset(2026, 10, 27, 9, 0, 0, TimeSpan.Zero));
    }
}
