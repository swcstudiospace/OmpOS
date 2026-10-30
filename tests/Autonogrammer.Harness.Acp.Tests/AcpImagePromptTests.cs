using System.Text.Json.Nodes;
using Autonogrammer.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Acp.Tests;

/// <summary>
/// Images through a real <see cref="AcpAgentSession"/> over a scripted omp: <c>initialize</c> says whether images are accepted (the captured reply of omp 18.12.3 says true), a prompt with images
/// carries image blocks with the base64 bytes and the media type next to the text, and a session whose engine refuses images sends nothing.
/// </summary>
public sealed class AcpImagePromptTests
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3];

    private static IEnumerable<byte[]> Script(byte[] frame, JsonObject[] seen, string? promptCapabilities)
    {
        var method = AcpFixtures.MethodOf(frame);
        if (string.Equals(method, "initialize", StringComparison.Ordinal) && promptCapabilities is not null)
        {
            var reply = JsonNode.Parse(AcpFixtures.Reply(frame).Single())!;
            var capabilities = reply["result"]!["agentCapabilities"]!.AsObject();
            _ = capabilities.Remove("promptCapabilities");
            if (promptCapabilities.Length > 0)
            {
                capabilities["promptCapabilities"] = JsonNode.Parse(promptCapabilities);
            }

            return [AcpFixtures.Utf8(reply.ToJsonString())];
        }

        if (!string.Equals(method, "session/prompt", StringComparison.Ordinal))
        {
            return AcpFixtures.Reply(frame);
        }

        seen[0] = JsonNode.Parse(frame)!.AsObject();
        return [AcpFixtures.Utf8($"{{\"jsonrpc\":\"2.0\",\"id\":{AcpFixtures.IdOf(frame)},\"result\":{{\"stopReason\":\"end_turn\"}}}}")];
    }

    private static async Task<(AcpAgentSession Session, ScriptedAcpTransport Transport)> OpenAsync(JsonObject[] seen, string? promptCapabilities, CancellationToken ct)
    {
        var transport = new ScriptedAcpTransport(frame => Script(frame, seen, promptCapabilities));
        var session = new AcpAgentSession(transport, new AcpClientOptions { WorkingDirectory = AcpFixtures.CapturedCwd, AuthMethodId = "agent" });
        await session.CreateAsync(ct).ConfigureAwait(true);
        return (session, transport);
    }

    private static async Task<List<HarnessEvent>> SubmitAsync(AcpAgentSession session, string text, PromptOptions options, CancellationToken ct)
    {
        var events = new List<HarnessEvent>();
        await foreach (var harnessEvent in session.SubmitPromptAsync(text, options, ct).WithCancellation(ct).ConfigureAwait(true))
        {
            events.Add(harnessEvent);
        }

        return events;
    }

    /// <summary>The session does not know before the handshake; the captured initialize reply of omp advertises image: true, and the session reads it.</summary>
    [Fact]
    public async Task TheCapturedInitializeReplyOfOmpAdvertisesImages()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = new ScriptedAcpTransport(AcpFixtures.Reply);
        await using var transportScope = transport.ConfigureAwait(true);
        var session = new AcpAgentSession(transport, new AcpClientOptions { WorkingDirectory = AcpFixtures.CapturedCwd, AuthMethodId = "agent" });
        await using var sessionScope = session.ConfigureAwait(true);

        session.SupportsImages.ShouldBeNull("nothing is known before initialize");
        await session.CreateAsync(ct).ConfigureAwait(true);

        session.SupportsImages.ShouldBe(true);
        ((IOmpModelControl)session).SupportsImages.ShouldBe(true, "the composer reads it through the model control");
    }

    /// <summary>An engine that says image false, or has no promptCapabilities group, does not take images.</summary>
    [Theory]
    [InlineData("{\"embeddedContext\":true,\"image\":false}")]
    [InlineData("")]
    public async Task AnEngineThatDoesNotAdvertiseImagesIsReadAsNo(string promptCapabilities)
    {
        var (session, transport) = await OpenAsync(new JsonObject[1], promptCapabilities, TestContext.Current.CancellationToken).ConfigureAwait(true);
        await using var transportScope = transport.ConfigureAwait(true);
        await using var sessionScope = session.ConfigureAwait(true);

        session.SupportsImages.ShouldBe(false);
    }

    /// <summary>Text, an embedded mention and an image travel in that order; the image block holds the base64 bytes and the media type.</summary>
    [Fact]
    public async Task AnImageIsSentAsAnImageBlockWithBase64AndTheMediaType()
    {
        var seen = new JsonObject[1];
        var ct = TestContext.Current.CancellationToken;
        var (session, transport) = await OpenAsync(seen, null, ct).ConfigureAwait(true);
        await using var transportScope = transport.ConfigureAwait(true);
        await using var sessionScope = session.ConfigureAwait(true);
        var options = new PromptOptions
        {
            Resources = [new PromptResource(new Uri("file:///work/a.txt"), "text/plain", "contents of a", "a.txt", 13, false)],
            Images = [new PromptImage("shot.png", "image/png", Png)],
        };

        var events = await SubmitAsync(session, "what is wrong here?", options, ct).ConfigureAwait(true);

        events.ShouldContain(new CompletedEvent("end_turn"));
        var blocks = seen[0]["params"]!["prompt"]!.AsArray();
        blocks.Select(block => block!["type"]!.GetValue<string>()).ShouldBe(["resource", "text", "image"]);
        blocks[1]!["text"]!.GetValue<string>().ShouldBe("what is wrong here?");
        blocks[2]!["data"]!.GetValue<string>().ShouldBe(Convert.ToBase64String(Png));
        blocks[2]!["mimeType"]!.GetValue<string>().ShouldBe("image/png");
    }

    /// <summary>A message with an image and no words is one image block.</summary>
    [Fact]
    public async Task AnImageWithoutWordsIsOnlyAnImageBlock()
    {
        var seen = new JsonObject[1];
        var ct = TestContext.Current.CancellationToken;
        var (session, transport) = await OpenAsync(seen, null, ct).ConfigureAwait(true);
        await using var transportScope = transport.ConfigureAwait(true);
        await using var sessionScope = session.ConfigureAwait(true);

        _ = await SubmitAsync(session, string.Empty, new PromptOptions { Images = [new PromptImage("a.jpg", "image/jpeg", new byte[] { 0xFF, 0xD8, 0xFF, 7 })] }, ct).ConfigureAwait(true);

        var blocks = seen[0]["params"]!["prompt"]!.AsArray();
        blocks.Count.ShouldBe(1);
        blocks[0]!["type"]!.GetValue<string>().ShouldBe("image");
        blocks[0]!["mimeType"]!.GetValue<string>().ShouldBe("image/jpeg");
        blocks[0]!["data"]!.GetValue<string>().ShouldBe("/9j/Bw==");
    }

    /// <summary>A prompt without images is unchanged: one text block.</summary>
    [Fact]
    public async Task APromptWithoutImagesStaysOneTextBlock()
    {
        var seen = new JsonObject[1];
        var ct = TestContext.Current.CancellationToken;
        var (session, transport) = await OpenAsync(seen, null, ct).ConfigureAwait(true);
        await using var transportScope = transport.ConfigureAwait(true);
        await using var sessionScope = session.ConfigureAwait(true);

        _ = await SubmitAsync(session, "plain", new PromptOptions(), ct).ConfigureAwait(true);

        seen[0]["params"]!["prompt"]!.AsArray().Count.ShouldBe(1);
    }

    /// <summary>When the engine advertises image false a prompt with images is refused before anything is sent; a prompt without images still goes through.</summary>
    [Fact]
    public async Task APromptWithImagesIsRefusedWhenTheEngineSaysImageFalse()
    {
        var seen = new JsonObject[1];
        var ct = TestContext.Current.CancellationToken;
        var (session, transport) = await OpenAsync(seen, "{\"embeddedContext\":true,\"image\":false}", ct).ConfigureAwait(true);
        await using var transportScope = transport.ConfigureAwait(true);
        await using var sessionScope = session.ConfigureAwait(true);

        var refused = await Should.ThrowAsync<NotSupportedException>(() => SubmitAsync(session, "see this", new PromptOptions { Images = [new PromptImage("a.png", "image/png", Png)] }, ct)).ConfigureAwait(true);

        refused.Message.ShouldContain("promptCapabilities.image is false", Case.Sensitive);
        transport.Sent.Select(AcpFixtures.MethodOf).ShouldNotContain("session/prompt", StringComparer.Ordinal);
        seen[0].ShouldBeNull();
        _ = await SubmitAsync(session, "no image", new PromptOptions(), ct).ConfigureAwait(true);
        seen[0].ShouldNotBeNull();
    }
}
