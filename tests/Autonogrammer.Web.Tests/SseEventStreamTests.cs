using System.Net;
using Autonogrammer.Web.Services;
using Shouldly;
using Xunit;

namespace Autonogrammer.Web.Tests;

/// <summary>SSE stream-client tests: resume, de-dup, terminal stop, reconnect bound.</summary>
public sealed class SseEventStreamTests
{
    private static readonly TimeSpan NoWait = TimeSpan.FromMilliseconds(1);

    /// <summary>A disconnect resumes with Last-Event-ID set to the last seen Seq; no event repeats.</summary>
    [Fact]
    public async Task ReconnectCarriesLastEventIdAndResumesWithoutGaps()
    {
        var ct = TestContext.Current.CancellationToken;
        using var handler = new ScriptedHandler();
        using var client = ClientFor(handler);
        var first = SseFixtures.Frame(SseFixtures.Text(1, "a"), "text_delta")
            + SseFixtures.Frame(SseFixtures.Text(2, "b"), "text_delta");
        using var firstResponse = OkWithChunks(SseFixtures.ChunkBy(first, 5));
        var rest = SseFixtures.Frame(SseFixtures.Text(3, "c"), "text_delta")
            + SseFixtures.Frame(SseFixtures.Completed(4), "completed");
        using var restResponse = OkWithChunks(SseFixtures.ChunkBy(rest, 5));
        handler.Enqueue(firstResponse);
        handler.Enqueue(restResponse);

        var stream = new SseEventStream(client, Options());
        var seqs = new List<long>();
        await foreach (var evt in stream.StreamSessionAsync("s-1", null, ct))
        {
            seqs.Add(evt.Seq);
        }

        seqs.ShouldBe([1L, 2L, 3L, 4L]);
        handler.Requests.Count.ShouldBe(2);
        handler.Requests[1].Headers.TryGetValues(SseEventStream.LastEventIdHeader, out var values).ShouldBeTrue();
        values.ShouldBe(["2"]);
    }

    /// <summary>Duplicate Seq values from replay+live overlap are dropped.</summary>
    [Fact]
    public async Task DuplicateSeqIsDropped()
    {
        var ct = TestContext.Current.CancellationToken;
        using var handler = new ScriptedHandler();
        using var client = ClientFor(handler);
        var body = SseFixtures.Frame(SseFixtures.Text(1), "text_delta")
            + SseFixtures.Frame(SseFixtures.Text(2), "text_delta")
            + SseFixtures.Frame(SseFixtures.Text(2), "text_delta")
            + SseFixtures.Frame(SseFixtures.Text(3), "text_delta")
            + SseFixtures.Frame(SseFixtures.Completed(4), "completed");
        using var queued = OkWithChunks(SseFixtures.ChunkBy(body, 7));
        handler.Enqueue(queued);

        var stream = new SseEventStream(client, Options());
        var seqs = new List<long>();
        await foreach (var evt in stream.StreamSessionAsync("s-1", null, ct))
        {
            seqs.Add(evt.Seq);
        }

        seqs.ShouldBe([1L, 2L, 3L, 4L]);
        handler.Requests.Count.ShouldBe(1);
    }

    /// <summary>The terminal event stops the stream without another reconnect.</summary>
    [Fact]
    public async Task TerminalEventStopsWithoutReconnect()
    {
        var ct = TestContext.Current.CancellationToken;
        using var handler = new ScriptedHandler();
        using var client = ClientFor(handler);
        var body = ": heartbeat\n\n"
            + SseFixtures.Frame(SseFixtures.Text(1), "text_delta")
            + SseFixtures.Frame(SseFixtures.Completed(2), "completed");
        using var queued = OkWithChunks([body]);
        handler.Enqueue(queued);

        var stream = new SseEventStream(client, Options());
        var seqs = new List<long>();
        await foreach (var evt in stream.StreamSessionAsync("s-1", null, ct))
        {
            seqs.Add(evt.Seq);
        }

        seqs.ShouldBe([1L, 2L]);
        handler.Requests.Count.ShouldBe(1);
    }

