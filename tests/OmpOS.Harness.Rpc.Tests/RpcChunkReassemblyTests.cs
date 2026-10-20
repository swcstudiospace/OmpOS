using System.Text.Json;
using System.Text.Json.Nodes;
using OmpOS.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Rpc.Tests;

/// <summary>Protocol v2 chunk reassembly: out-of-order delivery, the size caps, and duplicate or inconsistent chunks.</summary>
public sealed class RpcChunkReassemblyTests
{
    private const int ChunkCount = 8;

    /// <summary>A fixed out-of-index-order delivery sequence for eight chunks.</summary>
    private static readonly int[] ShuffledOrder = [5, 2, 7, 0, 3, 6, 1, 4];

    private const string ReadyLine =
        """{"type":"ready","protocolVersion":1,"supportedProtocolVersions":[1,2],"maxFrameBytes":1048576,"maxReassembledFrameBytes":67108864}""";

    /// <summary>
    /// A &gt;1 MiB <c>get_state</c> response split into eight <c>rpc_chunk</c> frames, delivered shuffled, is
    /// reassembled by the session into the original state.
    /// </summary>
    [Fact]
    public async Task ShuffledEightChunkGetStateReassemblesThroughSession()
    {
        var ct = TestContext.Current.CancellationToken;
        FakeRpcTransport transport = ShuffledEngine();
        var session = new OmpRpcSession(transport, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));

        await session.CreateAsync(ct).ConfigureAwait(true);
        JsonElement state = await session.GetStateAsync(ct).ConfigureAwait(true);

