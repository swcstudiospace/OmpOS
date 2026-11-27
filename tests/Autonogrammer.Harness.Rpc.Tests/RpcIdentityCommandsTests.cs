using Autonogrammer.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Rpc.Tests;

/// <summary>
/// Phase 80 Task 5: session identity from <c>get_state.sessionFile</c>, the advertised command list from
/// the frozen 19.1.1 <c>available_commands_update</c> frame (capture seq 3, R-UNSOLICITED), and the
/// capability keys (<c>transport</c>, the additive ready flag) over the scripted peer.
/// </summary>
public sealed class RpcIdentityCommandsTests
{
    private const string ReadyLine =
        """{"type":"ready","protocolVersion":1,"supportedProtocolVersions":[1,2],"maxFrameBytes":1048576,"maxReassembledFrameBytes":67108864}""";

    private const string ReadyLine19 =
        """{"type":"ready","protocolVersion":1,"supportedProtocolVersions":[1,2],"maxFrameBytes":1048576,"maxReassembledFrameBytes":67108864,"supportsBashExcludeFromContext":true}""";

    /// <summary>A reported <c>sessionFile</c> becomes the native session id; transport and ready-flag keys are advertised.</summary>
    [Fact]
    public async Task Identity_ReportedSessionFile_BecomesNativeSessionId()
    {
        var ct = TestContext.Current.CancellationToken;
        OmpRpcSession session = await StartAsync(ReadyLine19, RpcTestData.StateData, ct).ConfigureAwait(true);

        session.NativeSessionId.ShouldBe("/tmp/s.jsonl");
        session.AdvertisedCapabilities["model.id"].ShouldBe("glm-5.3-flash");
        session.AdvertisedCapabilities["transport"].ShouldBe("rpc");
        session.AdvertisedCapabilities["ready.supportsBashExcludeFromContext"].ShouldBe("true");
        await session.DisposeAsync().ConfigureAwait(true);
    }

    /// <summary>No <c>sessionFile</c> (ephemeral profile) leaves the identity null — never synthesised.</summary>
    [Fact]
    public async Task Identity_AbsentSessionFile_StaysNull()
    {
        var ct = TestContext.Current.CancellationToken;
        const string state = """{"model":{"id":"glm-5.3-flash","provider":"zai"}}""";
        OmpRpcSession session = await StartAsync(ReadyLine, state, ct).ConfigureAwait(true);

        session.NativeSessionId.ShouldBeNull();
        session.AdvertisedCapabilities["ready.supportsBashExcludeFromContext"].ShouldBe("false");
        await session.DisposeAsync().ConfigureAwait(true);
    }

