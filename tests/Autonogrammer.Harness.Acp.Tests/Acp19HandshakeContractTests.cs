using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Autonogrammer.Harness.Acp.AcpProtocol;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Acp.Tests;

/// <summary>
/// Phase 79 contract tests over the omp 19.1.1 ACP <c>initialize</c> capture frozen under
/// OMP19-protocol-evidence-v1 (ledger <c>docs/architecture/omp-19-reverification.md</c>, row ACP-INITIALIZE).
/// Only <c>initialize</c> was sent to the installed binary; no <c>authenticate</c>, no session, no prompt, so no
/// model turn is recorded (<c>live_model_turn: false</c> in the manifest).
/// </summary>
public sealed class Acp19HandshakeContractTests
{
    private const string Stem = "acp-observed-19.1.1-20261123T202251Z";

    /// <summary>
    /// The 19.1.1 <c>initialize</c> result keeps protocol v1, the single <c>agent</c> auth method the client is
    /// coded to send, and the 18.12.x capability groups; it reports version 19.1.1 (the 18.12.3 capture says
    /// 18.12.3) and adds the <c>_meta.steering.supported</c> advertisement the old capture lacks.
    /// </summary>
    [Fact]
    public void InitializeResult19KeepsAuthMethodAndCapabilitiesAndAddsSteeringMeta()
    {
        string[] lines = AcpFixtures.Lines(Stem + ".ndjson");
        lines.Length.ShouldBe(2);

        JsonNode request = JsonNode.Parse(lines[0])!;
        request["method"]!.GetValue<string>().ShouldBe("initialize");
        request["params"]!["protocolVersion"]!.GetValue<int>().ShouldBe(1);

        using JsonDocument document = JsonDocument.Parse(lines[1]);
        AcpFrames.InitializeResult result = document.RootElement.GetProperty("result")
            .Deserialize(AcpJsonContext.Default.InitializeResult)!;

        result.ProtocolVersion.ShouldBe(1);
        result.AgentInfo.ShouldNotBeNull();
        result.AgentInfo.Name.ShouldBe("omp");
        result.AgentInfo.Version.ShouldBe("19.1.1");
        result.AuthMethods.ShouldNotBeNull();
        result.AuthMethods.Count.ShouldBe(1);
        result.AuthMethods[0].Id.ShouldBe(AcpClientOptions.OmpAuthMethodId);
        result.AgentCapabilities.LoadSession.ShouldBeTrue();
        result.AgentCapabilities.McpCapabilities.ShouldNotBeNull();
        result.AgentCapabilities.McpCapabilities.Http.ShouldBeTrue();
        result.AgentCapabilities.McpCapabilities.Sse.ShouldBeTrue();

        result.Meta.ShouldNotBeNull();
        result.Meta.Value.GetProperty("steering").GetProperty("supported").GetBoolean().ShouldBeTrue();

        JsonNode oldResult = JsonNode.Parse(AcpFixtures.Lines("acp-out.ndjson")[0])!["result"]!;
        oldResult["agentInfo"]!["version"]!.GetValue<string>().ShouldBe("18.12.3");
        oldResult["_meta"].ShouldBeNull();
    }

    /// <summary>The sidecar manifest digests the committed frame bytes (OMP19-protocol-evidence-v1 rules V1 to V4).</summary>
    [Fact]
    public void ManifestDigestsTheFrozenFrameBytes()
    {
        byte[] frameBytes = File.ReadAllBytes(FramesPath(Stem + ".ndjson"));
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(FramesPath(Stem + ".manifest.json")));
        JsonElement root = manifest.RootElement;

        root.GetProperty("contract_id").GetString().ShouldBe("OMP19-protocol-evidence");
        root.GetProperty("transport").GetString().ShouldBe("acp");
        root.GetProperty("omp_version").GetString().ShouldBe("19.1.1");
        root.GetProperty("evidence_label").GetString().ShouldBe("observed-19.1.1");
        root.GetProperty("live_model_turn").GetBoolean().ShouldBeFalse();
        root.GetProperty("frames_bytes").GetInt32().ShouldBe(frameBytes.Length);
        root.GetProperty("frames_sha256").GetString()
            .ShouldBe("sha256:" + Convert.ToHexStringLower(SHA256.HashData(frameBytes)));

        string[] lines = AcpFixtures.Lines(Stem + ".ndjson");
        root.GetProperty("frame_count").GetInt32().ShouldBe(lines.Length);
        JsonElement[] frames = [.. root.GetProperty("frames").EnumerateArray()];
        frames.Length.ShouldBe(lines.Length);
        frames[0].GetProperty("direction").GetString().ShouldBe("out");
        frames[0].GetProperty("frame_type").GetString().ShouldBe("initialize");
        frames[1].GetProperty("direction").GetString().ShouldBe("in");
        frames[1].GetProperty("frame_type").GetString().ShouldBe("result:1");
        for (int i = 0; i < frames.Length; i++)
        {
            byte[] lineBytes = Encoding.UTF8.GetBytes(lines[i]);
            frames[i].GetProperty("seq").GetInt32().ShouldBe(i + 1);
            frames[i].GetProperty("byte_length").GetInt32().ShouldBe(lineBytes.Length);
            frames[i].GetProperty("line_sha256").GetString()
                .ShouldBe("sha256:" + Convert.ToHexStringLower(SHA256.HashData(lineBytes)));
        }

        string[] ledgerRefs = [.. root.GetProperty("ledger_refs").EnumerateArray().Select(static e => e.GetString()!)];
        ledgerRefs.ShouldBe(["ACP-INITIALIZE"]);
    }

    private static string FramesPath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "TestData", "frames", fileName);
}
