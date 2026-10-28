using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using Autonogrammer.Harness.Acp.AcpProtocol;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Acp.Tests;

/// <summary>Replays the frames captured from a live <c>omp acp</c> through the client, with no process involved.</summary>
[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "The session under test owns and disposes the scripted transport.")]
public sealed class AcpFrameReplayTests
{
    private static readonly FrozenDictionary<string, string> ExpectedCapabilities = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["loadSession"] = "true",
        ["mcpCapabilities.http"] = "true",
        ["mcpCapabilities.sse"] = "true",
        ["promptCapabilities.embeddedContext"] = "true",
        ["promptCapabilities.image"] = "true",
        ["sessionCapabilities.list"] = "true",
        ["sessionCapabilities.fork"] = "true",
        ["sessionCapabilities.resume"] = "true",
        ["sessionCapabilities.close"] = "true",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>The client's request frames for initialize, authenticate and session/new are byte-identical to the captured requests.</summary>
    [Fact]
    public async Task HandshakeRequestsAreByteIdenticalToCapture()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = new ScriptedAcpTransport(AcpFixtures.Reply);
        var session = new AcpAgentSession(transport, new AcpClientOptions { WorkingDirectory = AcpFixtures.CapturedCwd, AuthMethodId = "agent" });
        await using var sessionScope = session.ConfigureAwait(true);

        await session.CreateAsync(ct).ConfigureAwait(true);

        transport.Sent.Select(AcpFixtures.Text).ToArray().ShouldBe(AcpFixtures.Lines("acp-in.ndjson"));
    }

    /// <summary>The captured initialize result yields the agent identity and the flattened capability set.</summary>
    [Fact]
    public async Task CapturedInitializeResultPopulatesAgentInfoAndCapabilities()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = new ScriptedAcpTransport(AcpFixtures.Reply);
        var session = new AcpAgentSession(transport, new AcpClientOptions { WorkingDirectory = AcpFixtures.CapturedCwd, AuthMethodId = "agent" });
        await using var sessionScope = session.ConfigureAwait(true);

        await session.CreateAsync(ct).ConfigureAwait(true);

        session.AgentInfo.ShouldNotBeNull();
        session.AgentInfo.Name.ShouldBe("omp");
        session.AgentInfo.Version.ShouldBe("18.12.3");
        session.AdvertisedCapabilities.Count.ShouldBe(ExpectedCapabilities.Count);
        foreach (var (key, value) in ExpectedCapabilities)
        {
            session.AdvertisedCapabilities[key].ShouldBe(value, key);
        }

        session.SessionId.ShouldBe(AcpFixtures.CapturedSessionId);
    }

    /// <summary>The captured session/new result keeps <c>configOptions</c> opaque: it survives a decode and re-encode unchanged.</summary>
    [Fact]
    public void CapturedSessionNewResultRoundTripsConfigOptions()
    {
        var captured = JsonNode.Parse(AcpFixtures.Lines("acp-out.ndjson")[1])!["result"]!;

        var decoded = JsonSerializer.Deserialize(captured.ToJsonString(), AcpJsonContext.Default.NewSessionResult);

        decoded.ShouldNotBeNull();
        decoded.SessionId.ShouldBe(AcpFixtures.CapturedSessionId);
        decoded.ConfigOptions.ShouldNotBeNull();
        decoded.ConfigOptions.Value.ValueKind.ShouldBe(JsonValueKind.Array);

        var reencoded = JsonSerializer.SerializeToNode(decoded, AcpJsonContext.Default.NewSessionResult);

        reencoded.ShouldNotBeNull();
        JsonNode.DeepEquals(reencoded["configOptions"], captured["configOptions"]).ShouldBeTrue();
        reencoded["sessionId"]!.GetValue<string>().ShouldBe(AcpFixtures.CapturedSessionId);
    }

    /// <summary>Every captured response decodes as a response envelope with an id and a result.</summary>
    [Fact]
    public void CapturedResponsesDecodeAsResponseEnvelopes()
    {
        foreach (var line in AcpFixtures.Lines("acp-out.ndjson"))
        {
            var inbound = JsonSerializer.Deserialize(line, AcpJsonContext.Default.Inbound);

            inbound.ShouldNotBeNull();
            inbound.Method.ShouldBeNull();
            inbound.Id.ShouldNotBeNull();
            inbound.Result.ShouldNotBeNull();
            inbound.Error.ShouldBeNull();
        }
    }

    /// <summary>Requests are emitted with <c>jsonrpc</c> first, then <c>id</c>, <c>method</c> and <c>params</c>.</summary>
    [Fact]
    public void RequestEnvelopeKeysAreOrderedAsOnTheWire()
    {
        var frame = JsonSerializer.Serialize(
            new AcpFrames.Request<AcpFrames.CloseSessionParams>(9, "session/close", new AcpFrames.CloseSessionParams("sid")),
            AcpJsonContext.Default.RequestCloseSessionParams);

        frame.ShouldBe("{\"jsonrpc\":\"2.0\",\"id\":9,\"method\":\"session/close\",\"params\":{\"sessionId\":\"sid\"}}");
    }

    /// <summary>A text-only prompt serializes its content blocks the way the wire expects.</summary>
    [Fact]
    public void PromptParamsSerializeTextBlocks()
    {
        var frame = JsonSerializer.Serialize(
            new AcpFrames.Request<AcpFrames.PromptParams>(3, "session/prompt", new AcpFrames.PromptParams("sid", [new AcpFrames.ContentBlock { Text = "hi" }])),
            AcpJsonContext.Default.RequestPromptParams);

        frame.ShouldBe("{\"jsonrpc\":\"2.0\",\"id\":3,\"method\":\"session/prompt\",\"params\":{\"sessionId\":\"sid\",\"prompt\":[{\"type\":\"text\",\"text\":\"hi\"}]}}");
    }
}
