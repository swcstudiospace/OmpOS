using System.Runtime.CompilerServices;
using Autonogrammer.Contracts.Agents;
using Autonogrammer.Native.Agents;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.AgentLoop;

/// <summary>
/// The pieces that keep an observer looking at the session's own files as the session learns its omp session identifier and its Graph
/// identifier: the live scope, the wrapper that restarts an observer when the scope changes, and the tap that reports the planner's graph.
/// </summary>
public sealed class ScopeFollowingTests
{
    private const string GraphId = "ut-mvp0bh59-2dd6ef4c";

    private static StageObservationScope Scope() => new()
    {
        SessionId = "s-follow",
        WorkspaceRoot = "/work",
        SessionStateDirectory = "/state/sessions/s-follow",
    };

    private static StageEvent Event(StageSource source, string detail, string? graphId = null) => new()
    {
        SessionId = "s-follow",
        Source = source,
        StageId = "plan",
        Title = "Plan",
        Status = StageStatus.Running,
        Timestamp = DateTimeOffset.UnixEpoch,
        Detail = detail,
        GraphId = graphId,
    };

    /// <summary>An update fills in what became known, never forgets it, and reports whether anything changed.</summary>
    [Fact]
    public void TheScopeLearnsIdentifiersAndNeverForgetsThem()
    {
        var provider = new SessionScopeProvider(Scope());

        provider.Update("native-1", null).ShouldBeTrue();
        provider.Update(null, GraphId).ShouldBeTrue();
        provider.Update("native-1", GraphId).ShouldBeFalse();
        provider.Update(" ", null).ShouldBeFalse();

        provider.Current.AcpSessionId.ShouldBe("native-1");
        provider.Current.GraphId.ShouldBe(GraphId);
        provider.Current.SessionId.ShouldBe("s-follow");
    }

