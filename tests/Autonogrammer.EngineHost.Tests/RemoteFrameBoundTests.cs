using System.Net;
using System.Text;
using Autonogrammer.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace Autonogrammer.EngineHost.Tests;

/// <summary>
/// Remote SSE frames are read under the 1 MiB transport limit: a physical line over the cap is refused, accumulated
/// frame data over the cap is refused, and a frame under the cap is delivered unchanged.
/// </summary>
public sealed class RemoteFrameBoundTests
{
    /// <summary>A single SSE line longer than 1 MiB characters is refused as a malformed frame.</summary>
    [Fact]
    public async Task PhysicalLineOverTheLimitIsRefusedAsMalformed()
    {
        var ct = TestContext.Current.CancellationToken;
        using var handler = new SseHandler("data: " + new string('x', BoundedLineReader.MaxLineLength + 1) + "\n\n");
        using var http = ClientFor(handler);
        var host = new RemoteEngineHost(http);

        var malformed = await Should.ThrowAsync<MalformedFrameHarnessException>(
            () => CollectAsync(host.SubmitPromptAsync("prompt", new PromptOptions(), ct))).ConfigureAwait(true);

        malformed.Message.ShouldBe("A physical frame exceeds the 1 MiB transport limit.");
    }

    /// <summary>
    /// Three data lines of 400,000 characters each stay under the line cap, but the joined frame passes 1 MiB on the
    /// third line and is refused before it dispatches.
    /// </summary>
    [Fact]
    public async Task AccumulatedFrameDataOverTheLimitIsRefusedAsMalformed()
    {
        var ct = TestContext.Current.CancellationToken;
        var chunk = new string('y', 400_000);
        using var handler = new SseHandler($"id: 1\ndata: {chunk}\ndata: {chunk}\ndata: {chunk}\n\n");
        using var http = ClientFor(handler);
        var host = new RemoteEngineHost(http);

        var malformed = await Should.ThrowAsync<MalformedFrameHarnessException>(
            () => CollectAsync(host.SubmitPromptAsync("prompt", new PromptOptions(), ct))).ConfigureAwait(true);

        malformed.Message.ShouldBe("A remote frame exceeds the 1 MiB transport limit.");
    }

    /// <summary>A frame well under the cap is delivered with its payload unchanged, and the stream continues to completion.</summary>
    [Fact]
    public async Task NormalFrameUnderTheLimitIsDeliveredUnchanged()
    {
        var ct = TestContext.Current.CancellationToken;
        var text = new string('z', 500_000);
        using var handler = new SseHandler(
            $"id: 1\nevent: text_delta\ndata: {{\"kind\":\"text_delta\",\"delta\":\"{text}\"}}\n\n",
            "id: 2\nevent: completed\ndata: {\"kind\":\"completed\",\"reason\":\"end_turn\"}\n\n");
        using var http = ClientFor(handler);
        var host = new RemoteEngineHost(http);

        var seen = await CollectAsync(host.SubmitPromptAsync("prompt", new PromptOptions(), ct)).ConfigureAwait(true);

        seen.Count.ShouldBe(2);
        seen[0].ShouldBeOfType<TextDeltaEvent>().Text.ShouldBe(text);
        seen[1].ShouldBe(new CompletedEvent("end_turn"));
    }

    private static HttpClient ClientFor(HttpMessageHandler handler) =>
        new(handler, disposeHandler: false) { BaseAddress = new Uri("https://api.local/") };

    private static async Task<List<HarnessEvent>> CollectAsync(IAsyncEnumerable<HarnessEvent> events)
    {
        var seen = new List<HarnessEvent>();
        await foreach (var evt in events.ConfigureAwait(false))
        {
            seen.Add(evt);
        }

        return seen;
    }

    /// <summary>Creates a session, accepts a prompt, and serves the given SSE body for the event stream.</summary>
    private sealed class SseHandler(params string[] frames) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Post && string.Equals(path, "/sessions", StringComparison.Ordinal))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created)
                {
                    Content = new StringContent("""{"id":"sess-1","ownerId":"local-dev"}""", Encoding.UTF8, "application/json"),
                });
            }

            if (request.Method == HttpMethod.Post && string.Equals(path, "/sessions/sess-1/prompts", StringComparison.Ordinal))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted)
                {
                    Content = new StringContent("""{"sessionId":"sess-1","status":"accepted"}""", Encoding.UTF8, "application/json"),
                });
            }

            if (request.Method == HttpMethod.Get && string.Equals(path, "/sessions/sess-1/events", StringComparison.Ordinal))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(string.Concat(frames), Encoding.UTF8, "text/event-stream"),
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
