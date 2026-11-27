using Autonogrammer.Harness.Acp;
using Autonogrammer.Harness.AgentLoop.Launch;
using Autonogrammer.Native.Engine;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Composition;

/// <summary>
/// The OQ-526 remainder frozen on RPC (Phase 83, Tasks 7–8): the Phase 83 re-probe of 2026-11-26 observed no
/// channel — no MCP/server spelling for <c>--mode rpc</c> in omp 19.2.1 <c>--help</c>, no MCP-shaped command in
/// the RPC command table, the overlay <c>mcpServers</c> key inert, no gate surface on the RPC prompt path, and
/// <c>OmpRpcProcess.Start</c> taking the omp path directly with no wrapper seam. Each refusal therefore names the
/// single point and the re-probe date; a future omp version adding a channel must delete the named test. The
/// OQ-525 approval refusal is asserted present and unchanged (Task 8 must not move it).
/// </summary>
public sealed class RpcRefusalFreezeTests
{
    private static OmpLaunchPlan Plan() => new()
    {
        ExtraArguments = [],
        Environment = new Dictionary<string, string>(StringComparer.Ordinal),
        McpServers = [],
        WorkingDirectory = "/work",
        PlanningEvents = [],
    };

    /// <summary>Plan MCP servers: RPC refuses and names the Phase 83 re-probe (Task 7, OQ-526 P7–P9).</summary>
    [Fact]
    public void ComposeRpc_PlanMcpServers_RefusesNamingPhase83Reprobe()
    {
        var plan = Plan() with
        {
            McpServers = [new AgentMcpServerSpec { Name = "fixture", Url = new Uri("http://127.0.0.1:1/mcp"), Headers = new Dictionary<string, string>(StringComparer.Ordinal) }],
        };

        var error = Should.Throw<ArgumentException>(() => EngineComposition.Compose([], plan, options: new EngineCompositionOptions { Transport = EngineTransport.Rpc }));

        error.Message.ShouldContain("plan MCP servers");
        error.Message.ShouldContain("Phase 83");
        error.Message.ShouldContain("OQ-526");
    }

    /// <summary>User MCP servers: RPC refuses and names the Phase 83 re-probe (Task 7, OQ-526 P7–P9).</summary>
    [Fact]
    public void ComposeRpc_UserMcpServers_RefusesNamingPhase83Reprobe()
    {
        var options = new EngineCompositionOptions
        {
            Transport = EngineTransport.Rpc,
            ExtraMcpServers = [new AcpHttpMcpServer("fixture", new Uri("http://127.0.0.1:2/mcp"), new Dictionary<string, string>(StringComparer.Ordinal))],
        };

        var error = Should.Throw<ArgumentException>(() => EngineComposition.Compose([], Plan(), options: options));

        error.Message.ShouldContain("user MCP servers");
        error.Message.ShouldContain("Phase 83");
        error.Message.ShouldContain("OQ-526");
    }

    /// <summary>Hook gate: RPC refuses and names the Phase 83 re-probe (Task 8, OQ-526 P2).</summary>
    [Fact]
    public void ComposeRpc_HookGate_RefusesNamingPhase83Reprobe()
    {
        var options = new EngineCompositionOptions { Transport = EngineTransport.Rpc, HookGate = new PassthroughGate() };

        var error = Should.Throw<ArgumentException>(() => EngineComposition.Compose([], Plan(), options: options));

        error.Message.ShouldContain("hook gate");
        error.Message.ShouldContain("Phase 83");
        error.Message.ShouldContain("OQ-526");
    }

    /// <summary>Process wrapper: RPC refuses and names the Phase 83 re-probe (Task 8, OQ-526 P6).</summary>
    [Fact]
    public void ComposeRpc_ProcessWrapper_RefusesNamingPhase83Reprobe()
    {
        var options = new EngineCompositionOptions { Transport = EngineTransport.Rpc, ProcessWrapper = new PassthroughWrapper() };

        var error = Should.Throw<ArgumentException>(() => EngineComposition.Compose([], Plan(), options: options));

        error.Message.ShouldContain("process wrapper");
        error.Message.ShouldContain("Phase 83");
        error.Message.ShouldContain("OQ-526");
    }

    /// <summary>The OQ-525 approval refusal stays present, deny-by-default, and untouched by Tasks 7–8.</summary>
    [Fact]
    public void ComposeRpc_ApprovalCapableFlow_RefusesNamingOq525()
    {
        var error = Should.Throw<ArgumentException>(() => EngineComposition.Compose(["read"], Plan(), options: new EngineCompositionOptions { Transport = EngineTransport.Rpc }));

        error.Message.ShouldContain("OQ-525");
        error.Message.ShouldContain("deny-by-default");
    }

    private sealed class PassthroughGate : IAcpHookGate
    {
        public void Observe(AcpHookSignal signal) { }
        public ValueTask SessionOpenedAsync(string sessionId, bool resumed, CancellationToken cancellationToken) => ValueTask.CompletedTask;
        public ValueTask<AcpHookVerdict> BeforePromptAsync(string prompt, CancellationToken cancellationToken) => ValueTask.FromResult(new AcpHookVerdict());
        public ValueTask<AcpHookVerdict> BeforePermissionAsync(AcpPermissionRequest request, CancellationToken cancellationToken) => ValueTask.FromResult(new AcpHookVerdict());
        public ValueTask<AcpHookVerdict> BeforeReadFileAsync(string fullPath, string content, CancellationToken cancellationToken) => ValueTask.FromResult(new AcpHookVerdict());
        public ValueTask AfterFileWriteAsync(string fullPath, string? previousContent, string content, CancellationToken cancellationToken) => ValueTask.CompletedTask;
        public ValueTask<AcpTurnEndDecision> TurnEndedAsync(string stopReason, CancellationToken cancellationToken) => ValueTask.FromResult(new AcpTurnEndDecision());
        public ValueTask SessionClosedAsync(string reason, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }

    private sealed class PassthroughWrapper : Autonogrammer.Harness.Abstractions.IProcessLaunchWrapper
    {
        public Autonogrammer.Harness.Abstractions.ProcessLaunch Wrap(Autonogrammer.Harness.Abstractions.ProcessLaunch intended) => intended;
    }
}
