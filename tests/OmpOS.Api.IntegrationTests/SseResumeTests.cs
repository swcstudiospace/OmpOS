using System.Globalization;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using OmpOS.Api.Sse;
using OmpOS.Contracts.Streaming;
using OmpOS.Harness.Abstractions;
using OmpOS.Sessions;
using Shouldly;
using Xunit;

namespace OmpOS.Api.IntegrationTests;

/// <summary>
/// SSE resume: a scripted 12-event stream is read, interrupted, and resumed with no gaps,
/// no duplicates, and a terminal event.
/// </summary>
public sealed class SseResumeTests
{
    /// <summary>Verifies Last-Event-ID resume delivers events 6..N exactly once with a terminal close.</summary>
    [Fact]
    public async Task Resume_WithLastEventId_DeliversRemainingExactlyOnce()
    {
        var script = ScriptedHarnessFactory.DeltaScript(11);
        using var factory = new OmpApiFactory(configureServices: services => services.AddSingleton<IHarnessSessionFactory>(new ScriptedHarnessFactory(script)));
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;
        var id = await SessionClient.CreateSessionAsync(client, testToken);

        using var accepted = await SessionClient.PostPromptAsync(client, id, "{\"prompt\":\"stream me\"}", testToken);
        accepted.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        var first = await SessionClient.ReadStreamAsync(client, $"/sessions/{id}/events", 5, testToken);
        first.Count.ShouldBe(5);
        first[0].EventType.ShouldBe("prompt_accepted");
        first.ShouldAllBe(static item => !string.IsNullOrWhiteSpace(item.EventId));
        var firstSeqs = first.Select(static item => long.Parse(item.EventId!, CultureInfo.InvariantCulture)).ToList();
        firstSeqs.ShouldBe(Enumerable.Range(1, 5).Select(static i => (long)i).ToList());

        var resumed = await SessionClient.ReadStreamAsync(client, $"/sessions/{id}/events", 50, testToken, first[^1].EventId);
        var resumedSeqs = resumed.Select(static item => long.Parse(item.EventId!, CultureInfo.InvariantCulture)).ToList();
        resumedSeqs.ShouldBe(Enumerable.Range(6, resumedSeqs.Count).Select(static i => (long)i).ToList());
        resumed.ShouldAllBe(static item => !string.IsNullOrWhiteSpace(item.Data));
        resumed.Take(resumed.Count - 2).ShouldAllBe(static item => string.Equals(item.EventType, "text_delta", StringComparison.Ordinal));
        resumed[^1].EventType.ShouldBe("completed");

        var combined = firstSeqs.Concat(resumedSeqs).ToList();
        combined.Distinct().Count().ShouldBe(combined.Count, "resume must not duplicate or gap");
    }

    /// <summary>Verifies the since query parameter resumes the stream.</summary>
    [Fact]
    public async Task Resume_WithSinceQuery_DeliversRemainingContiguously()
    {
        var script = ScriptedHarnessFactory.DeltaScript(11);
        using var factory = new OmpApiFactory(configureServices: services => services.AddSingleton<IHarnessSessionFactory>(new ScriptedHarnessFactory(script)));
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;
        var id = await SessionClient.CreateSessionAsync(client, testToken);

        using var accepted = await SessionClient.PostPromptAsync(client, id, "{\"prompt\":\"stream me\"}", testToken);
        accepted.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        var first = await SessionClient.ReadStreamAsync(client, $"/sessions/{id}/events", 2, testToken);
        first.Count.ShouldBe(2);

        var resumed = await SessionClient.ReadStreamAsync(client, $"/sessions/{id}/events?since={first[^1].EventId}", 50, testToken);
        var resumedSeqs = resumed.Select(static item => long.Parse(item.EventId!, CultureInfo.InvariantCulture)).ToList();
        resumedSeqs.ShouldBe(Enumerable.Range(3, resumedSeqs.Count).Select(static i => (long)i).ToList());
        resumed[^1].EventType.ShouldBe("completed", "the stream ends on the terminal event");
    }

    /// <summary>Verifies streams on unknown sessions return 404.</summary>
    [Fact]
    public async Task Events_UnknownSession_Returns404()
    {
        using var factory = new OmpApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/sessions/does-not-exist/events", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// An append that reaches the log without a local notify is delivered to a waiting stream by its poll. Another process
    /// appends this way, so the poll is what carries a cross-process append to the stream.
    /// </summary>
    [Fact]
    public async Task AppendWithoutLocalNotify_IsDeliveredByPoll()
    {
        using var factory = new OmpApiFactory();
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;
        var id = await SessionClient.CreateSessionAsync(client, testToken);
        var streamer = factory.Services.GetRequiredService<SessionEventStreamer>();
        var repository = factory.Services.GetRequiredService<ISessionRepository>();

        using var budget = CancellationTokenSource.CreateLinkedTokenSource(testToken);
        budget.CancelAfter(TimeSpan.FromSeconds(10));
        var token = budget.Token;

        await using var enumerator = streamer.SubscribeAsync(id, 0, token).GetAsyncEnumerator(token);
        var delivered = enumerator.MoveNextAsync().AsTask();

        // Let the subscriber finish its first read and settle into its wait before the append.
        await Task.Delay(TimeSpan.FromMilliseconds(250), token);
        var evt = new StreamEvent
        {
            Seq = 0,
            SessionId = id,
            CorrelationId = id,
            Timestamp = DateTimeOffset.UtcNow,
            Payload = new TextDeltaPayload { Delta = "polled" },
        };
        await repository.AppendAsync(id, evt, token);

        (await delivered.WaitAsync(token)).ShouldBeTrue();
        enumerator.Current.Payload.ShouldBeOfType<TextDeltaPayload>().Delta.ShouldBe("polled");
    }
}
