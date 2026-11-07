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

        // Append only once every subscriber is parked on its wait, so the single notify is what must wake all of them.
        await WaitUntilParkedAsync(streamer, id, WatcherCount, token).ConfigureAwait(true);

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

    /// <summary>Verifies the parked-subscriber count rises as each subscriber parks, falls as each one leaves, and is zero for an unknown session.</summary>
    [Fact]
    public async Task ParkedSubscriberCountTracksEachSubscriberUntilItLeaves()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;
        var id = await SessionClient.CreateSessionAsync(client, testToken).ConfigureAwait(true);
        var streamer = factory.Services.GetRequiredService<SessionEventStreamer>();
        streamer.WaitingSubscribers("no-such-session").ShouldBe(0);

        using var firstLeave = CancellationTokenSource.CreateLinkedTokenSource(testToken);
        using var secondLeave = CancellationTokenSource.CreateLinkedTokenSource(testToken);
        var first = streamer.SubscribeAsync(id, 0, firstLeave.Token).GetAsyncEnumerator(firstLeave.Token);
        var second = streamer.SubscribeAsync(id, 0, secondLeave.Token).GetAsyncEnumerator(secondLeave.Token);
        var firstPending = NextAsync(first);
        var secondPending = NextAsync(second);

        await WaitUntilParkedAsync(streamer, id, 2, testToken).ConfigureAwait(true);
        await firstLeave.CancelAsync().ConfigureAwait(true);
        await WaitUntilCompletedAsync(() => firstPending.IsCompleted, testToken).ConfigureAwait(true);
        firstPending.IsCanceled.ShouldBeTrue("a cancelled subscriber ends its read as cancelled");
        await WaitUntilParkedAsync(streamer, id, 1, testToken).ConfigureAwait(true);
        await secondLeave.CancelAsync().ConfigureAwait(true);
        await WaitUntilCompletedAsync(() => secondPending.IsCompleted, testToken).ConfigureAwait(true);
        secondPending.IsCanceled.ShouldBeTrue("a cancelled subscriber ends its read as cancelled");
        streamer.WaitingSubscribers(id).ShouldBe(0);

        await first.DisposeAsync().ConfigureAwait(true);
        await second.DisposeAsync().ConfigureAwait(true);
    }

    /// <summary>Starts one read of a subscriber; the subscriber parks on its wait before this returns its task.</summary>
    /// <param name="enumerator">The subscriber to read.</param>
    private static async Task<bool> NextAsync(IAsyncEnumerator<StreamEvent> enumerator) => await enumerator.MoveNextAsync().ConfigureAwait(false);

    /// <summary>Waits until <paramref name="expected"/> subscribers of the session are parked, bounded by the caller's token.</summary>
    /// <param name="streamer">The streamer whose subscribers are counted.</param>
    /// <param name="sessionId">The session the subscribers follow.</param>
    /// <param name="expected">How many subscribers must be parked.</param>
    /// <param name="cancellationToken">Bounds the wait; a subscriber that never parks fails the test by cancellation.</param>
    private static async Task WaitUntilParkedAsync(SessionEventStreamer streamer, string sessionId, int expected, CancellationToken cancellationToken)
    {
        while (streamer.WaitingSubscribers(sessionId) != expected)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(5), cancellationToken).ConfigureAwait(true);
        }
    }

    /// <summary>Waits until <paramref name="completed"/> reports true, bounded by the caller's token.</summary>
    /// <param name="completed">Reads whether the read has finished.</param>
    /// <param name="cancellationToken">Bounds the wait.</param>
    private static async Task WaitUntilCompletedAsync(Func<bool> completed, CancellationToken cancellationToken)
    {
        while (!completed())
        {
            await Task.Delay(TimeSpan.FromMilliseconds(5), cancellationToken).ConfigureAwait(true);
        }
    }
}
