using Shouldly;
using Xunit;

namespace OmpOS.Coordination.Tests;

/// <summary>Fan-out, unsubscribe and failure isolation of the process-local signal hub.</summary>
public sealed class InMemorySessionSignalsTests
{
    /// <summary>Every subscriber receives the session id of each publish.</summary>
    [Fact]
    public async Task PublishDeliversTheSessionIdToEverySubscriber()
    {
        var ct = TestContext.Current.CancellationToken;
        var signals = new InMemorySessionSignals();
        var first = new List<string>();
        var second = new List<string>();
        var firstSubscription = await signals.SubscribeAsync((id, _) => { first.Add(id); return ValueTask.CompletedTask; }, ct).ConfigureAwait(true);
        var secondSubscription = await signals.SubscribeAsync((id, _) => { second.Add(id); return ValueTask.CompletedTask; }, ct).ConfigureAwait(true);

        await signals.PublishAsync("s-1", ct).ConfigureAwait(true);

        first.Count.ShouldBe(1);
        first[0].ShouldBe("s-1");
        second.Count.ShouldBe(1);
        second[0].ShouldBe("s-1");
        await firstSubscription.DisposeAsync().ConfigureAwait(true);
        await secondSubscription.DisposeAsync().ConfigureAwait(true);
    }

    /// <summary>After disposal, later publishes skip the subscriber, and the other subscriber keeps receiving.</summary>
    [Fact]
    public async Task DisposedSubscriptionReceivesNothingFurther()
    {
        var ct = TestContext.Current.CancellationToken;
        var signals = new InMemorySessionSignals();
        var unsubscribed = new List<string>();
        var kept = new List<string>();
        var subscription = await signals.SubscribeAsync((id, _) => { unsubscribed.Add(id); return ValueTask.CompletedTask; }, ct).ConfigureAwait(true);
        var keptSubscription = await signals.SubscribeAsync((id, _) => { kept.Add(id); return ValueTask.CompletedTask; }, ct).ConfigureAwait(true);

        await subscription.DisposeAsync().ConfigureAwait(true);
        await subscription.DisposeAsync().ConfigureAwait(true);
        await signals.PublishAsync("s-1", ct).ConfigureAwait(true);

        unsubscribed.ShouldBeEmpty();
        kept.Count.ShouldBe(1);
        kept[0].ShouldBe("s-1");
        await keptSubscription.DisposeAsync().ConfigureAwait(true);
    }

    /// <summary>A throwing subscriber does not stop the others, and the publisher sees the failure afterwards.</summary>
    [Fact]
    public async Task FailingSubscriberDoesNotStopOthersAndIsReportedToThePublisher()
    {
        var ct = TestContext.Current.CancellationToken;
        var signals = new InMemorySessionSignals();
        var delivered = new List<string>();
        var failing = await signals.SubscribeAsync((_, _) => throw new InvalidOperationException("handler failed"), ct).ConfigureAwait(true);
        var healthy = await signals.SubscribeAsync((id, _) => { delivered.Add(id); return ValueTask.CompletedTask; }, ct).ConfigureAwait(true);

        var failure = await Should.ThrowAsync<AggregateException>(() => signals.PublishAsync("s-1", ct)).ConfigureAwait(true);

        failure.InnerExceptions.ShouldHaveSingleItem().ShouldBeOfType<InvalidOperationException>();
        delivered.Count.ShouldBe(1);
        delivered[0].ShouldBe("s-1");
        await failing.DisposeAsync().ConfigureAwait(true);
        await healthy.DisposeAsync().ConfigureAwait(true);
    }

    /// <summary>Publishing with no subscribers succeeds silently.</summary>
    [Fact]
    public async Task PublishWithoutSubscribersSucceeds()
    {
        var ct = TestContext.Current.CancellationToken;
        var signals = new InMemorySessionSignals();

        await signals.PublishAsync("s-1", ct).ConfigureAwait(true);
    }
}
