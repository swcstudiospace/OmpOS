using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Autonogrammer.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Rpc.Tests;

/// <summary>
/// Phase 79 contract tests over the omp 19.1.1 handshake capture frozen under OMP19-protocol-evidence-v1
/// (ledger <c>docs/architecture/omp-19-reverification.md</c>, OQ-520 to OQ-524). The capture exchanged only
/// <c>negotiate_protocol</c>, <c>get_state</c> and an unknown-command probe with the installed binary; no prompt
/// was sent, so no model turn is recorded (<c>live_model_turn: false</c> in the manifest).
/// </summary>
public sealed class Omp19HandshakeContractTests
{
    private const string Stem = "rpc-observed-19.1.1-20261123T202249Z";

    /// <summary>
    /// The 19.1.1 <c>ready</c> frame keeps the 18.12.x protocol versions and caps the chunk assembler codes
    /// against, and adds <c>supportsBashExcludeFromContext</c>, which the 18.12.3 capture does not carry.
    /// </summary>
    [Fact]
    public void ReadyFrame19KeepsCodedCapsAndAddsBashExcludeFlag()
    {
        string readyLine = InLines()[0];
        using JsonDocument document = JsonDocument.Parse(readyLine);

        ReadyFrame ready = document.RootElement.Deserialize(OmpRpcJsonContext.Default.ReadyFrame)!;

        ready.Type.ShouldBe("ready");
        ready.ProtocolVersion.ShouldBe(1);
        ready.SupportedProtocolVersions.ShouldBe([1, 2]);
        ready.MaxFrameBytes.ShouldBe(OmpRpcFrames.MaxPhysicalFrameBytes);
        ready.MaxReassembledFrameBytes.ShouldBe(OmpRpcFrames.MaxReassembledFrameBytes);

        document.RootElement.GetProperty("supportsBashExcludeFromContext").GetBoolean().ShouldBeTrue();

        using JsonDocument oldDocument = JsonDocument.Parse(RpcTestData.Fixture()[0]);
        oldDocument.RootElement.TryGetProperty("supportsBashExcludeFromContext", out _).ShouldBeFalse();
    }

    /// <summary>
    /// On 19.1.1, <c>negotiate_protocol</c> confirms v2, <c>get_state</c> answers with the 18.12.x state keys plus
    /// the additive thinking-level keys, and an unknown command is rejected in the same envelope as on 18.12.x.
    /// </summary>
    [Fact]
    public void HandshakeCommands19AnswerWithRecordedShapes()
    {
        IReadOnlyList<string> inLines = InLines();

        JsonNode negotiated = Response(inLines, "negotiate_protocol");
        negotiated["success"]!.GetValue<bool>().ShouldBeTrue();
        negotiated["data"]!["protocolVersion"]!.GetValue<int>().ShouldBe(OmpRpcFrames.NegotiatedProtocolVersion);

        JsonNode state = Response(inLines, "get_state");
        state["success"]!.GetValue<bool>().ShouldBeTrue();
        JsonNode data = state["data"]!;
        data["model"]!["id"]!.GetValue<string>().ShouldBe("claude-opus-4-5");
        data["model"]!["provider"]!.GetValue<string>().ShouldBe("anthropic");
        data["steeringMode"]!.GetValue<string>().ShouldBe("one-at-a-time");
        data["followUpMode"]!.GetValue<string>().ShouldBe("one-at-a-time");

        // Additive on 19.1.1: the 18.12.x get_state capture has none of these keys.
        data["configuredThinkingLevel"].ShouldNotBeNull();
        data["thinkingLevels"].ShouldNotBeNull();
        data["slowModeScope"]!.GetValue<string>().ShouldBe("global");
        JsonNode oldState = Response(RpcTestData.Fixture(), "get_state");
        oldState["data"]!["configuredThinkingLevel"].ShouldBeNull();
        oldState["data"]!["thinkingLevels"].ShouldBeNull();
        oldState["data"]!["slowModeScope"].ShouldBeNull();

        // The non-synthetic prompt/tool text of get_state is redacted out of the frozen fixture (rule R4).
        data["systemPrompt"]!.GetValue<string>().ShouldBe("<redacted>");
        data["dumpTools"]!.GetValue<string>().ShouldBe("<redacted>");

        JsonNode unknown = Response(inLines, "nope");
        unknown["success"]!.GetValue<bool>().ShouldBeFalse();
        unknown["error"]!.GetValue<string>().ShouldBe("Unknown command: nope");
    }

    /// <summary>
    /// A session completes the v2 handshake against the 19.1.1 frames exactly as against the 18.12.x capture:
    /// the unknown <c>ready</c> field does not fault it, capabilities come from the 19.1.1 <c>get_state</c>, and an
    /// unknown command surfaces as a typed <see cref="RpcCommandException"/> without faulting the session.
    /// </summary>
    [Fact]
    public async Task SessionReplays19HandshakeGetStateAndUnknownCommand()
    {
        var ct = TestContext.Current.CancellationToken;
        FakeRpcTransport transport = ReplayTransport(InLines());
        var session = new OmpRpcSession(transport, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));

        await session.CreateAsync(ct).ConfigureAwait(true);

        session.State.ShouldBe(HarnessState.Ready);
        transport.Sent.Select(RpcTestData.TypeOf).ShouldBe(["negotiate_protocol", "get_state"]);
        session.AdvertisedCapabilities["model.id"].ShouldBe("claude-opus-4-5");
        session.AdvertisedCapabilities["model.provider"].ShouldBe("anthropic");