    /// <summary>Comment lines are ignored even when split across tiny chunks.</summary>
    [Fact]
    public async Task CommentLinesIgnoredAcrossSplitChunks()
    {
        var ct = TestContext.Current.CancellationToken;
        using var handler = new ScriptedHandler();
        using var client = ClientFor(handler);
        var body = ": hb\n: hb2\n"
            + SseFixtures.Frame(SseFixtures.Text(1), "text_delta")
            + ": mid\n"
            + SseFixtures.Frame(SseFixtures.Completed(2), "completed");
        using var queued = OkWithChunks(SseFixtures.ChunkBy(body, 4));
        handler.Enqueue(queued);

        var stream = new SseEventStream(client, Options());
        var seqs = new List<long>();
        await foreach (var evt in stream.StreamSessionAsync("s-1", null, ct))
        {
            seqs.Add(evt.Seq);
        }

        seqs.ShouldBe([1L, 2L]);
    }

    /// <summary>Each connection that delivers an event makes progress, so a stream that keeps advancing never exhausts the reconnect budget.</summary>
    [Fact]
    public async Task ProgressingConnectionsDoNotExhaustReconnectBudget()
    {
        var ct = TestContext.Current.CancellationToken;
        using var handler = new ScriptedHandler();
        using var client = ClientFor(handler);
        var responses = new List<HttpResponseMessage>();
        try
        {
            for (var seq = 1; seq <= 5; seq++)
            {
                // Each connection delivers one event and then drops without a terminal event.
                var dropped = OkWithChunks(SseFixtures.ChunkBy(SseFixtures.Frame(SseFixtures.Text(seq), "text_delta"), 6));
                responses.Add(dropped);
                handler.Enqueue(dropped);
            }

            var completed = OkWithChunks(SseFixtures.ChunkBy(SseFixtures.Frame(SseFixtures.Completed(6), "completed"), 6));
            responses.Add(completed);
            handler.Enqueue(completed);

            var stream = new SseEventStream(client, Options(maxReconnects: 2));
            var seqs = new List<long>();
            await foreach (var evt in stream.StreamSessionAsync("s-1", null, ct))
            {
                seqs.Add(evt.Seq);
            }

            seqs.ShouldBe([1L, 2L, 3L, 4L, 5L, 6L]);
            handler.Requests.Count.ShouldBe(6);
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    /// <summary>Consecutive connections that deliver no event exhaust the reconnect bound and throw, so the surface can go offline.</summary>
    [Fact]
    public async Task DroplessConnectionsThrowAtReconnectBudget()
    {
        var ct = TestContext.Current.CancellationToken;
        using var handler = new ScriptedHandler();
        using var client = ClientFor(handler);
        var failures = new List<HttpResponseMessage>();
        for (var i = 0; i < 5; i++)
        {
            var failing = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ChunkedSseContent(SseFixtures.ChunkBy(SseFixtures.Frame(SseFixtures.Text(1), "text_delta"), 6), failAfter: 0),
            };
            failures.Add(failing);
            handler.Enqueue(failing);
        }

        try
        {
            var stream = new SseEventStream(client, Options(maxReconnects: 2));
            var ex = await Should.ThrowAsync<ApiClientException>(() => DrainAsync(stream, ct)).ConfigureAwait(true);

            ex.Message.ShouldContain("giving up after 2 reconnect(s)");
            handler.Requests.Count.ShouldBe(3);
        }
        finally
        {
            foreach (var failing in failures)
            {
                failing.Dispose();
            }
        }
    }

    /// <summary>The initial request carries the since query when a resume Seq is given.</summary>
    [Fact]
    public async Task InitialSinceQueryIsSent()
    {
        var ct = TestContext.Current.CancellationToken;
        using var handler = new ScriptedHandler();
        using var client = ClientFor(handler);
        var body = SseFixtures.Frame(SseFixtures.Text(6), "text_delta")
            + SseFixtures.Frame(SseFixtures.Completed(7), "completed");
        using var queued = OkWithChunks([body]);
        handler.Enqueue(queued);

        var stream = new SseEventStream(client, Options());
        var seqs = new List<long>();
        await foreach (var evt in stream.StreamSessionAsync("s-1", 6, ct))
        {
            seqs.Add(evt.Seq);
        }

        seqs.ShouldBe([6L, 7L]);
        handler.Requests.Count.ShouldBe(1);
        handler.Requests[0].RequestUri!.Query.ShouldContain("since=5");
    }

    /// <summary>The first event is yielded before the stream completes: the client streams live, not buffered.</summary>
    [Fact]
    public async Task FirstEventYieldsBeforeStreamCompletes()
    {
        var ct = TestContext.Current.CancellationToken;
        using var handler = new ScriptedHandler();
        using var client = ClientFor(handler);
        var first = SseFixtures.Frame(SseFixtures.Text(1, "a"), "text_delta");
        var rest = SseFixtures.Frame(SseFixtures.Completed(2), "completed");
        using var gated = new GatedSseContent(first, rest);
        using var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = gated };
        handler.Enqueue(response);

        var stream = new SseEventStream(client, Options());
        var seqs = new List<long>();
        try
        {
            // CollectAsync runs until the gate blocks the second read, so the first event
            // arrives while the stream is still open without needing a background thread.
            var consuming = CollectAsync(stream, seqs, ct);

            var firstSeen = false;
            for (var i = 0; i < 200 && !firstSeen; i++)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(25), ct).ConfigureAwait(true);
                lock (seqs)
                {
                    firstSeen = seqs.Count == 1 && seqs[0] == 1;
                }
            }

