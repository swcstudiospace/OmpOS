using System.Collections;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using Autonogrammer.Harness.Acp;
using Autonogrammer.Harness.AgentLoop.Launch;
using Autonogrammer.Harness.Rpc;
using Autonogrammer.Native.Engine;
using Autonogrammer.Native.Tests.AgentLoop;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Composition;

/// <summary>
/// Phase 82 Task 4 (SC1, RPC-82-1): one fixed session request composed down both adapters to the last host-side
/// value before process spawn, asserting byte parity modulo the transport delta enumerated here and nowhere else:
/// the ACP child argv is <c>acp</c> followed by <c>plan.ExtraArguments</c> (<c>AcpStdioTransport.StartProcess</c>);
/// the RPC child argv is <c>--mode rpc --no-ui --cwd &lt;wd&gt;</c> followed by the same <c>plan.ExtraArguments</c>
/// (<c>OmpRpcProcess.Start</c>). Everything else is byte-asserted: the working directory, the resolved environment
/// (names and values after each side's <c>EngineEnvironmentPolicy</c> application; the fixture carries no secret
/// values), the <c>--config</c> argv pair naming the same overlay file on both sides, and the overlay file bytes
/// themselves (written once via <c>ConfigOverlayWriter.Write</c>; neither adapter rewrites it).
/// Uncovered points and their owners: plan MCP servers P7–P9 (Task 6, refused by <c>ComposeRpc</c> while OQ-526 is
/// open — this fixture therefore carries zero servers and the test name says so), bundle-skills overlay reachability
/// P4 (Task 7), Cowork standing context P5 (ACP-only while OQ-525 is open), isolation wrapper P6 and hook gate P2
/// (refused, Task 7), approval behaviour P11 (OQ-525; argv mechanics only, via <c>ExtraArguments</c>).
/// No omp process is spawned: ACP is captured through <c>ScriptedOmp</c>'s transport factory, RPC through the
/// scripted <c>RpcSessionFactory</c> seam over a fake line transport.
/// </summary>
public sealed class LaunchPlanParityTests
{
    private const string ReadyLine =
        """{"type":"ready","protocolVersion":1,"supportedProtocolVersions":[1,2],"maxFrameBytes":1048576,"maxReassembledFrameBytes":67108864}""";

    /// <summary>One fixed session request: identical arguments, overlay, working directory and environment on both adapters; no MCP servers until Task 6 lands their channel.</summary>
    [Fact]
    public async Task LaunchPlanParity_IdenticalRequestDeliversIdenticalArgumentsOverlayAndEnvironment_NoMcpServersUntilTask6()
    {
        var ct = TestContext.Current.CancellationToken;
        using var tree = new TempTree();
        var stateRoot = tree.State;
        var settings = new AgentLoopSettings
        {
            PluginsRoot = tree.Plugins,
            StateRoot = stateRoot,
            UltrathinkEnabled = false,
            SwarmEnabled = false,
            SubstrateEnabled = false,
            GsdEnabled = false,
            RealmEnabled = false,
        };
        var sessionDirectory = SessionStateLayout.SessionDirectory(stateRoot, "s-parity");
        var overlayPath = SessionStateLayout.ConfigOverlayFile(sessionDirectory);
        var overlayEntries = new[] { "skills.customDirectories=" + Path.Combine(tree.Workspace, "bundle-skills") };
        using var overlay = ConfigOverlayWriter.Write(settings, sessionDirectory, null, null, overlayEntries);
        overlay.ShouldNotBeNull();
        overlay!.Path.ShouldBe(overlayPath);
        var overlayBytesBefore = await File.ReadAllBytesAsync(overlayPath, ct).ConfigureAwait(true);
        var overlayHashBefore = Convert.ToHexString(SHA256.HashData(overlayBytesBefore));

        var plan = FixturePlan(tree, overlayPath, overlayEntries);

        var omp = new ScriptedOmp();
        var acp = EngineComposition.Compose([], plan, omp.TransportFactory);
        await using (acp.Host.ConfigureAwait(true))
        {
            await acp.Host.ConnectAsync(ct).ConfigureAwait(true);
        }

        var rpcOptions = await ComposeRpcAsync(plan, ct).ConfigureAwait(true);
        var acpLaunch = omp.Launches.ShouldHaveSingleItem();

        AssertTransportDelta(plan, acpLaunch, rpcOptions, overlayPath);

        var overlayBytesAfter = await File.ReadAllBytesAsync(overlayPath, ct).ConfigureAwait(true);
        Convert.ToHexString(SHA256.HashData(overlayBytesAfter)).ShouldBe(overlayHashBefore);
        overlayBytesAfter.ShouldBe(overlayBytesBefore);

        AssertResolvedEnvironmentEqual(acpLaunch, rpcOptions);

        // The fixture carries no MCP servers until Task 6 lands their channel, and both sides deliver none.
        plan.McpServers.ShouldBeEmpty();
        acpLaunch.McpServers.ShouldBeEmpty();
    }

