using System.Net.ServerSentEvents;
using System.Threading.Channels;
using Autonogrammer.Api.Sse;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Hosting;

/// <summary>Pins the server-sent event writer: headers, keep-alive on idle, the clean end on shutdown, and unchanged framing.</summary>
public sealed class SseStreamResultTests
{
    private static readonly TimeSpan Heartbeat = TimeSpan.FromSeconds(15);

    private static Action<WebApplication> MapStream(Channel<SseItem<string>> channel) =>
        app => app.MapGet("/sse", () => new SseStreamResult(channel.Reader.ReadAllAsync()));

    private static async Task<List<string>> ReadFrameAsync(StreamReader reader, CancellationToken testToken)
    {
        var lines = new List<string>();
        while (true)
        {
            var line = await reader.ReadLineAsync(testToken).ConfigureAwait(false) ?? throw new InvalidOperationException("The stream ended before the frame was complete.");
            if (line.Length == 0)
            {
                return lines;
            }

            lines.Add(line);
        }
    }

    private static SseItem<string> Item(string data, string id) => new(data, "message") { EventId = id };

    /// <summary>The stream sets its headers, frames events as before, and writes a keep-alive only after it has been idle for the interval.</summary>
    [Fact]
    public async Task IdleStream_GetsAKeepAliveComment_AndHeadersArePresent()
    {
        var ct = TestContext.Current.CancellationToken;
        var time = new ManualTimeProvider();
        var channel = Channel.CreateUnbounded<SseItem<string>>();
        await using var app = await HostingTestHost.StartAsync(
            ct,
            configureServices: services => services.AddSingleton<TimeProvider>(time),
            mapEndpoints: MapStream(channel)).ConfigureAwait(true);
        using var client = app.GetTestClient();

        using var response = await client.GetAsync(new Uri("/sse", UriKind.Relative), HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(true);

        response.Content.Headers.ContentType.ShouldNotBeNull().MediaType.ShouldBe("text/event-stream");
        var cacheControl = response.Headers.CacheControl.ShouldNotBeNull();
        cacheControl.NoStore.ShouldBeTrue();
        cacheControl.NoTransform.ShouldBeTrue();
        response.Headers.GetValues("X-Accel-Buffering").ShouldBe(["no"]);

        var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(true);
        await using (stream.ConfigureAwait(true))
        {
            using var reader = new StreamReader(stream);

            _ = channel.Writer.TryWrite(Item("one", "1"));
            (await ReadFrameAsync(reader, ct).ConfigureAwait(true)).ShouldBe(new[] { "id: 1", "data: one" }, ignoreOrder: true);

            time.Advance(Heartbeat - TimeSpan.FromSeconds(1));
            _ = channel.Writer.TryWrite(Item("two", "2"));
            (await ReadFrameAsync(reader, ct).ConfigureAwait(true)).ShouldBe(new[] { "id: 2", "data: two" }, ignoreOrder: true);

            time.Advance(Heartbeat * 2);
            (await ReadFrameAsync(reader, ct).ConfigureAwait(true)).ShouldBe(new[] { ": keepalive" });
        }
    }

    /// <summary>Event ids, order and data pass through unchanged, so a client can resume with Last-Event-ID.</summary>
    [Fact]
    public async Task Events_KeepTheirIdsAndOrder()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = Channel.CreateUnbounded<SseItem<string>>();
        await using var app = await HostingTestHost.StartAsync(ct, mapEndpoints: MapStream(channel)).ConfigureAwait(true);
        using var client = app.GetTestClient();
        _ = channel.Writer.TryWrite(Item("a", "5"));
        _ = channel.Writer.TryWrite(Item("b", "6"));
        channel.Writer.Complete();

        var body = await client.GetStringAsync(new Uri("/sse", UriKind.Relative), ct).ConfigureAwait(true);

        var frames = body.ReplaceLineEndings("\n").Split("\n\n", StringSplitOptions.RemoveEmptyEntries);
        frames.Length.ShouldBe(2);
        frames[0].Split('\n').ShouldBe(new[] { "id: 5", "data: a" }, ignoreOrder: true);
        frames[1].Split('\n').ShouldBe(new[] { "id: 6", "data: b" }, ignoreOrder: true);
    }

    /// <summary>When the host stops, the stream writes <c>retry: 2000</c> and ends so the client reconnects elsewhere.</summary>
    [Fact]
    public async Task HostStopping_WritesRetryAndEndsTheStream()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = Channel.CreateUnbounded<SseItem<string>>();
        await using var app = await HostingTestHost.StartAsync(ct, mapEndpoints: MapStream(channel)).ConfigureAwait(true);
        using var client = app.GetTestClient();
        using var response = await client.GetAsync(new Uri("/sse", UriKind.Relative), HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(true);
        var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(true);
        await using (stream.ConfigureAwait(true))
        {
            using var reader = new StreamReader(stream);
            _ = channel.Writer.TryWrite(Item("one", "1"));
            _ = await ReadFrameAsync(reader, ct).ConfigureAwait(true);

            app.Lifetime.StopApplication();

            var rest = await reader.ReadToEndAsync(ct).ConfigureAwait(true);
            rest.ReplaceLineEndings("\n").ShouldBe("retry: 2000\n\n");
        }
    }

    /// <summary>The real session stream carries the hosting headers and still answers a resume position.</summary>
    [Fact]
    public async Task SessionStream_CarriesTheHostingHeaders()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var id = await SessionClient.CreateSessionAsync(client, ct).ConfigureAwait(true);
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri($"/sessions/{id}/events", UriKind.Relative));
        request.Headers.TryAddWithoutValidation("Last-Event-ID", "0");

        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(true);

        response.EnsureSuccessStatusCode();
        response.Headers.CacheControl.ShouldNotBeNull().NoTransform.ShouldBeTrue();
        response.Headers.GetValues("X-Accel-Buffering").ShouldBe(["no"]);
    }
}