        RpcCommandException failure = await Should.ThrowAsync<RpcCommandException>(
            () => session.ExecuteAsync(id => new UnknownCommandProbe("nope") { Id = id }, ct)).ConfigureAwait(true);
        failure.Command.ShouldBe("nope");
        failure.Message.ShouldBe("Unknown command: nope");

        session.State.ShouldBe(HarnessState.Ready);
        await session.DisposeAsync().ConfigureAwait(true);
    }

    /// <summary>The sidecar manifest digests the committed frame bytes (OMP19-protocol-evidence-v1 rules V1 to V4).</summary>
    [Fact]
    public void ManifestDigestsTheFrozenFrameBytes()
    {
        byte[] frameBytes = File.ReadAllBytes(FramesPath(Stem + ".ndjson"));
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(FramesPath(Stem + ".manifest.json")));
        JsonElement root = manifest.RootElement;

        root.GetProperty("contract_id").GetString().ShouldBe("OMP19-protocol-evidence");
        root.GetProperty("contract_version").GetString().ShouldBe("1.0.0");
        root.GetProperty("omp_version").GetString().ShouldBe("19.1.1");
        root.GetProperty("evidence_label").GetString().ShouldBe("observed-19.1.1");
        root.GetProperty("live_model_turn").GetBoolean().ShouldBeFalse();
        root.GetProperty("frames_bytes").GetInt32().ShouldBe(frameBytes.Length);
        root.GetProperty("frames_sha256").GetString()
            .ShouldBe("sha256:" + Convert.ToHexStringLower(SHA256.HashData(frameBytes)));

        string[] lines = File.ReadAllLines(FramesPath(Stem + ".ndjson"));
        root.GetProperty("frame_count").GetInt32().ShouldBe(lines.Length);
        JsonElement[] frames = [.. root.GetProperty("frames").EnumerateArray()];
        frames.Length.ShouldBe(lines.Length);
        for (int i = 0; i < frames.Length; i++)
        {
            byte[] lineBytes = Encoding.UTF8.GetBytes(lines[i]);
            frames[i].GetProperty("seq").GetInt32().ShouldBe(i + 1);
            frames[i].GetProperty("byte_length").GetInt32().ShouldBe(lineBytes.Length);
            frames[i].GetProperty("line_sha256").GetString()
                .ShouldBe("sha256:" + Convert.ToHexStringLower(SHA256.HashData(lineBytes)));
            frames[i].GetProperty("frame_type").GetString()
                .ShouldBe(JsonNode.Parse(lines[i])!["type"]!.GetValue<string>());
        }

        string[] ledgerRefs = [.. root.GetProperty("ledger_refs").EnumerateArray().Select(static e => e.GetString()!)];
        ledgerRefs.ShouldBe(["OQ-520", "OQ-521", "OQ-522", "OQ-523", "OQ-524"]);
    }

    /// <summary>
    /// The version probe parses the installed 19.1.1 string and the pin now accepts exactly the 19.1 line:
    /// Phase 80 moved it on this ledger's evidence (OQ-524 closed, "Pin decision (D-03)"), so 18.12.6 and
    /// 19.2.0 fall outside again on either side.
    /// </summary>
    [Fact]
    public void Version1911ParsesAndIsInsideThePinnedLine()
    {
        OmpVersion version = OmpVersion.Parse("omp/19.1.1");

        version.ShouldBe(new OmpVersion(19, 1, 1));
        version.IsPinnedRelease.ShouldBeTrue();
        OmpVersion.PinnedMajor.ShouldBe(19);
        OmpVersion.PinnedMinor.ShouldBe(1);
        new OmpVersion(18, 12, 6).IsPinnedRelease.ShouldBeFalse();
        new OmpVersion(19, 0, 0).IsPinnedRelease.ShouldBeFalse();
        new OmpVersion(19, 2, 0).IsPinnedRelease.ShouldBeFalse();
    }

    private static string FramesPath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "TestData", "frames", fileName);

    /// <summary>Reads the capture lines omp wrote (manifest direction <c>in</c>), in file order.</summary>
    private static IReadOnlyList<string> InLines()
    {
        string[] lines = File.ReadAllLines(FramesPath(Stem + ".ndjson"));
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(FramesPath(Stem + ".manifest.json")));
        return [.. manifest.RootElement.GetProperty("frames").EnumerateArray()
            .Where(static frame => string.Equals(frame.GetProperty("direction").GetString(), "in", StringComparison.Ordinal))
            .Select(frame => lines[frame.GetProperty("seq").GetInt32() - 1])];
    }

    private static JsonNode Response(IReadOnlyList<string> lines, string command) =>
        lines.Select(static line => JsonNode.Parse(line)!)
            .Single(node => string.Equals(node["command"]?.GetValue<string>(), command, StringComparison.Ordinal));

    private static FakeRpcTransport ReplayTransport(IReadOnlyList<string> inLines)
    {
        var pending = inLines.Where(IsResponse).ToList();
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

        foreach (string line in inLines.Where(line => !IsResponse(line)))
        {
            transport.Emit(line);
        }

        return transport;
    }

    private static bool IsResponse(string line) =>
        string.Equals(JsonNode.Parse(line)!["type"]?.GetValue<string>(), OmpRpcFrames.ResponseType, StringComparison.Ordinal);

    private static string? CommandOf(string line) => JsonNode.Parse(line)!["command"]?.GetValue<string>();
}