        session.AdvertisedCapabilities["model.id"].ShouldBe("big-model");
        state.GetProperty("model").GetProperty("provider").GetString().ShouldBe("test");
        state.GetProperty("padding").GetString()!.Length.ShouldBe(1_100_000);
        await session.DisposeAsync().ConfigureAwait(true);
    }

    /// <summary>Chunks delivered in index order release the logical frame only after the last index arrives.</summary>
    [Fact]
    public void InOrderChunksReleaseOnlyWhenComplete()
    {
        var assembler = new RpcChunkAssembler();
        IReadOnlyList<string> lines = RpcTestData.ChunkLines(RpcTestData.LargeStateResponse("7"), "in-order", ChunkCount);

        for (int i = 0; i < ChunkCount - 1; i++)
        {
            assembler.Accept(lines[i]).ShouldBeNull();
        }

        using JsonDocument? logical = assembler.Accept(lines[ChunkCount - 1]);
        logical.ShouldNotBeNull();
        logical.RootElement.GetProperty("id").GetString().ShouldBe("7");
        logical.RootElement.GetProperty("command").GetString().ShouldBe("get_state");
    }

    /// <summary>Reverse-order delivery also reassembles, because the assembler buffers by index.</summary>
    [Fact]
    public void ReverseOrderChunksReassemble()
    {
        var assembler = new RpcChunkAssembler();
        IReadOnlyList<string> lines = RpcTestData.ChunkLines(RpcTestData.LargeStateResponse("9"), "reverse", ChunkCount);

        JsonDocument? logical = null;
        foreach (string line in lines.Reverse())
        {
            logical?.Dispose();
            logical = assembler.Accept(line);
        }

        logical.ShouldNotBeNull();
        using (logical)
        {
            logical.RootElement.GetProperty("id").GetString().ShouldBe("9");
        }
    }

    /// <summary>A chunk that repeats an index already buffered is malformed.</summary>
    [Fact]
    public void RepeatedIndexIsMalformed()
    {
        var assembler = new RpcChunkAssembler();
        IReadOnlyList<string> lines = RpcTestData.ChunkLines(RpcTestData.LargeStateResponse("1"), "dup", ChunkCount);
        assembler.Accept(lines[3]).ShouldBeNull();

        Should.Throw<MalformedFrameHarnessException>(() => assembler.Accept(lines[3]));
    }

    /// <summary>A later chunk that changes the declared count is malformed.</summary>
    [Fact]
    public void ChangedCountMidFrameIsMalformed()
    {
        var assembler = new RpcChunkAssembler();
        IReadOnlyList<string> lines = RpcTestData.ChunkLines(RpcTestData.LargeStateResponse("1"), "count", ChunkCount);
        assembler.Accept(lines[0]).ShouldBeNull();
        JsonObject altered = JsonNode.Parse(lines[1])!.AsObject();
        altered["count"] = ChunkCount + 1;

        Should.Throw<MalformedFrameHarnessException>(() => assembler.Accept(altered.ToJsonString()));
    }

    /// <summary>A chunk id reused after its frame completed is malformed.</summary>
    [Fact]
    public void ReusedChunkIdAfterCompletionIsMalformed()
    {
        var assembler = new RpcChunkAssembler();
        IReadOnlyList<string> lines = RpcTestData.ChunkLines(RpcTestData.LargeStateResponse("1"), "reuse", ChunkCount);
        foreach (string line in lines)
        {
            assembler.Accept(line)?.Dispose();
        }

        Should.Throw<MalformedFrameHarnessException>(() => assembler.Accept(lines[0]));
    }

    /// <summary>A physical line over 1 MiB is rejected before parsing.</summary>
    [Fact]
    public void OversizedPhysicalLineIsMalformed()
    {
        var assembler = new RpcChunkAssembler();
        string line = new string('a', OmpRpcFrames.MaxPhysicalFrameBytes);

        Should.Throw<MalformedFrameHarnessException>(() => assembler.Accept(line));
    }

    /// <summary>A declared byteLength above the 64 MiB reassembly cap is rejected before any buffering.</summary>
    [Fact]
    public void ByteLengthAboveReassemblyCapIsMalformed()
    {
        var assembler = new RpcChunkAssembler();
        string line = new JsonObject
        {
            ["type"] = "rpc_chunk",
            ["chunkId"] = "cap",
            ["index"] = 0,
            ["count"] = 2,
            ["byteLength"] = OmpRpcFrames.MaxReassembledFrameBytes + 1,
            ["data"] = Convert.ToBase64String(new byte[16]),
        }.ToJsonString();

        Should.Throw<MalformedFrameHarnessException>(() => assembler.Accept(line));
    }

    /// <summary>Chunk data that is not base64 is malformed.</summary>
    [Fact]
    public void InvalidBase64IsMalformed()
    {
        var assembler = new RpcChunkAssembler();
        string line = new JsonObject
        {
            ["type"] = "rpc_chunk",
            ["chunkId"] = "b64",
            ["index"] = 0,
            ["count"] = 2,
            ["byteLength"] = OmpRpcFrames.MaxPhysicalFrameBytes + 1,
            ["data"] = "%%%not-base64%%%",
        }.ToJsonString();

        Should.Throw<MalformedFrameHarnessException>(() => assembler.Accept(line));
    }

    /// <summary>A line that is valid JSON but not an object is malformed.</summary>
    [Fact]
    public void NonObjectFrameIsMalformed()
    {
        var assembler = new RpcChunkAssembler();

        Should.Throw<MalformedFrameHarnessException>(() => assembler.Accept("[1,2,3]"));
    }

    /// <summary>An unrelated frame interleaved with a chunk stream is delivered, and the chunked frame still completes.</summary>
    [Fact]
    public void UnrelatedFrameInterleavedWithChunksIsDelivered()
    {
        var assembler = new RpcChunkAssembler();
        IReadOnlyList<string> lines = RpcTestData.ChunkLines(RpcTestData.LargeStateResponse("3"), "mix", ChunkCount);
        assembler.Accept(lines[0]).ShouldBeNull();

        using JsonDocument? interleaved = assembler.Accept("""{"type":"advisor_cost_changed"}""");
        interleaved.ShouldNotBeNull();
        OmpRpcFrames.ReadType(interleaved.RootElement).ShouldBe("advisor_cost_changed");

        JsonDocument? logical = null;
        for (int i = 1; i < ChunkCount; i++)
        {
            logical?.Dispose();
            logical = assembler.Accept(lines[i]);
        }

        logical.ShouldNotBeNull();
        using (logical)
        {
            logical.RootElement.GetProperty("id").GetString().ShouldBe("3");
        }
    }

    /// <summary>Builds an engine that answers the handshake and serves <c>get_state</c> as eight shuffled chunks.</summary>
    private static FakeRpcTransport ShuffledEngine()
    {
        var transport = new FakeRpcTransport(command =>
        {
            string id = RpcTestData.IdOf(command);
            return RpcTestData.TypeOf(command) switch
            {
                "negotiate_protocol" => [RpcTestData.Ok(id, "negotiate_protocol", """{"protocolVersion":2}""")],
                "get_state" => ShuffledChunks(RpcTestData.LargeStateResponse(id), "state-" + id),
                _ => [],
            };
        });
        transport.Emit(ReadyLine);
        return transport;
    }

    /// <summary>Splits a logical frame into eight chunks and returns them in <see cref="ShuffledOrder"/>.</summary>
    private static string[] ShuffledChunks(string logical, string chunkId)
    {
        IReadOnlyList<string> ordered = RpcTestData.ChunkLines(logical, chunkId, ChunkCount);
        return ShuffledOrder.Select(index => ordered[index]).ToArray();
    }
}
