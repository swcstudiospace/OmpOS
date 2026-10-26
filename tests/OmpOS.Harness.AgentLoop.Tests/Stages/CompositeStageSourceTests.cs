using OmpOS.Contracts.Agents;
using OmpOS.Harness.AgentLoop.Stages;
using OmpOS.Harness.AgentLoop.Tests.Sessions;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.AgentLoop.Tests.Stages;

/// <summary>
/// Verifies the composite stage source through the events it emits and through counters on scripted child sources: per-child
/// order, isolation of a faulty child, disposal on cancellation, de-duplication and back-pressure.
/// </summary>
public sealed class CompositeStageSourceTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 26, 8, 0, 0, TimeSpan.Zero);

    private static readonly StageObservationScope Scope = new()
    {
        SessionId = "s-test",
        WorkspaceRoot = "/work",
        SessionStateDirectory = "/state/s-test",
        GraphId = "ut-graph-12345678",
    };

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task EventsOfEachChildKeepTheirOrderWhenChildrenInterleave()
    {
        var first = new ScriptedSource(Enumerable.Range(0, 20).Select(index => Event("a-" + index, StageStatus.Running, source: StageSource.Ultrathink)));
        var second = new ScriptedSource(Enumerable.Range(0, 20).Select(index => Event("b-" + index, StageStatus.Running, source: StageSource.Swarm)));
        var composite = Compose(Named("Ultrathink", StageSource.Ultrathink, first, mayComplete: true), Named("Swarm", StageSource.Swarm, second, mayComplete: true));

        var events = await CollectAsync(composite);

        events.Count.ShouldBe(40);
        events.Where(item => item.Source == StageSource.Ultrathink).Select(item => item.StageId)
            .ShouldBe(Enumerable.Range(0, 20).Select(index => "a-" + index));
        events.Where(item => item.Source == StageSource.Swarm).Select(item => item.StageId)
            .ShouldBe(Enumerable.Range(0, 20).Select(index => "b-" + index));
    }

    [Fact]
    public async Task ThrowingChildBecomesOneDegradedStageWhileTheOthersKeepRunning()
    {
        var healthy = new ScriptedSource([Event("ok-1", StageStatus.Running), Event("ok-2", StageStatus.Succeeded)]);
        var faulty = new ScriptedSource(
            [Event("bad-1", StageStatus.Running, source: StageSource.Swarm)],
            failure: new InvalidOperationException("boom: live.json is gone\n   at Fake.Frame() in /secret/path.cs:line 3"));
        var laterHealthy = new ScriptedSource([Event("later", StageStatus.Succeeded, source: StageSource.Gsd)]);
        var composite = Compose(
            Named("Omp", StageSource.Omp, healthy, mayComplete: true),
            Named("Swarm observer", StageSource.Swarm, faulty),
            Named("Gsd", StageSource.Gsd, laterHealthy, mayComplete: true));

        var events = await CollectAsync(composite);

        events.Select(item => item.StageId).Where(id => id is "ok-1" or "ok-2" or "bad-1" or "later").Order(StringComparer.Ordinal).ToArray()
            .ShouldBe(["bad-1", "later", "ok-1", "ok-2"]);
        var degraded = events.Where(item => item.Status == StageStatus.Degraded).ShouldHaveSingleItem();
        degraded.ShouldSatisfyAllConditions(
            item => item.Title.ShouldBe("Swarm observer"),
            item => item.Source.ShouldBe(StageSource.Swarm),
            item => item.SessionId.ShouldBe("s-test"),
            item => item.GraphId.ShouldBe("ut-graph-12345678"),
            item => item.Timestamp.ShouldBe(Start),
            item => item.Detail.ShouldBe("InvalidOperationException: boom: live.json is gone"));
    }

    [Fact]
    public async Task ChildThatThrowsWhenAskedToObserveIsReportedTheSameWay()
    {
        var composite = Compose(
            Named("Substrate", StageSource.Substrate, new ThrowingOnCallSource(new TimeoutException())),
            Named("Omp", StageSource.Omp, new ScriptedSource([Event("ok", StageStatus.Running)]), mayComplete: true));

        var events = await CollectAsync(composite);

        events.Select(item => item.StageId).ShouldContain("ok", StringComparer.Ordinal);
        var degraded = events.Where(item => item.Status == StageStatus.Degraded).ShouldHaveSingleItem();
        degraded.Title.ShouldBe("Substrate");
        degraded.Source.ShouldBe(StageSource.Substrate);
        degraded.Detail.ShouldNotBeNull().ShouldStartWith("TimeoutException");
    }

    [Fact]
    public async Task ChildThatEndsEarlyIsReportedUnlessItIsAllowedToComplete()
    {
        var composite = Compose(
            Named("Observer", StageSource.Swarm, new ScriptedSource([Event("x", StageStatus.Running)])),
            Named("Availability", StageSource.Omp, new ScriptedSource([Event("y", StageStatus.Unavailable)]), mayComplete: true));

        var events = await CollectAsync(composite);

        events.Where(item => item.Status == StageStatus.Degraded).ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            item => item.Title.ShouldBe("Observer"),
            item => item.Detail.ShouldNotBeNull().ShouldContain("ended before"));
        events.Select(item => item.StageId).ShouldContain("y", StringComparer.Ordinal);
    }

    [Fact]
    public async Task ChildThatKeepsFailingProducesExactlyOneDegradedEvent()
    {
        var composite = Compose(Named("Flaky", StageSource.Gsd, new ScriptedSource([Event("g", StageStatus.Running)], failure: new IOException("disk"))));

        var events = await CollectAsync(composite);

        events.Count(item => item.Status == StageStatus.Degraded).ShouldBe(1);
    }

    [Fact]
    public async Task CancellationCompletesQuietlyAndDisposesEveryChildEnumerator()
    {
        var children = Enumerable.Range(0, 3)
            .Select(index => new ScriptedSource([Event("c-" + index, StageStatus.Running)], holdOpen: true))
            .ToArray();
        var composite = Compose(children.Select((child, index) => Named("child-" + index, StageSource.Swarm, child)).ToArray());
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
        var seen = 0;

        await foreach (var item in composite.ObserveAsync(Scope, cancellation.Token).ConfigureAwait(true))
        {
            item.Status.ShouldBe(StageStatus.Running);
            seen++;
            if (seen == 3)
            {
                await cancellation.CancelAsync().ConfigureAwait(true);
            }
        }

        seen.ShouldBe(3);
        children.ShouldAllBe(child => child.Started == 1 && child.Disposed == 1);
    }

    [Fact]
    public async Task ConsumerThatStopsEarlyAlsoDisposesEveryChildEnumerator()
    {
        var children = Enumerable.Range(0, 3)
            .Select(index => new ScriptedSource([Event("c-" + index, StageStatus.Running)], holdOpen: true))
            .ToArray();
        var composite = Compose(children.Select((child, index) => Named("child-" + index, StageSource.Swarm, child)).ToArray());

        await foreach (var item in composite.ObserveAsync(Scope, Token).ConfigureAwait(true))
        {
            item.ShouldNotBeNull();
            break;
        }

        children.ShouldAllBe(child => child.Started == child.Disposed);
        children.Sum(child => child.Started).ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task AlreadyCancelledTokenYieldsNothingAndLeavesNoEnumeratorUndisposed()
    {
        var child = new ScriptedSource([Event("never", StageStatus.Running)], holdOpen: true);
        var composite = Compose(Named("child", StageSource.Swarm, child));
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
        await cancellation.CancelAsync().ConfigureAwait(true);

        var events = await CollectAsync(composite, cancellation.Token).ConfigureAwait(true);

        events.ShouldBeEmpty();
        child.Started.ShouldBe(child.Disposed);
    }

    [Fact]
    public async Task UnchangedEventsForOneKeyAreEmittedOnlyOnce()
    {
        var script = new[]
        {
            Event("stage", StageStatus.Running, title: "Plan", detail: "one"),
            Event("stage", StageStatus.Running, title: "Plan", detail: "one"),
            Event("stage", StageStatus.Running, title: "Plan", detail: "two"),
            Event("stage", StageStatus.Succeeded, title: "Plan", detail: "two"),
            Event("stage", StageStatus.Succeeded, title: "Plan", detail: "two"),
            Event("stage", StageStatus.Succeeded, title: "Plan finished", detail: "two"),
            Event("other", StageStatus.Succeeded, title: "Plan finished", detail: "two"),
            Event("stage", StageStatus.Succeeded, title: "Plan finished", detail: "two", source: StageSource.Gsd),
            Event("stage", StageStatus.Running, title: "Plan", detail: "one"),
        };
        var composite = Compose(Named("Ultrathink", StageSource.Ultrathink, new ScriptedSource(script), mayComplete: true));

        var events = await CollectAsync(composite);

        events.Select(item => (item.Source, item.StageId, item.Status, item.Title, item.Detail)).ToArray().ShouldBe(
            new[]
            {
                (StageSource.Swarm, "stage", StageStatus.Running, "Plan", (string?)"one"),
                (StageSource.Swarm, "stage", StageStatus.Running, "Plan", "two"),
                (StageSource.Swarm, "stage", StageStatus.Succeeded, "Plan", "two"),
                (StageSource.Swarm, "stage", StageStatus.Succeeded, "Plan finished", "two"),
                (StageSource.Swarm, "other", StageStatus.Succeeded, "Plan finished", "two"),
                (StageSource.Gsd, "stage", StageStatus.Succeeded, "Plan finished", "two"),
                (StageSource.Swarm, "stage", StageStatus.Running, "Plan", "one"),
            });
    }

    [Fact]
    public async Task SlowConsumerHoldsTheProducerBackWithoutLosingOrReorderingEvents()
    {
        const int capacity = 4;
        const int total = 500;
        var producer = new ScriptedSource(Enumerable.Range(0, total).Select(index => Event("e-" + index, StageStatus.Running)));
        var composite = new CompositeStageSource([Named("Producer", StageSource.Swarm, producer, mayComplete: true)], new SessionTestClock(Start), capacity);
        var received = new List<string>();
        var maxLead = 0;

        await foreach (var item in composite.ObserveAsync(Scope, Token).ConfigureAwait(true))
        {
            received.Add(item.StageId);
            maxLead = Math.Max(maxLead, producer.Produced - received.Count);
            await Task.Yield();
        }

        received.ShouldBe(Enumerable.Range(0, total).Select(index => "e-" + index));
        maxLead.ShouldBeLessThanOrEqualTo(capacity + 2);
    }

    [Fact]
    public async Task CompositeWithoutChildrenCompletesImmediately()
    {
        var events = await CollectAsync(Compose());

        events.ShouldBeEmpty();
    }

    private static CompositeStageSource Compose(params NamedStageSource[] children) => new(children, new SessionTestClock(Start));

    private static NamedStageSource Named(string name, StageSource source, IStageSource inner, bool mayComplete = false) =>
        new(name, source, inner, mayComplete);

    private static StageEvent Event(
        string stageId,
        StageStatus status,
        string title = "title",
        string? detail = null,
        StageSource source = StageSource.Swarm) => new()
        {
            SessionId = "s-test",
            Source = source,
            StageId = stageId,
            Title = title,
            Status = status,
            Timestamp = Start,
            Detail = detail,
        };

    private static async Task<List<StageEvent>> CollectAsync(CompositeStageSource composite, CancellationToken? token = null)
    {
        var events = new List<StageEvent>();
        var effective = token ?? Token;
        await foreach (var item in composite.ObserveAsync(Scope, effective).ConfigureAwait(true))
        {
            events.Add(item);
        }

        return events;
    }

    /// <summary>A scripted child: it emits its events, then fails, completes or holds open until it is cancelled. It counts starts and disposals.</summary>
    private sealed class ScriptedSource(IEnumerable<StageEvent> events, bool holdOpen = false, Exception? failure = null) : IStageSource
    {
        private readonly StageEvent[] _events = [.. events];
        private readonly bool _holdOpen = holdOpen;
        private readonly Exception? _failure = failure;
        private int _started;
        private int _disposed;
        private int _produced;

        public int Started => Volatile.Read(ref _started);

        public int Disposed => Volatile.Read(ref _disposed);

        public int Produced => Volatile.Read(ref _produced);

        public IAsyncEnumerable<StageEvent> ObserveAsync(StageObservationScope scope, CancellationToken cancellationToken) => new Sequence(this);

        private sealed class Sequence(ScriptedSource owner) : IAsyncEnumerable<StageEvent>
        {
            public IAsyncEnumerator<StageEvent> GetAsyncEnumerator(CancellationToken cancellationToken = default)
            {
                Interlocked.Increment(ref owner._started);
                return new Enumerator(owner, cancellationToken);
            }
        }

        private sealed class Enumerator(ScriptedSource owner, CancellationToken cancellationToken) : IAsyncEnumerator<StageEvent>
        {
            private int _index = -1;

            public StageEvent Current => owner._events[_index];

            public async ValueTask<bool> MoveNextAsync()
            {
                _index++;
                if (_index < owner._events.Length)
                {
                    Interlocked.Increment(ref owner._produced);
                    return true;
                }

                if (owner._failure is not null)
                {
                    throw owner._failure;
                }

                if (owner._holdOpen)
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(true);
                }

                return false;
            }

            public ValueTask DisposeAsync()
            {
                Interlocked.Increment(ref owner._disposed);
                return ValueTask.CompletedTask;
            }
        }
    }

    /// <summary>A child whose observe call itself throws, before any enumerator exists.</summary>
    private sealed class ThrowingOnCallSource(Exception failure) : IStageSource
    {
        public IAsyncEnumerable<StageEvent> ObserveAsync(StageObservationScope scope, CancellationToken cancellationToken) => throw failure;
    }
}
