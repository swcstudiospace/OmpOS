using Autonogrammer.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Rpc.Tests;

/// <summary>
/// Phase 80 Task 7: deny-by-default approvals over RPC (OQ-525, still open — no permission-request surface
/// has been observed). The limit is advertised on every session, and the pump never answers an unmodelled
/// ask: neither a grant nor a deny frame shape is invented, so an unknown request-shaped frame produces
/// zero outbound lines and does not fault the session.
/// </summary>
public sealed class RpcApprovalDenyTests
{
    private const string ReadyLine =
        """{"type":"ready","protocolVersion":1,"supportedProtocolVersions":[1,2],"maxFrameBytes":1048576,"maxReassembledFrameBytes":67108864}""";

    /// <summary>Every started session advertises <c>approvals=denied</c> in its capabilities (the user-visible limit).</summary>
    [Fact]
    public async Task StartedSession_AdvertisesApprovalsDenied()
    {
        var ct = TestContext.Current.CancellationToken;
        var session = new OmpRpcSession(Engine(), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));

        await session.CreateAsync(ct).ConfigureAwait(true);

        session.AdvertisedCapabilities["approvals"].ShouldBe("denied");
        await session.DisposeAsync().ConfigureAwait(true);
    }

    /// <summary>
    /// Unknown request-shaped frames (including one carrying an <c>id</c>, as an unobserved permission ask
    /// might) get no answer and no fault. The prompt run afterwards is the synchronisation point: the pump
    /// dispatches lines in order, so completing it proves the asks were processed — and the only outbound
    /// line added is that prompt itself.
    /// </summary>
    [Fact]
    public async Task UnmodelledAsk_ProducesZeroOutboundLinesAndNoFault()
    {
        var ct = TestContext.Current.CancellationToken;
        FakeRpcTransport transport = Engine();
        var session = new OmpRpcSession(transport, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
        await session.CreateAsync(ct).ConfigureAwait(true);
        int sentBefore = transport.Sent.Count;

        transport.Emit("""{"type":"some_unmodelled_ask","id":"77","tool":"bash","input":"rm -rf /"}""");
        transport.Emit("""{"type":"another_unmodelled_ask","request":"grant"}""");

        List<HarnessEvent> events = await CollectAsync(session.SubmitPromptAsync("hi", new PromptOptions(), ct)).ConfigureAwait(true);

        events.ShouldBe([new TextDeltaEvent("done"), new CompletedEvent("completed")]);
        session.State.ShouldBe(HarnessState.Ready);
        transport.Sent.Count.ShouldBe(sentBefore + 1);
        RpcTestData.TypeOf(transport.Sent[^1]).ShouldBe("prompt");
        await session.DisposeAsync().ConfigureAwait(true);
    }

    private static FakeRpcTransport Engine()
    {
        var transport = new FakeRpcTransport(line =>
        {
            string command = RpcTestData.TypeOf(line);
            string id = RpcTestData.IdOf(line);
            return command switch
            {
                "negotiate_protocol" => [RpcTestData.Ok(id, command, """{"protocolVersion":2}""")],
                "get_state" => [RpcTestData.Ok(id, command, RpcTestData.StateData)],
                "prompt" =>
                [
                    RpcTestData.Ok(id, "prompt", """{"agentInvoked":true}"""),
                    RpcTestData.TextDelta("done"),
                    RpcTestData.PromptResult(id, "completed"),
                ],
                _ => [],
            };
        });
        transport.Emit(ReadyLine);
        return transport;
    }

    private static async Task<List<HarnessEvent>> CollectAsync(IAsyncEnumerable<HarnessEvent> events)
    {
        var collected = new List<HarnessEvent>();
        IAsyncEnumerator<HarnessEvent> enumerator = events.GetAsyncEnumerator();
        try
        {
            while (await enumerator.MoveNextAsync().ConfigureAwait(false))
            {
                collected.Add(enumerator.Current);
            }
        }
        finally
        {
            await enumerator.DisposeAsync().ConfigureAwait(false);
        }

        return collected;
    }
}
