using System.IO.Pipelines;
using System.Net.ServerSentEvents;
using System.Threading.Channels;
using Autonogrammer.Api.Sse;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Hosting;

/// <summary>Pins the server-sent event writer: headers, keep-alive on idle, the clean end on shutdown, and unchanged framing.</summary>
public sealed class SseStreamResultTests
{
    private static readonly TimeSpan Heartbeat = TimeSpan.FromSeconds(15);

    private static Action<WebApplication> MapStream(Channel<SseItem<string>> channel) =>
        app => app.MapGet("/sse", () => new SseStreamResult(channel.Reader.ReadAllAsync()));

    // Tests that read whole frames in order switch the keep-alive off, so a slow machine cannot slip a comment frame between the frames they expect.
    private static Dictionary<string, string?> NoKeepAlive() => new(StringComparer.Ordinal) { ["Hosting:Sse:HeartbeatSeconds"] = "0" };

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

    // The stream's idle period is a timer of the manual clock. A test may only move the clock once the stream has registered the timer of its current
    // wait; otherwise the timer would be created after the move and nothing would ever fire it.
    private static async Task WaitForTimersAsync(ManualTimeProvider time, int atLeast, CancellationToken testToken)
    {
        while (time.TimersCreated < atLeast)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(5), testToken).ConfigureAwait(false);
        }
    }

    /// <summary>Over HTTP a stream carries its headers, frames an event as before, and an idle stream gets a keep-alive comment (the interval is shortened to one second).</summary>
    [Fact]
    public async Task IdleStream_GetsAKeepAliveComment_AndHeadersArePresent()
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        limit.CancelAfter(TimeSpan.FromSeconds(30));
        var ct = limit.Token;
        var channel = Channel.CreateUnbounded<SseItem<string>>();
        _ = channel.Writer.TryWrite(Item("one", "1"));
        var config = new Dictionary<string, string?>(StringComparer.Ordinal) { ["Hosting:Sse:HeartbeatSeconds"] = "1" };
        var app = await HostingTestHost.StartAsync(ct, config: config, mapEndpoints: MapStream(channel)).ConfigureAwait(true);
        await using var appScope = app.ConfigureAwait(true);
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
            // The event was queued before the request, so it is the first frame however slow the machine is; the keep-alive can only come after it.
            (await ReadFrameAsync(reader, ct).ConfigureAwait(true)).ShouldBe(["event: message", "data: one", "id: 1"]);

            (await ReadFrameAsync(reader, ct).ConfigureAwait(true)).ShouldBe([": keepalive"]);
        }
    }

    // Runs the write loop over a pipe. The stream over the pipe's writer belongs to this task, so it is disposed when the loop ends, which is also what lets the reader see the end.
    private static async Task RunLoopAsync(PipeWriter writer, ChannelReader<SseItem<string>> items, TimeProvider time, CancellationToken testToken)
    {
        var body = writer.AsStream();
        await using (body.ConfigureAwait(false))
        {
            await SseStreamResult.WriteLoopAsync(items, body, time, Heartbeat, testToken).ConfigureAwait(false);
        }
    }

    /// <summary>The write loop writes a keep-alive only after a full idle interval counted from the last write, and an item restarts the interval.</summary>
    [Fact]
    public async Task WriteLoop_WritesAKeepAliveOnlyAfterAFullIdlePeriod()
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        limit.CancelAfter(TimeSpan.FromSeconds(30));
        var ct = limit.Token;
        var time = new ManualTimeProvider();
        var channel = Channel.CreateUnbounded<SseItem<string>>();
        var pipe = new Pipe();
        var loop = RunLoopAsync(pipe.Writer, channel.Reader, time, ct);
        var incoming = pipe.Reader.AsStream();
        await using (incoming.ConfigureAwait(true))
        {
            using var reader = new StreamReader(incoming);

            await WaitForTimersAsync(time, 1, ct).ConfigureAwait(true);
            _ = channel.Writer.TryWrite(Item("one", "1"));
            (await ReadFrameAsync(reader, ct).ConfigureAwait(true)).ShouldBe(["event: message", "data: one", "id: 1"]);

            await WaitForTimersAsync(time, 2, ct).ConfigureAwait(true);
            time.Advance(Heartbeat - TimeSpan.FromSeconds(1));
            _ = channel.Writer.TryWrite(Item("two", "2"));
            (await ReadFrameAsync(reader, ct).ConfigureAwait(true)).ShouldBe(["event: message", "data: two", "id: 2"]);

            await WaitForTimersAsync(time, 3, ct).ConfigureAwait(true);
            time.Advance(Heartbeat * 2);
            (await ReadFrameAsync(reader, ct).ConfigureAwait(true)).ShouldBe([": keepalive"]);

            channel.Writer.Complete();
            await loop.ConfigureAwait(true);
        }
    }

    /// <summary>Event ids, order and data pass through unchanged, so a client can resume with Last-Event-ID.</summary>
    [Fact]
    public async Task Events_KeepTheirIdsAndOrder()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = Channel.CreateUnbounded<SseItem<string>>();
        var app = await HostingTestHost.StartAsync(ct, config: NoKeepAlive(), mapEndpoints: MapStream(channel)).ConfigureAwait(true);
        await using var appScope = app.ConfigureAwait(true);
        using var client = app.GetTestClient();
        _ = channel.Writer.TryWrite(Item("a", "5"));
        _ = channel.Writer.TryWrite(Item("b", "6"));
        channel.Writer.Complete();

        var body = await client.GetStringAsync(new Uri("/sse", UriKind.Relative), ct).ConfigureAwait(true);

        var frames = body.ReplaceLineEndings("\n").Split("\n\n", StringSplitOptions.RemoveEmptyEntries);
        frames.Length.ShouldBe(2);
        frames[0].Split('\n').ShouldBe(["event: message", "data: a", "id: 5"]);
        frames[1].Split('\n').ShouldBe(["event: message", "data: b", "id: 6"]);
    }

    /// <summary>When the host stops, the stream writes <c>retry: 2000</c> and ends so the client reconnects elsewhere.</summary>
    [Fact]
    public async Task HostStopping_WritesRetryAndEndsTheStream()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = Channel.CreateUnbounded<SseItem<string>>();
        _ = channel.Writer.TryWrite(Item("one", "1"));
        var app = await HostingTestHost.StartAsync(ct, config: NoKeepAlive(), mapEndpoints: MapStream(channel)).ConfigureAwait(true);
        await using var appScope = app.ConfigureAwait(true);
        using var client = app.GetTestClient();
        using var response = await client.GetAsync(new Uri("/sse", UriKind.Relative), HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(true);
        var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(true);
        await using (stream.ConfigureAwait(true))
        {
            using var reader = new StreamReader(stream);
            // The event was queued before the request, so it is the first frame.
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