    /// <summary>A waiter completes on the next change, and at once when the scope it read is already out of date.</summary>
    [Fact]
    public async Task AWaiterCompletesOnTheNextChangeAndAtOnceWhenItIsStale()
    {
        var provider = new SessionScopeProvider(Scope());
        var known = provider.Current;
        var waiting = provider.WhenChangedAfterAsync(known);
        waiting.IsCompleted.ShouldBeFalse();

        provider.Update("native-2", null).ShouldBeTrue();

        await waiting.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken).ConfigureAwait(true);
        provider.WhenChangedAfterAsync(known).IsCompleted.ShouldBeTrue();
        provider.WhenChangedAfterAsync(provider.Current).IsCompleted.ShouldBeFalse();
    }

    /// <summary>When the scope changes, the observer is stopped, disposed, and started again with the new scope.</summary>
    [Fact]
    public async Task TheObserverRestartsWithTheNewScopeWhenTheScopeChanges()
    {
        var ct = TestContext.Current.CancellationToken;
        var provider = new SessionScopeProvider(Scope());
        var inner = new HoldingSource();
        var enumerator = new ScopeFollowingStageSource(inner, provider).ObserveAsync(provider.Current, ct).GetAsyncEnumerator(ct);
        await using var lifetime = enumerator.ConfigureAwait(true);

        (await enumerator.MoveNextAsync().ConfigureAwait(true)).ShouldBeTrue();
        enumerator.Current.Detail.ShouldBe("none");
        provider.Update("native-5", null).ShouldBeTrue();
        (await enumerator.MoveNextAsync().ConfigureAwait(true)).ShouldBeTrue();

        enumerator.Current.Detail.ShouldBe("native-5");
        inner.Scopes.Select(static scope => scope.AcpSessionId).ShouldBe([null, "native-5"]);
        inner.Ended.ShouldBe(1, "the first observation was stopped before the second one started");

        await enumerator.DisposeAsync().ConfigureAwait(true);
        inner.Ended.ShouldBe(2);
    }

    /// <summary>A source that ends by itself ends the wrapper too, after its events.</summary>
    [Fact]
    public async Task ASourceThatEndsEndsTheWrapper()
    {
        var ct = TestContext.Current.CancellationToken;
        var provider = new SessionScopeProvider(Scope());
        var events = new List<StageEvent>();

        await foreach (var stageEvent in new ScopeFollowingStageSource(new FiniteSource(), provider).ObserveAsync(provider.Current, ct).ConfigureAwait(true))
        {
            events.Add(stageEvent);
        }

        events.Select(static e => e.Detail).ShouldBe(["one", "two"]);
    }

    /// <summary>Cancelling the observation ends the sequence without throwing to the consumer.</summary>
    [Fact]
    public async Task CancellationEndsTheSequenceWithoutAnException()
    {
        var provider = new SessionScopeProvider(Scope());
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var enumerator = new ScopeFollowingStageSource(new HoldingSource(), provider).ObserveAsync(provider.Current, cancellation.Token).GetAsyncEnumerator(cancellation.Token);
        await using var lifetime = enumerator.ConfigureAwait(true);
        (await enumerator.MoveNextAsync().ConfigureAwait(true)).ShouldBeTrue();

        await cancellation.CancelAsync().ConfigureAwait(true);

        (await enumerator.MoveNextAsync().ConfigureAwait(true)).ShouldBeFalse();
    }

    /// <summary>The task context is read with the scope the session has now, but only for the same session.</summary>
    [Fact]
    public async Task TheContextIsReadWithTheCurrentScopeOfTheSameSessionOnly()
    {
        var ct = TestContext.Current.CancellationToken;
        var provider = new SessionScopeProvider(Scope());
        var inner = new CapturingContextSource();
        var source = new ScopeFollowingTaskContextSource(inner, provider);
        provider.Update("native-3", GraphId);

        await source.GetAsync(Scope(), ct).ConfigureAwait(true);
        await source.GetAsync(Scope() with { SessionId = "s-other" }, ct).ConfigureAwait(true);

        inner.Scopes[0].AcpSessionId.ShouldBe("native-3");
        inner.Scopes[0].GraphId.ShouldBe(GraphId);
        inner.Scopes[1].SessionId.ShouldBe("s-other");
        inner.Scopes[1].AcpSessionId.ShouldBeNull();
    }

    /// <summary>Only a planner event that carries a Graph identifier is reported, and every event passes through in order.</summary>
    [Fact]
    public async Task OnlyAPlannerEventWithAGraphIdentifierIsReported()
    {
        var ct = TestContext.Current.CancellationToken;
        StageEvent[] events =
        [
            Event(StageSource.Swarm, "swarm", GraphId),
            Event(StageSource.Ultrathink, "no graph yet"),
            Event(StageSource.Ultrathink, "planned", GraphId),
        ];
        var reported = new List<string>();
        var tap = new GraphTapStageSource(new ListSource(events), reported.Add);
        var seen = new List<StageEvent>();

        await foreach (var stageEvent in tap.ObserveAsync(Scope(), ct).ConfigureAwait(true))
        {
            seen.Add(stageEvent);
        }

        seen.ShouldBe(events);
        reported.ShouldBe([GraphId]);
    }

    /// <summary>A re-check source hands on what is published and ends when its observation is cancelled.</summary>
    [Fact]
    public async Task TheRecheckSourceCarriesPublishedEventsUntilCancelled()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var source = new RecheckStageSource();
        source.Publish(Event(StageSource.Gsd, "before"));
        var enumerator = source.ObserveAsync(Scope(), cancellation.Token).GetAsyncEnumerator(cancellation.Token);
        await using var lifetime = enumerator.ConfigureAwait(true);

        (await enumerator.MoveNextAsync().ConfigureAwait(true)).ShouldBeTrue();
        enumerator.Current.Detail.ShouldBe("before");
        var next = enumerator.MoveNextAsync().AsTask();
        source.Publish(Event(StageSource.Gsd, "after"));
        (await next.ConfigureAwait(true)).ShouldBeTrue();
        enumerator.Current.Detail.ShouldBe("after");

        await cancellation.CancelAsync().ConfigureAwait(true);
        (await enumerator.MoveNextAsync().ConfigureAwait(true)).ShouldBeFalse();
    }

    /// <summary>Yields one event that names the omp session of its scope, then stays open until it is cancelled. It records its scopes and how many observations ended.</summary>
    private sealed class HoldingSource : IStageSource
    {
        private readonly System.Threading.Lock _gate = new();
        private readonly List<StageObservationScope> _scopes = [];
        private int _ended;

        public IReadOnlyList<StageObservationScope> Scopes
        {
            get
            {
                lock (_gate)
                {
                    return [.. _scopes];
                }
            }
        }

        public int Ended => Volatile.Read(ref _ended);

        public async IAsyncEnumerable<StageEvent> ObserveAsync(StageObservationScope scope, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                _scopes.Add(scope);
            }

            try
            {
                yield return Event(StageSource.Ultrathink, scope.AcpSessionId ?? "none");
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                Interlocked.Increment(ref _ended);
            }
        }
    }

    private sealed class FiniteSource : IStageSource
    {
        public async IAsyncEnumerable<StageEvent> ObserveAsync(StageObservationScope scope, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.Yield();
            yield return Event(StageSource.Ultrathink, "one");
            yield return Event(StageSource.Ultrathink, "two");
        }
    }

    private sealed class ListSource(IReadOnlyList<StageEvent> events) : IStageSource
    {
        public async IAsyncEnumerable<StageEvent> ObserveAsync(StageObservationScope scope, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.Yield();
            foreach (var stageEvent in events)
            {
                yield return stageEvent;
            }
        }
    }

    private sealed class CapturingContextSource : ITaskContextSource
    {
        public List<StageObservationScope> Scopes { get; } = [];

        public Task<TaskContext> GetAsync(StageObservationScope scope, CancellationToken cancellationToken)
        {
            Scopes.Add(scope);
            return Task.FromResult(new TaskContext { Title = "context", SharedMemory = [], Lessons = [], Warnings = [] });
        }
    }
}
