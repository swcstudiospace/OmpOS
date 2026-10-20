using System.Text.Json;
using System.Text.Json.Nodes;
using OmpOS.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Rpc.Tests;

/// <summary>Replays the committed omp 18.12.3 RPC capture through the parser and through a session.</summary>
public sealed class RpcFrameReplayTests
{
    /// <summary>The capture's first frame is the ready handshake with the documented limits.</summary>
    [Fact]
    public void ReadyFrameParsesWithCapturedLimits()
    {
        using JsonDocument document = JsonDocument.Parse(RpcTestData.Fixture()[0]);

        ReadyFrame ready = document.RootElement.Deserialize(OmpRpcJsonContext.Default.ReadyFrame)!;

        ready.Type.ShouldBe("ready");
        ready.ProtocolVersion.ShouldBe(1);
        ready.SupportedProtocolVersions.ShouldBe([1, 2]);
        ready.MaxFrameBytes.ShouldBe(1_048_576);
        ready.MaxReassembledFrameBytes.ShouldBe(67_108_864);
    }

    /// <summary>Every captured line passes the envelope check, and unsolicited frames keep their discriminators.</summary>
    [Fact]
    public void EveryCapturedLineClassifiesWithItsDiscriminator()
    {
        var assembler = new RpcChunkAssembler();
        var types = new List<string?>();

        foreach (string line in RpcTestData.Fixture())
        {
            using JsonDocument? document = assembler.Accept(line);
            document.ShouldNotBeNull();
            types.Add(OmpRpcFrames.ReadType(document.RootElement));
        }

        types.ShouldBe(
        [
            "ready",
            "advisor_cost_changed",
            "available_commands_update",
            "response",
            "response",
            "response",
        ]);
    }

    /// <summary>
    /// The handshake and state responses correlate with the ids the session sends, unsolicited frames do not fault the
    /// session, and an unknown command surfaces as a typed <see cref="RpcCommandException"/> without faulting it.
    /// </summary>
    [Fact]
    public async Task SessionReplaysHandshakeGetStateAndUnknownCommandFromCapture()
    {
        var ct = TestContext.Current.CancellationToken;
        FakeRpcTransport transport = ReplayTransport(RpcTestData.Fixture());
        var session = new OmpRpcSession(transport, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));

        await session.CreateAsync(ct).ConfigureAwait(true);

        session.State.ShouldBe(HarnessState.Ready);
        transport.Sent.Select(RpcTestData.TypeOf).ShouldBe(["negotiate_protocol", "get_state"]);
        transport.Sent[0].ShouldContain("\"protocolVersion\":2");
        session.AdvertisedCapabilities["model.id"].ShouldBe("glm-5.3-flash");
        session.AdvertisedCapabilities["model.provider"].ShouldBe("zai");


        RpcCommandException failure = await Should.ThrowAsync<RpcCommandException>(
            () => session.ExecuteAsync(id => new UnknownCommandProbe("nope") { Id = id }, ct)).ConfigureAwait(true);
        failure.Command.ShouldBe("nope");
        failure.Message.ShouldBe("Unknown command: nope");

        session.State.ShouldBe(HarnessState.Ready);
        await session.DisposeAsync().ConfigureAwait(true);
    }

    /// <summary>
    /// Builds a transport that emits the capture's unsolicited frames up front and answers each written command with
    /// the captured response of the same command type, rewritten to carry the session's id.
    /// </summary>
    private static FakeRpcTransport ReplayTransport(IReadOnlyList<string> fixture)
    {
        var pending = fixture.Where(IsResponse).ToList();
        var transport = new FakeRpcTransport(command =>
        {
            string type = RpcTestData.TypeOf(command);
            int index = pending.FindIndex(line => string.Equals(CommandOf(line), type, StringComparison.Ordinal));
            if (index < 0)
            {
                return [];
            }

            string reply = RpcTestData.WithId(pending[index], RpcTestData.IdOf(command));
            pending.RemoveAt(index);
            return [reply];
        });

        foreach (string line in fixture.Where(line => !IsResponse(line)))
        {
            transport.Emit(line);
        }

        return transport;
    }

    private static bool IsResponse(string line) =>
        string.Equals(JsonNode.Parse(line)!["type"]?.GetValue<string>(), OmpRpcFrames.ResponseType, StringComparison.Ordinal);

    private static string? CommandOf(string line) => JsonNode.Parse(line)!["command"]?.GetValue<string>();
}