            firstSeen.ShouldBeTrue("the first event must be yielded while the stream is still open");
            consuming.IsCompleted.ShouldBeFalse("the stream must still be open after the first event");
            gated.Release();
            await consuming.ConfigureAwait(true);

            seqs.ShouldBe([1L, 2L]);
            handler.Requests.Count.ShouldBe(1);
        }
        finally
        {
            gated.Release();
        }
    }

    /// <summary>A terminal payload without an event field still stops the stream.</summary>
    [Fact]
    public async Task TerminalPayloadWithoutEventFieldStops()
    {
        var ct = TestContext.Current.CancellationToken;
        using var handler = new ScriptedHandler();
        using var client = ClientFor(handler);
        using var queued = OkWithChunks([SseFixtures.FrameWithoutEvent(SseFixtures.Completed(9))]);
        handler.Enqueue(queued);

        var stream = new SseEventStream(client, Options());
        var seqs = new List<long>();
        await foreach (var evt in stream.StreamSessionAsync("s-1", null, ct))
        {
            seqs.Add(evt.Seq);
        }

        seqs.ShouldBe([9L]);
        handler.Requests.Count.ShouldBe(1);
    }

    private static HttpResponseMessage OkWithChunks(string[] chunks) =>
        new(HttpStatusCode.OK) { Content = new ChunkedSseContent(chunks) };

    private static async Task DrainAsync(SseEventStream stream, CancellationToken cancellationToken)
    {
        await foreach (var _ in stream.StreamSessionAsync("s-1", null, cancellationToken).ConfigureAwait(false))
        {
        }
    }

    private static async Task CollectAsync(SseEventStream stream, List<long> seqs, CancellationToken cancellationToken)
    {
        await foreach (var evt in stream.StreamSessionAsync("s-1", null, cancellationToken).ConfigureAwait(false))
        {
            lock (seqs)
            {
                seqs.Add(evt.Seq);
            }
        }
    }

    private static HttpClient ClientFor(ScriptedHandler handler) =>
        new(handler) { BaseAddress = new Uri("http://127.0.0.1:5187/", UriKind.Absolute) };

    private static SseStreamOptions Options(int? maxReconnects = null) =>
        new() { InitialRetryDelay = NoWait, MaxRetryDelay = NoWait, MaxReconnects = maxReconnects ?? 10 };
}
