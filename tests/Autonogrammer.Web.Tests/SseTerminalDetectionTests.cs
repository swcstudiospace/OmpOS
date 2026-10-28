using System.Net;
using System.Text.Json;
using Autonogrammer.Contracts.Serialization;
using Autonogrammer.Contracts.Streaming;
using Autonogrammer.Web.Services;
using Shouldly;
using Xunit;

namespace Autonogrammer.Web.Tests;

/// <summary>Terminal detection: a terminal payload ends the turn whatever its frame fields say, and exactly once.</summary>
public sealed class SseTerminalDetectionTests
{
    private static readonly TimeSpan NoWait = TimeSpan.FromMilliseconds(1);

    /// <summary>A terminal payload with no id line still ends the turn without a reconnect.</summary>
    [Fact]
    public async Task TerminalPayloadWithoutIdStopsWithoutReconnect()
    {
        var ct = TestContext.Current.CancellationToken;
        using var handler = new ScriptedHandler();
        using var client = ClientFor(handler);
        var body = SseFixtures.Frame(SseFixtures.Text(1), "text_delta")
            + DataLine(SseFixtures.Completed(2).Payload) + "\n\n";
        using var response = OkWithChunks(SseFixtures.ChunkBy(body, 9));
        handler.Enqueue(response);

        var seqs = await CollectAsync(client, ct).ConfigureAwait(true);

        seqs.ShouldBe([1L]);
        handler.Requests.Count.ShouldBe(1);
    }

    /// <summary>An error payload without an id stops the stream without a reconnect.</summary>
    [Fact]
    public async Task ErrorPayloadWithoutIdStopsWithoutReconnect()
    {
        var ct = TestContext.Current.CancellationToken;
        using var handler = new ScriptedHandler();
        using var client = ClientFor(handler);
        var error = new ErrorPayload { Message = "boom", Retryable = false };
        var body = SseFixtures.Frame(SseFixtures.Text(1), "text_delta") + DataLine(error) + "\n\n";
        using var response = OkWithChunks([body]);
        handler.Enqueue(response);

        var seqs = await CollectAsync(client, ct).ConfigureAwait(true);

        seqs.ShouldBe([1L]);
        handler.Requests.Count.ShouldBe(1);
    }

    /// <summary>A terminal payload whose Seq was already delivered still ends the turn.</summary>
    [Fact]
    public async Task TerminalPayloadWithDeliveredSeqStopsWithoutReconnect()
    {
        var ct = TestContext.Current.CancellationToken;
        using var handler = new ScriptedHandler();
        using var client = ClientFor(handler);
        var body = SseFixtures.Frame(SseFixtures.Text(1), "text_delta")
            + SseFixtures.Frame(SseFixtures.Text(2), "text_delta")
            + SseFixtures.Frame(SseFixtures.Completed(2), "text_delta");
        using var response = OkWithChunks(SseFixtures.ChunkBy(body, 11));
        handler.Enqueue(response);

        var seqs = await CollectAsync(client, ct).ConfigureAwait(true);

        seqs.ShouldBe([1L, 2L]);
        handler.Requests.Count.ShouldBe(1);
    }

    /// <summary>A terminal payload in the trailing frame, with no blank line after it, still ends the turn.</summary>
    [Fact]
    public async Task TerminalPayloadInTrailingFrameStopsWithoutReconnect()
    {
        var ct = TestContext.Current.CancellationToken;
        using var handler = new ScriptedHandler();
        using var client = ClientFor(handler);
        var body = SseFixtures.Frame(SseFixtures.Text(1), "text_delta") + DataLine(SseFixtures.Completed(2).Payload);
        using var response = OkWithChunks(SseFixtures.ChunkBy(body, 6));
        handler.Enqueue(response);

        var seqs = await CollectAsync(client, ct).ConfigureAwait(true);

        seqs.ShouldBe([1L]);
        handler.Requests.Count.ShouldBe(1);
    }

    /// <summary>A terminal payload under a non-terminal event name still ends the turn with its Seq delivered.</summary>
    [Fact]
    public async Task TerminalPayloadUnderOtherEventNameStops()
    {
        var ct = TestContext.Current.CancellationToken;
        using var handler = new ScriptedHandler();
        using var client = ClientFor(handler);
        var body = SseFixtures.Frame(SseFixtures.Text(1), "text_delta")
            + SseFixtures.Frame(SseFixtures.Completed(2), "message");
        using var response = OkWithChunks([body]);
        handler.Enqueue(response);

        var seqs = await CollectAsync(client, ct).ConfigureAwait(true);

        seqs.ShouldBe([1L, 2L]);
        handler.Requests.Count.ShouldBe(1);
    }

    /// <summary>Frames after the first terminal event are never delivered.</summary>
    [Fact]
    public async Task EventsAfterTerminalAreNotDelivered()
    {
        var ct = TestContext.Current.CancellationToken;
        using var handler = new ScriptedHandler();
        using var client = ClientFor(handler);
        var body = SseFixtures.Frame(SseFixtures.Text(1), "text_delta")
            + SseFixtures.Frame(SseFixtures.Completed(2), "completed")
            + SseFixtures.Frame(SseFixtures.Text(3), "text_delta");
        using var response = OkWithChunks([body]);
        handler.Enqueue(response);

        var seqs = await CollectAsync(client, ct).ConfigureAwait(true);

        seqs.ShouldBe([1L, 2L]);
        handler.Requests.Count.ShouldBe(1);
    }

    private static string DataLine(StreamEventPayload payload) =>
        "data: " + JsonSerializer.Serialize(payload, AutonogrammerJsonContext.Default.StreamEventPayload);

    private static HttpResponseMessage OkWithChunks(string[] chunks) =>
        new(HttpStatusCode.OK) { Content = new ChunkedSseContent(chunks) };

    private static async Task<List<long>> CollectAsync(HttpClient client, CancellationToken cancellationToken)
    {
        var stream = new SseEventStream(client, new SseStreamOptions { InitialRetryDelay = NoWait, MaxRetryDelay = NoWait, MaxReconnects = 10 });
        var seqs = new List<long>();
        await foreach (var evt in stream.StreamSessionAsync("s-1", null, cancellationToken).ConfigureAwait(false))
        {
            seqs.Add(evt.Seq);
        }

        return seqs;
    }

    private static HttpClient ClientFor(ScriptedHandler handler) =>
        new(handler) { BaseAddress = new Uri("http://127.0.0.1:5187/", UriKind.Absolute) };
}
