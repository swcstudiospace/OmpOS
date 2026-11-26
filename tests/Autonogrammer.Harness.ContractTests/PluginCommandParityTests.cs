using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Harness.AgentLoop.Plugins;
using Autonogrammer.Harness.Acp;
using Autonogrammer.Harness.Rpc;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.ContractTests;

/// <summary>
/// Phase 82 Task 5, scripted half (gate): for each catalog plugin, one <c>available_commands_update</c> carrying that
/// plugin's <see cref="OmpPluginDescriptor.ExpectedCommands"/> drives both a scripted ACP session and a scripted RPC
/// session, and both surfaces must expose the identical <see cref="IOmpSessionCommands.Commands"/> set. The ACP frame
/// shape is the <c>session/update</c> notification the ACP suite scripts (<c>AcpAvailableCommandsTests.Update</c>); the
/// RPC frame shape is the frozen 19.1.1 capture (ledger R-UNSOLICITED, <c>commands</c> array). Realm is excluded
/// (unavailable, Task 8). The hooks bridge (<c>plugins/autonogrammer-hooks-omp</c>) registers no commands — its source
/// has no <c>registerCommand</c> — so its case asserts the empty set on both sides; its load channel (extension plus
/// <c>AUTONOGRAMMER_HOOKS_*</c> environment through the argv channel) is proven by the Task 4 fixture, not by frames.
/// Swarm's <c>swarm_*</c> tools never appear on the commands surface on either transport; the commands-surface claim
/// covers <c>swarm</c> only. The argv/overlay/environment half of the shared-plan guarantee is the canonical suite
/// <c>Autonogrammer.Native.Tests.Composition.LaunchPlanParityTests</c> (RPC-83-3); this class covers the
/// per-plugin <c>available_commands_update</c> surfacing over both scripted transports.
/// </summary>
public sealed class PluginCommandParityTests
{
    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(10);

    /// <summary>Every in-scope catalog plugin: the same expected-commands frame surfaces identically on ACP and RPC.</summary>
    [Theory]
    [InlineData("ultrathink")]
    [InlineData("swarm")]
    [InlineData("substrate")]
    [InlineData("gsd")]
    public async Task PluginCommands_SameFrameSurfacesIdenticallyOnBothTransports(string pluginId)
    {
        var ct = TestContext.Current.CancellationToken;
        OmpPluginCatalog.BuiltIn.ShouldContain(candidate => candidate.Id == pluginId);
        var descriptor = OmpPluginCatalog.BuiltIn.First(candidate => string.Equals(candidate.Id, pluginId, StringComparison.Ordinal));
        var expected = descriptor.ExpectedCommands;
        expected.ShouldNotBeEmpty();
        string commandsJson = "[" + string.Join(',', expected.Select(static name => "{\"name\":\"" + name + "\"}")) + "]";

        var acpCommands = await CommandsOnAcpAsync(commandsJson, ct).ConfigureAwait(true);
        var rpcCommands = await CommandsOnRpcAsync(commandsJson, ct).ConfigureAwait(true);

        acpCommands.Select(static command => command.Name).ShouldBe(expected);
        rpcCommands.Select(static command => command.Name).ShouldBe(expected);
        rpcCommands.Select(static command => command.Name).ShouldBe(acpCommands.Select(static command => command.Name));
    }

    /// <summary>The hooks bridge advertises no commands: both transports expose the identical (empty) set.</summary>
    [Fact]
    public async Task HooksBridge_NoCommandsAdvertisedButSurfaceParity_BothTransportsExposeEmptySet()
    {
        var ct = TestContext.Current.CancellationToken;

        var acpCommands = await CommandsOnAcpAsync("[]", ct).ConfigureAwait(true);
        var rpcCommands = await CommandsOnRpcAsync("[]", ct).ConfigureAwait(true);

        acpCommands.ShouldBeEmpty();
        rpcCommands.ShouldBeEmpty();
    }

    private static async Task<IReadOnlyList<OmpCommand>> CommandsOnAcpAsync(string commandsJson, CancellationToken cancellationToken)
    {
        var transport = new ScriptedAcpTransport();
        await using var transportScope = transport.ConfigureAwait(true);
        var session = new AcpAgentSession(transport, new AcpClientOptions { WorkingDirectory = AppContext.BaseDirectory, AuthMethodId = "agent" });
        await using var sessionScope = session.ConfigureAwait(true);
        await session.CreateAsync(cancellationToken).ConfigureAwait(true);
        using var changed = new SemaphoreSlim(0);
        session.CommandsChanged += (_, _) => changed.Release();

        transport.DeliverAvailableCommands(commandsJson);
        await changed.WaitAsync(WaitLimit, cancellationToken).ConfigureAwait(true);

        return ((IOmpSessionCommands)session).Commands;
    }

    private static async Task<IReadOnlyList<OmpCommand>> CommandsOnRpcAsync(string commandsJson, CancellationToken cancellationToken)
    {
        var starter = new ScriptedRpcTransport.Starter(() => new ScriptedRpcTransport("/tmp/plugin-parity.jsonl"));
        var factory = new RpcHarnessSessionFactory(starter);
        IOmpSession session = factory.CreateSession("plugin-parity");
        await using var sessionScope = session.ConfigureAwait(true);
        await session.CreateAsync(cancellationToken).ConfigureAwait(true);
        var commands = session.ShouldBeAssignableTo<IOmpSessionCommands>();
        using var changed = new SemaphoreSlim(0);
        commands.CommandsChanged += (_, _) => changed.Release();

        starter.Transports[^1].DeliverAvailableCommands(commandsJson);
        await changed.WaitAsync(WaitLimit, cancellationToken).ConfigureAwait(true);

        return commands.Commands;
    }
}
