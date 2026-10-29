using Autonogrammer.Api.Sse;
using Autonogrammer.Contracts.Streaming;
using Autonogrammer.Sessions;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests;

/// <summary>
/// SSE watchers: one append followed by one notify wakes every subscriber waiting on the session,
/// not just one of them, so no stream stays asleep after the log grows.
/// </summary>
public sealed class SseWatcherWakeTests
{
    private const int WatcherCount = 3;
    private static readonly TimeSpan WakeBudget = TimeSpan.FromSeconds(10);

    /// <summary>Verifies every subscriber waiting on a session receives an append announced by a single notify.</summary>
    [Fact]
    public async Task SingleAppend_WakesEveryWaitingSubscriber()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;
        var id = await SessionClient.CreateSessionAsync(client, testToken).ConfigureAwait(true);
        var streamer = factory.Services.GetRequiredService<SessionEventStreamer>();
        var repository = factory.Services.GetRequiredService<ISessionRepository>();

        using var timeout = new CancellationTokenSource(WakeBudget);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, testToken);
        var token = linked.Token;

        var enumerators = new List<IAsyncEnumerator<StreamEvent>>();
        var pending = new List<Task<bool>>();
        for (var index = 0; index < WatcherCount; index++)
        {
            var enumerator = streamer.SubscribeAsync(id, 0, token).GetAsyncEnumerator(token);
            enumerators.Add(enumerator);
            pending.Add(enumerator.MoveNextAsync().AsTask());
        }

        // Give every subscriber time to reach its wait; one that has not waited yet still replays the append.
        await Task.Delay(TimeSpan.FromMilliseconds(250), token).ConfigureAwait(true);

        var evt = new StreamEvent
        {
            Seq = 0,
            SessionId = id,
            CorrelationId = id,
            Timestamp = DateTimeOffset.UtcNow,
            Payload = new TextDeltaPayload { Delta = "wake" },
        };
        await repository.AppendAsync(id, evt, token).ConfigureAwait(true);
        streamer.Notify(id);

        var woke = await Task.WhenAll(pending).WaitAsync(WakeBudget, token).ConfigureAwait(true);

        woke.ShouldAllBe(static delivered => delivered, "every waiting subscriber must wake for the append");
        enumerators.ShouldAllBe(static enumerator => enumerator.Current.Payload is TextDeltaPayload);
        foreach (var enumerator in enumerators)
        {
            await enumerator.DisposeAsync().ConfigureAwait(true);
        }
    }
}
