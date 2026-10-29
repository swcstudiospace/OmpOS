using Autonogrammer.Coordination.Dragonfly;
using Microsoft.Extensions.Logging;
using Shouldly;
using StackExchange.Redis;
using Xunit;

namespace Autonogrammer.Coordination.Tests;

/// <summary>A failing signal handler is logged at Warning, and the subscription keeps delivering later signals. Requires AUTONOGRAMMER_TEST_DRAGONFLY_CONNECTION.</summary>
public sealed class DragonflySignalHandlerFailureTests
{
    [Fact]
    public async Task AFailingHandlerIsLoggedAtWarningAndLaterSignalsStillReachTheSubscription()
    {
        var ct = TestContext.Current.CancellationToken;
        using var subscriberConnection = await ConnectionMultiplexer.ConnectAsync(DragonflyTestEnvironment.RequireConnectionString()).ConfigureAwait(true);
        using var publisherConnection = await ConnectionMultiplexer.ConnectAsync(DragonflyTestEnvironment.RequireConnectionString()).ConfigureAwait(true);
        var logger = new CapturingLogger<DragonflySessionSignals>();
        var subscriber = new DragonflySessionSignals(subscriberConnection, logger);
        var publisher = new DragonflySessionSignals(publisherConnection);
        var failingId = DragonflyTestEnvironment.NewId();
        var healthyId = DragonflyTestEnvironment.NewId();
        var delivered = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        var subscription = await subscriber.SubscribeAsync(
            (id, ignoredToken) =>
            {
                if (string.Equals(id, failingId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("The handler failed on purpose.");
                }

                if (string.Equals(id, healthyId, StringComparison.Ordinal))
                {
                    delivered.TrySetResult(id);
                }

                return ValueTask.CompletedTask;
            },
            ct).ConfigureAwait(true);

        await publisher.PublishAsync(failingId, ct).ConfigureAwait(true);
        await publisher.PublishAsync(healthyId, ct).ConfigureAwait(true);
        var received = await delivered.Task.WaitAsync(TimeSpan.FromSeconds(10), ct).ConfigureAwait(true);

        received.ShouldBe(healthyId);

        // The handlers of two signals can run on different threads, so the healthy one may finish before the failure of the other is logged.
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (DateTime.UtcNow < deadline && !logger.Entries.Any(static entry => entry.Level == LogLevel.Warning))
        {
            await Task.Delay(TimeSpan.FromMilliseconds(20), ct).ConfigureAwait(true);
        }

        logger.Entries.Select(entry => entry.Level).ShouldContain(LogLevel.Warning);
        await subscription.DisposeAsync().ConfigureAwait(true);
    }
}