    /// <summary>The one fixed session-request fixture: substrate disabled so no network or secret enters it.</summary>
    private static OmpLaunchPlan FixturePlan(TempTree tree, string overlayPath, IReadOnlyList<string> overlayEntries) => new()
    {
        ExtraArguments = ["--no-extensions", "-e", "/fixture/ext/ultrathink", "--config", overlayPath, "--skills", "fixture-*", "--add-dir", Path.Combine(tree.Workspace, "other")],
        Environment = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["FIXTURE_STATE_DIR"] = Path.Combine(tree.State, "state"),
            ["FIXTURE_PLUGIN_PATH"] = "/fixture/ext/ultrathink",
        },
        McpServers = [],
        WorkingDirectory = tree.Workspace,
        ConfigOverlayPath = overlayPath,
        OverlayEntries = overlayEntries,
        PlanningEvents = [],
    };


    /// <summary>Transport delta, enumerated once: both sides take plan.ExtraArguments verbatim; only the leading words differ.</summary>
    private static void AssertTransportDelta(OmpLaunchPlan plan, AcpClientOptions acpLaunch, OmpRpcClientOptions rpcOptions, string overlayPath)
    {
        acpLaunch.ExtraArguments.ShouldBe(plan.ExtraArguments);
        rpcOptions.ExtraArguments.ShouldBe(plan.ExtraArguments);
        acpLaunch.WorkingDirectory.ShouldBe(plan.WorkingDirectory);
        rpcOptions.Cwd.ShouldBe(plan.WorkingDirectory);

        // Both argv name the same overlay file.
        acpLaunch.ExtraArguments.SkipWhile(static a => !string.Equals(a, "--config", StringComparison.Ordinal)).Take(2).ShouldBe(["--config", overlayPath]);
        rpcOptions.ExtraArguments.SkipWhile(static a => !string.Equals(a, "--config", StringComparison.Ordinal)).Take(2).ShouldBe(["--config", overlayPath]);
    }

    /// <summary>
    /// The resolved environment is identical: the ACP transport applies EngineEnvironmentPolicy to the inherited
    /// environment with the plan's entries on top; the RPC composition does the same thing into a complete
    /// environment. Replaying the ACP side here must therefore match the RPC options exactly, names and values.
    /// </summary>
    private static void AssertResolvedEnvironmentEqual(AcpClientOptions acpLaunch, OmpRpcClientOptions rpcOptions)
    {
        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var acpResolved = new Dictionary<string, string?>(comparer);
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            acpResolved[(string)entry.Key] = (string?)entry.Value;
        }

        new EngineEnvironmentPolicy(acpLaunch.EnvironmentMode, acpLaunch.EnvironmentAllowList).Apply(acpResolved, acpLaunch.EnvironmentVariables);
        var acpPairs = acpResolved
            .Where(static pair => pair.Value is not null)
            .Select(static pair => pair.Key + "=" + pair.Value)
            .OrderBy(static value => value, StringComparer.Ordinal);
        var rpcPairs = rpcOptions.EnvironmentVariables!
            .Select(static pair => pair.Key + "=" + pair.Value)
            .OrderBy(static value => value, StringComparer.Ordinal);
        rpcPairs.ShouldBe(acpPairs);
    }


    /// <summary>Overlay, without a path, stays refused on RPC: nothing would write the file the entries name.</summary>
    [Fact]
    public void ComposeRpc_OverlayEntriesWithoutOverlayPath_Refuses()
    {
        var plan = new OmpLaunchPlan
        {
            ExtraArguments = [],
            Environment = new Dictionary<string, string>(StringComparer.Ordinal),
            McpServers = [],
            WorkingDirectory = "/work",
            OverlayEntries = ["skills.customDirectories=/fixture/skills"],
            PlanningEvents = [],
        };

        Should.Throw<ArgumentException>(() => EngineComposition.Compose([], plan, options: new EngineCompositionOptions { Transport = EngineTransport.Rpc }));
    }

    /// <summary>Plan MCP servers stay refused on RPC (OQ-526): no observed channel exists.</summary>
    [Fact]
    public void ComposeRpc_PlanMcpServers_Refuses()
    {
        var plan = new OmpLaunchPlan
        {
            ExtraArguments = [],
            Environment = new Dictionary<string, string>(StringComparer.Ordinal),
            McpServers = [new Autonogrammer.Harness.AgentLoop.Launch.AgentMcpServerSpec { Name = "fixture", Url = new Uri("http://127.0.0.1:1/mcp"), Headers = new Dictionary<string, string>(StringComparer.Ordinal) }],
            WorkingDirectory = "/work",
            PlanningEvents = [],
        };

        Should.Throw<ArgumentException>(() => EngineComposition.Compose([], plan, options: new EngineCompositionOptions { Transport = EngineTransport.Rpc }));
    }

    /// <summary>User MCP servers, the hook gate and the process wrapper stay refused on RPC (OQ-526), one clause each.</summary>
    [Fact]
    public void ComposeRpc_UserMcpServersHookGateAndWrapper_Refuse()
    {
        var plan = new OmpLaunchPlan
        {
            ExtraArguments = [],
            Environment = new Dictionary<string, string>(StringComparer.Ordinal),
            McpServers = [],
            WorkingDirectory = "/work",
            PlanningEvents = [],
        };

        Should.Throw<ArgumentException>(() => EngineComposition.Compose([], plan, options: new EngineCompositionOptions
        {
            Transport = EngineTransport.Rpc,
            ExtraMcpServers = [new Autonogrammer.Harness.Acp.AcpHttpMcpServer("fixture", new Uri("http://127.0.0.1:2/mcp"), new Dictionary<string, string>(StringComparer.Ordinal))],
        }));
        Should.Throw<ArgumentException>(() => EngineComposition.Compose([], plan, options: new EngineCompositionOptions
        {
            Transport = EngineTransport.Rpc,
            ProcessWrapper = new PassthroughWrapper(),
        }));
        Should.Throw<ArgumentException>(() => EngineComposition.Compose([], plan, options: new EngineCompositionOptions
        {
            Transport = EngineTransport.Rpc,
            HookGate = new PassthroughGate(),
        }));
    }
    private sealed class PassthroughGate : Autonogrammer.Harness.Acp.IAcpHookGate
    {
        public void Observe(Autonogrammer.Harness.Acp.AcpHookSignal signal) { }
        public ValueTask SessionOpenedAsync(string sessionId, bool resumed, CancellationToken cancellationToken) => ValueTask.CompletedTask;
        public ValueTask<Autonogrammer.Harness.Acp.AcpHookVerdict> BeforePromptAsync(string prompt, CancellationToken cancellationToken) => ValueTask.FromResult(new Autonogrammer.Harness.Acp.AcpHookVerdict());
        public ValueTask<Autonogrammer.Harness.Acp.AcpHookVerdict> BeforePermissionAsync(Autonogrammer.Harness.Acp.AcpPermissionRequest request, CancellationToken cancellationToken) => ValueTask.FromResult(new Autonogrammer.Harness.Acp.AcpHookVerdict());
        public ValueTask<Autonogrammer.Harness.Acp.AcpHookVerdict> BeforeReadFileAsync(string fullPath, string content, CancellationToken cancellationToken) => ValueTask.FromResult(new Autonogrammer.Harness.Acp.AcpHookVerdict());
        public ValueTask AfterFileWriteAsync(string fullPath, string? previousContent, string content, CancellationToken cancellationToken) => ValueTask.CompletedTask;
        public ValueTask<Autonogrammer.Harness.Acp.AcpTurnEndDecision> TurnEndedAsync(string stopReason, CancellationToken cancellationToken) => ValueTask.FromResult(new Autonogrammer.Harness.Acp.AcpTurnEndDecision());
        public ValueTask SessionClosedAsync(string reason, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }

    private sealed class PassthroughWrapper : Autonogrammer.Harness.Abstractions.IProcessLaunchWrapper
    {
        public Autonogrammer.Harness.Abstractions.ProcessLaunch Wrap(Autonogrammer.Harness.Abstractions.ProcessLaunch intended) => intended;
    }

    private static async Task<OmpRpcClientOptions> ComposeRpcAsync(OmpLaunchPlan plan, CancellationToken cancellationToken)
    {
        OmpRpcClientOptions? captured = null;
        var transport = new ParityFakeTransport();
        try
        {
            var options = new EngineCompositionOptions
            {
                Transport = EngineTransport.Rpc,
                RpcSessionFactory = rpcOptions =>
                {
                    captured = rpcOptions;
                    return new OmpRpcSession(transport, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
                },
            };
            var composition = EngineComposition.Compose([], plan, options: options);
            await using (composition.Host.ConfigureAwait(true))
            {
                await composition.Host.ConnectAsync(cancellationToken).ConfigureAwait(true);
            }
        }
        finally
        {
            await transport.DisposeAsync().ConfigureAwait(false);
        }

        return captured.ShouldNotBeNull();
    }


    /// <summary>A minimal scripted RPC line transport: <c>ready</c> on open, <c>negotiate_protocol</c> and <c>get_state</c> answered, nothing else.</summary>
    private sealed class ParityFakeTransport : IRpcTransport
    {
        private readonly Channel<string> _stdout = Channel.CreateUnbounded<string>();

        public ParityFakeTransport() => _stdout.Writer.TryWrite(ReadyLine);

        public IAsyncEnumerable<string> ReadLinesAsync(CancellationToken cancellationToken) =>
            _stdout.Reader.ReadAllAsync(cancellationToken);

        public Task SendLineAsync(string line, CancellationToken cancellationToken)
        {
            string command = JsonNode.Parse(line)!["type"]!.GetValue<string>();
            string id = JsonNode.Parse(line)!["id"]!.GetValue<string>();
            string? answer = command switch
            {
                "negotiate_protocol" => "{\"id\":\"" + id + "\",\"type\":\"response\",\"command\":\"" + command + "\",\"success\":true,\"data\":{\"protocolVersion\":2}}",
                "get_state" => "{\"id\":\"" + id + "\",\"type\":\"response\",\"command\":\"" + command + "\",\"success\":true,\"data\":{\"model\":{\"id\":\"fixture\",\"provider\":\"fixture\"}}}",
                _ => null,
            };
            if (answer is not null)
            {
                _stdout.Writer.TryWrite(answer);
            }

            return Task.CompletedTask;
        }

        public Task CloseInputAsync(CancellationToken cancellationToken)
        {
            _stdout.Writer.TryComplete();
            return Task.CompletedTask;
        }

        public Task<int> WaitForExitAsync(CancellationToken cancellationToken) => Task.FromResult(0);

        public ValueTask DisposeAsync()
        {
            _stdout.Writer.TryComplete();
            return ValueTask.CompletedTask;
        }
    }
}