    /// <summary>The frozen 53-command frame populates Commands in engine order; a repeat raises no second event.</summary>
    [Fact]
    public async Task Commands_FrozenFrame_PopulatesAndRepeatIsSilent()
    {
        var ct = TestContext.Current.CancellationToken;
        string commandsLine = RpcTestData.Observed19()[2];
        FakeRpcTransport transport = Engine(ReadyLine, RpcTestData.StateData);
        transport.Emit(commandsLine);
        var session = new OmpRpcSession(transport, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
        await session.CreateAsync(ct).ConfigureAwait(true);

        session.Commands.Count.ShouldBe(53);
        session.Commands[0].Name.ShouldBe("security");
        session.Commands[0].InputHint.ShouldBe("<plan|scan|status|cancel|scans|show|import|export|validate|compare|disposition>");

        // A repeat of the same frame must not raise again: count only rises for the different frame after it.
        int raised = 0;
        var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        session.CommandsChanged += (_, _) =>
        {
            Interlocked.Increment(ref raised);
            changed.TrySetResult();
        };
        transport.Emit(commandsLine);
        transport.Emit("""{"type":"available_commands_update","commands":[{"name":"only","description":"Only command"}]}""");
        await changed.Task.WaitAsync(ct).ConfigureAwait(true);

        Volatile.Read(ref raised).ShouldBe(1);
        session.Commands.ShouldBe([new OmpCommand("only", "Only command", null)]);
        session.State.ShouldBe(HarnessState.Ready);
        await session.DisposeAsync().ConfigureAwait(true);
    }

    /// <summary>Malformed command entries are skipped per entry and never fault the session.</summary>
    [Fact]
    public async Task Commands_MalformedEntries_SkippedWithoutFault()
    {
        var ct = TestContext.Current.CancellationToken;
        FakeRpcTransport transport = Engine(ReadyLine, RpcTestData.StateData);
        var session = new OmpRpcSession(transport, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
        await session.CreateAsync(ct).ConfigureAwait(true);

        var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        session.CommandsChanged += (_, _) => changed.TrySetResult();
        transport.Emit("""{"type":"available_commands_update","commands":[{"name":"ok"},"junk",{"description":"no name"},{"name":"also-ok","input":{"hint":"[x]"}}]}""");
        await changed.Task.WaitAsync(ct).ConfigureAwait(true);

        session.Commands.ShouldBe([new OmpCommand("ok", null, null), new OmpCommand("also-ok", null, "[x]")]);
        session.State.ShouldBe(HarnessState.Ready);
        await session.DisposeAsync().ConfigureAwait(true);
    }

    /// <summary>
    /// Resuming a foreign (non-RPC) id surfaces omp's switch refusal by name: the scripted peer rejects
    /// <c>switch_session</c> and the caller gets the typed refusal, never a silent fresh session.
    /// </summary>
    [Fact]
    public async Task Resume_ForeignId_SurfacesSwitchRefusal()
    {
        var ct = TestContext.Current.CancellationToken;
        FakeRpcTransport transport = Engine(ReadyLine, RpcTestData.StateData, rejectSwitch: true);
        var session = new OmpRpcSession(transport, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));

        RpcCommandException failure = await Should.ThrowAsync<RpcCommandException>(
            () => session.ResumeAsync("acp-session-id-not-a-path", ct)).ConfigureAwait(true);

        failure.Command.ShouldBe("switch_session");
        await session.DisposeAsync().ConfigureAwait(true);
    }

    /// <summary>The factory wrapper forwards identity and commands (null/empty before start) and their change event.</summary>
    [Fact]
    public async Task Wrapper_ForwardsIdentityAndCommands()
    {
        var ct = TestContext.Current.CancellationToken;
        FakeRpcTransport transport = Engine(ReadyLine, RpcTestData.StateData);
        var starter = new TransportStarter(() => transport);
        IOmpSession session = new RpcHarnessSessionFactory(starter).CreateSession("hi");
        await using (session.ConfigureAwait(false))
        {
            session.ShouldBeAssignableTo<IOmpSessionIdentity>().NativeSessionId.ShouldBeNull();
            session.ShouldBeAssignableTo<IOmpSessionCommands>().Commands.ShouldBeEmpty();

            await session.CreateAsync(ct).ConfigureAwait(true);

            session.ShouldBeAssignableTo<IOmpSessionIdentity>().NativeSessionId.ShouldBe("/tmp/s.jsonl");
            var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            session.ShouldBeAssignableTo<IOmpSessionCommands>().CommandsChanged += (_, _) => changed.TrySetResult();
            transport.Emit(RpcTestData.Observed19()[2]);
            await changed.Task.WaitAsync(ct).ConfigureAwait(true);

            session.ShouldBeAssignableTo<IOmpSessionCommands>().Commands.Count.ShouldBe(53);
        }
    }

    private static async Task<OmpRpcSession> StartAsync(string readyLine, string stateData, CancellationToken ct)
    {
        var session = new OmpRpcSession(Engine(readyLine, stateData), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
        var started = false;
        try
        {
            await session.CreateAsync(ct).ConfigureAwait(false);
            started = true;
            return session;
        }
        finally
        {
            if (!started)
            {
                await session.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private static FakeRpcTransport Engine(string readyLine, string stateData, bool rejectSwitch = false)
    {
        var transport = new FakeRpcTransport(line =>
        {
            string command = RpcTestData.TypeOf(line);
            string id = RpcTestData.IdOf(line);
            return command switch
            {
                "negotiate_protocol" => [RpcTestData.Ok(id, command, """{"protocolVersion":2}""")],
                "get_state" => [RpcTestData.Ok(id, command, stateData)],
                "switch_session" when rejectSwitch => [RpcTestData.Rejected(id, command, "no such session file")],
                "switch_session" => [RpcTestData.Ok(id, command, """{"cancelled":false}""")],
                _ => [],
            };
        });
        transport.Emit(readyLine);
        return transport;
    }
}
