using System.Diagnostics.CodeAnalysis;
using OmpOS.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Rpc.Tests;

/// <summary>A stderr flood neither blocks the stdout protocol stream nor grows the tail past its cap.</summary>
public sealed class RpcStderrFloodTests
{
    private const string ReadyLine =
        """{"type":"ready","protocolVersion":1,"supportedProtocolVersions":[1,2],"maxFrameBytes":1048576,"maxReassembledFrameBytes":67108864}""";

    /// <summary>
    /// Five hundred stderr lines before the handshake plus a live trickle during the prompt: the prompt still
    /// streams to completion and the tail keeps exactly the last 200 lines, oldest first.
    /// </summary>
    [Fact]
    [SuppressMessage(
        "Usage",
        "CA2000",
        Justification = "The OmpRpcSession takes ownership of the transport and disposes it in DisposeAsync.")]
    public async Task StderrFloodLeavesProtocolStreamAndBoundedTailIntact()
    {
        var ct = TestContext.Current.CancellationToken;
        FakeRpcTransport transport = Engine(
            (command, id) => command switch
            {
                "prompt" =>
                [
                    RpcTestData.Ok(id, "prompt", """{"agentInvoked":true}"""),
                    RpcTestData.TextDelta("done"),
                    RpcTestData.PromptResult(id, "completed"),
                ],
                _ => null,
            });
        for (int i = 0; i < 500; i++)
        {
            transport.EmitStderr($"err-{i}");
        }

        var session = new OmpRpcSession(transport, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
        await session.CreateAsync(ct).ConfigureAwait(true);
        for (int i = 500; i < 510; i++)
        {
            transport.EmitStderr($"err-{i}");
        }

        List<HarnessEvent> events = await CollectAsync(session.SubmitPromptAsync("hi", new PromptOptions(), ct))
            .ConfigureAwait(true);

        events.ShouldBe([new TextDeltaEvent("done"), new CompletedEvent("completed")]);
        transport.RecentStderr.Count.ShouldBe(FakeRpcTransport.StderrTailCap);
        transport.RecentStderr[0].ShouldBe("err-310");
        transport.RecentStderr[^1].ShouldBe("err-509");
        await session.DisposeAsync().ConfigureAwait(true);
    }

    /// <summary>
    /// Starts a session against a scripted engine. The engine answers the handshake itself and every other command
    /// with <paramref name="scripted"/>.
    /// </summary>
    private static FakeRpcTransport Engine(Func<string, string, IEnumerable<string>?> scripted)
    {
        var transport = new FakeRpcTransport(line =>
        {
            string command = RpcTestData.TypeOf(line);
            string id = RpcTestData.IdOf(line);
            return Handshake(command, id) ?? scripted(command, id) ?? Array.Empty<string>();
        });
        transport.Emit(ReadyLine);
        return transport;
    }

    private static IEnumerable<string>? Handshake(string command, string id) => command switch
    {
        "negotiate_protocol" => [RpcTestData.Ok(id, command, """{"protocolVersion":2}""")],
        "get_state" => [RpcTestData.Ok(id, command, RpcTestData.StateData)],
        _ => null,
    };

    private static async Task<List<HarnessEvent>> CollectAsync(IAsyncEnumerable<HarnessEvent> events)
    {
        var collected = new List<HarnessEvent>();
        IAsyncEnumerator<HarnessEvent> enumerator = events.GetAsyncEnumerator();
        try
        {
            while (await enumerator.MoveNextAsync().ConfigureAwait(true))
            {
                collected.Add(enumerator.Current);
            }
        }
        finally
        {
            await enumerator.DisposeAsync().ConfigureAwait(true);
        }

        return collected;
    }
}
