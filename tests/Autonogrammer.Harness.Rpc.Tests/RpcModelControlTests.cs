using Autonogrammer.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Rpc.Tests;

/// <summary>
/// Phase 80 Task 6: model control is read-only over RPC and options/fork stay named protocol gaps
/// (OQ-523 — no set-model, set-option or fork command exists in the client or the Phase 79 ledger).
/// Every attempt is pinned against the transport line count so a guessed command would fail here.
/// </summary>
public sealed class RpcModelControlTests
{
    private const string ReadyLine =
        """{"type":"ready","protocolVersion":1,"supportedProtocolVersions":[1,2],"maxFrameBytes":1048576,"maxReassembledFrameBytes":67108864}""";

    /// <summary>The reported current model is the single choice, ModelChanged fires for it, and image support stays unclaimed.</summary>
    [Fact]
    public async Task Model_ReportedCurrent_IsTheSingleChoice()
    {
        var ct = TestContext.Current.CancellationToken;
        FakeRpcTransport transport = Engine(RpcTestData.StateData);
        var session = new OmpRpcSession(transport, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
        int modelChanges = 0;
        session.ModelChanged += (_, _) => Interlocked.Increment(ref modelChanges);

        await session.CreateAsync(ct).ConfigureAwait(true);

        session.CurrentModelId.ShouldBe("glm-5.3-flash");
        session.ModelChoices.ShouldBe([new OmpModelChoice("glm-5.3-flash", "glm-5.3-flash")]);
        ((IOmpModelControl)session).SupportsImages.ShouldBeNull();
        Volatile.Read(ref modelChanges).ShouldBe(1);
        await session.DisposeAsync().ConfigureAwait(true);
    }

    /// <summary>Switching to the already-current model is a no-op switch that sends nothing.</summary>
    [Fact]
    public async Task SetModel_AlreadyCurrent_SwitchesWithoutSending()
    {
        var ct = TestContext.Current.CancellationToken;
        FakeRpcTransport transport = Engine(RpcTestData.StateData);
        OmpRpcSession session = await StartAsync(transport, ct).ConfigureAwait(true);
        int sentBefore = transport.Sent.Count;

        OmpModelSwitchResult result = await session.SetModelAsync("glm-5.3-flash", ct).ConfigureAwait(true);

        result.ShouldBe(new OmpModelSwitchResult(OmpModelSwitchOutcome.Switched, "glm-5.3-flash", null));
        transport.Sent.Count.ShouldBe(sentBefore);
        await session.DisposeAsync().ConfigureAwait(true);
    }

    /// <summary>Any other model switch is refused with the OQ-523 gap named, and nothing is sent.</summary>
    [Fact]
    public async Task SetModel_OtherModel_RefusedWithNamedGapWithoutSending()
    {
        var ct = TestContext.Current.CancellationToken;
        FakeRpcTransport transport = Engine(RpcTestData.StateData);
        OmpRpcSession session = await StartAsync(transport, ct).ConfigureAwait(true);
        int sentBefore = transport.Sent.Count;

        OmpModelSwitchResult result = await session.SetModelAsync("some/other-model", ct).ConfigureAwait(true);

        result.Outcome.ShouldBe(OmpModelSwitchOutcome.NoModelOption);
        result.CurrentModelId.ShouldBe("glm-5.3-flash");
        result.Message.ShouldBe(
            "omp RPC exposes no verified model-switch command (OQ-523); the model is fixed at engine start (--model/profile).");
        transport.Sent.Count.ShouldBe(sentBefore);
        await session.DisposeAsync().ConfigureAwait(true);
    }

    /// <summary>Options are an explicitly empty surface: empty list, every set UnknownOption with the OQ-523 reason, nothing sent.</summary>
    [Fact]
    public async Task Options_EmptySurface_RefusesEverySetWithoutSending()
    {
        var ct = TestContext.Current.CancellationToken;
        FakeRpcTransport transport = Engine(RpcTestData.StateData);
        OmpRpcSession session = await StartAsync(transport, ct).ConfigureAwait(true);
        int sentBefore = transport.Sent.Count;

        session.Options.ShouldBeEmpty();
        OmpOptionSetResult result = await session.SetOptionAsync("thinking", "high", ct).ConfigureAwait(true);

        result.Outcome.ShouldBe(OmpOptionSetOutcome.UnknownOption);
        result.Message.ShouldBe(
            "omp RPC exposes no verified option-set command (OQ-523); session options are fixed at engine start.");
        transport.Sent.Count.ShouldBe(sentBefore);
        await session.DisposeAsync().ConfigureAwait(true);
    }

    /// <summary>Fork is not implemented: the absence of the interface is the honest signal (hosts refuse by name).</summary>
    [Fact]
    public async Task Fork_InterfaceAbsentByDesign()
    {
        var ct = TestContext.Current.CancellationToken;
        OmpRpcSession session = await StartAsync(Engine(RpcTestData.StateData), ct).ConfigureAwait(true);

        session.ShouldNotBeAssignableTo<IOmpSessionFork>();
        await session.DisposeAsync().ConfigureAwait(true);
    }

    /// <summary>The factory wrapper forwards the read-only model surface and refusals; before start a switch is NotOpen.</summary>
    [Fact]
    public async Task Wrapper_ForwardsModelControlAndOptions()
    {
        var ct = TestContext.Current.CancellationToken;
        FakeRpcTransport transport = Engine(RpcTestData.StateData);
        var starter = new TransportStarter(() => transport);
        IOmpSession session = new RpcHarnessSessionFactory(starter).CreateSession("hi");
        await using (session.ConfigureAwait(false))
        {
            var control = session.ShouldBeAssignableTo<IOmpModelControl>();
            control.ModelChoices.ShouldBeEmpty();
            control.CurrentModelId.ShouldBeNull();
            (await control.SetModelAsync("glm-5.3-flash", ct).ConfigureAwait(true)).Outcome
                .ShouldBe(OmpModelSwitchOutcome.NotOpen);

            await session.CreateAsync(ct).ConfigureAwait(true);

            control.CurrentModelId.ShouldBe("glm-5.3-flash");
            control.ModelChoices.Count.ShouldBe(1);
            OmpModelSwitchResult refused = await control.SetModelAsync("some/other-model", ct).ConfigureAwait(true);
            refused.Outcome.ShouldBe(OmpModelSwitchOutcome.NoModelOption);
            refused.Message.ShouldNotBeNull().ShouldContain("OQ-523");

            var options = session.ShouldBeAssignableTo<IOmpSessionOptions>();
            options.Options.ShouldBeEmpty();
            (await options.SetOptionAsync("thinking", "high", ct).ConfigureAwait(true)).Outcome
                .ShouldBe(OmpOptionSetOutcome.UnknownOption);
            session.ShouldNotBeAssignableTo<IOmpSessionFork>();
        }
    }

    private static async Task<OmpRpcSession> StartAsync(FakeRpcTransport transport, CancellationToken ct)
    {
        var session = new OmpRpcSession(transport, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
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

    private static FakeRpcTransport Engine(string stateData)
    {
        var transport = new FakeRpcTransport(line =>
        {
            string command = RpcTestData.TypeOf(line);
            string id = RpcTestData.IdOf(line);
            return command switch
            {
                "negotiate_protocol" => [RpcTestData.Ok(id, command, """{"protocolVersion":2}""")],
                "get_state" => [RpcTestData.Ok(id, command, stateData)],
                _ => [],
            };
        });
        transport.Emit(ReadyLine);
        return transport;
    }
}
