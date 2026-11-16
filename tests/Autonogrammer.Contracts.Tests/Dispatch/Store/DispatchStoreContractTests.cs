using Autonogrammer.Contracts.Dispatch;
using Shouldly;
using Xunit;

namespace Autonogrammer.Contracts.Tests.Dispatch.Store;

/// <summary>
/// Behavioral contract every <see cref="IDispatchStore"/> must satisfy. The in-memory store and the Postgres store both derive from this class.
/// </summary>
/// <typeparam name="TStore">The store type under test.</typeparam>
public abstract class DispatchStoreContractTests<TStore> where TStore : IDispatchStore
{
    /// <summary>Creates an empty store.</summary>
    /// <param name="limits">Caps and retention.</param>
    /// <param name="time">Clock the store uses for retention.</param>
    /// <param name="cancellationToken">Cancels creation.</param>
    /// <returns>An empty store.</returns>
    protected abstract Task<TStore> CreateStoreAsync(DispatchStoreLimits limits, TimeProvider time, CancellationToken cancellationToken);

    /// <summary>Sequence numbers start at 1 and have no gaps. The pair (device, seq) is unique.</summary>
    [Fact]
    public async Task SeqIsStrictlyIncreasingWithoutGaps()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = await CreateStoreAsync(DispatchStoreLimits.Default, TimeProvider.System, ct).ConfigureAwait(true);
        await CreateDeviceAsync(store, "acct", "dev", ct).ConfigureAwait(true);

        var first = await EnqueueAsync(store, "acct", "dev", "c1", "k1", "fp", ct).ConfigureAwait(true);
        var second = await EnqueueAsync(store, "acct", "dev", "c2", "k2", "fp", ct).ConfigureAwait(true);

        first.Accepted!.Seq.ShouldBe(1);
        second.Accepted!.Seq.ShouldBe(2);
        var loaded = await store.GetCommandAsync("acct", "c2", ct).ConfigureAwait(true);
        loaded!.Seq.ShouldBe(2);
    }

    /// <summary>The 101st queued command is QueueFull. A delivered command no longer occupies the cap.</summary>
    [Fact]
    public async Task QueueCapIsAtomic()
    {
        var ct = TestContext.Current.CancellationToken;
        var limits = DispatchStoreLimits.Default with { MaxQueuedPerDevice = 100 };
        var store = await CreateStoreAsync(limits, TimeProvider.System, ct).ConfigureAwait(true);
        await CreateDeviceAsync(store, "acct", "dev", ct).ConfigureAwait(true);
        for (var i = 0; i < 100; i++)
        {
            var accepted = await EnqueueAsync(store, "acct", "dev", "c" + i.ToString(System.Globalization.CultureInfo.InvariantCulture), "k" + i.ToString(System.Globalization.CultureInfo.InvariantCulture), "fp", ct).ConfigureAwait(true);
            accepted.Status.ShouldBe(DispatchEnqueueStatus.Accepted);
        }

        var full = await EnqueueAsync(store, "acct", "dev", "overflow", "k-over", "fp", ct).ConfigureAwait(true);
        full.Status.ShouldBe(DispatchEnqueueStatus.QueueFull);

        var moved = await store.TryTransitionAsync("acct", "c0", DispatchCommandState.Queued, DispatchCommandState.Delivered, null, null, ct).ConfigureAwait(true);
        moved.Status.ShouldBe(DispatchTransitionStatus.Applied);
        var after = await EnqueueAsync(store, "acct", "dev", "overflow", "k-over", "fp", ct).ConfigureAwait(true);
        after.Status.ShouldBe(DispatchEnqueueStatus.Accepted);
    }

    /// <summary>The same key and fingerprint returns the original 202 body. A different fingerprint is reused.</summary>
    [Fact]
    public async Task IdempotencyReplaysOriginalCommand()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = await CreateStoreAsync(DispatchStoreLimits.Default, TimeProvider.System, ct).ConfigureAwait(true);
        await CreateDeviceAsync(store, "acct", "dev", ct).ConfigureAwait(true);
        var first = await EnqueueAsync(store, "acct", "dev", "c1", "same", "fp-a", ct).ConfigureAwait(true);
        var replay = await EnqueueAsync(store, "acct", "dev", "c2", "same", "fp-a", ct).ConfigureAwait(true);
        var reused = await EnqueueAsync(store, "acct", "dev", "c3", "same", "fp-b", ct).ConfigureAwait(true);

        replay.Status.ShouldBe(DispatchEnqueueStatus.Replayed);
        replay.Accepted!.CommandId.ShouldBe(first.Accepted!.CommandId);
        replay.Accepted.Seq.ShouldBe(first.Accepted.Seq);
        replay.Accepted.State.ShouldBe(first.Accepted.State);
        reused.Status.ShouldBe(DispatchEnqueueStatus.IdempotencyKeyReused);
        (await store.GetCommandAsync("acct", "c3", ct).ConfigureAwait(true)).ShouldBeNull();
    }

    /// <summary>Compare-and-set follows the table. A completed command cannot be replayed into another state.</summary>
    [Fact]
    public async Task TransitionsAreCompareAndSet()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = await CreateStoreAsync(DispatchStoreLimits.Default, TimeProvider.System, ct).ConfigureAwait(true);
        await CreateDeviceAsync(store, "acct", "dev", ct).ConfigureAwait(true);
        await EnqueueAsync(store, "acct", "dev", "c1", "k", "fp", ct).ConfigureAwait(true);

        var applied = await store.TryTransitionAsync("acct", "c1", DispatchCommandState.Queued, DispatchCommandState.Delivered, null, null, ct).ConfigureAwait(true);
        applied.Status.ShouldBe(DispatchTransitionStatus.Applied);
        var illegal = await store.TryTransitionAsync("acct", "c1", DispatchCommandState.Delivered, DispatchCommandState.Completed, null, null, ct).ConfigureAwait(true);
        illegal.Status.ShouldBe(DispatchTransitionStatus.Rejected);
        (await store.GetCommandAsync("acct", "c1", ct).ConfigureAwait(true))!.State.ShouldBe(DispatchCommandState.Delivered);

        await store.TryTransitionAsync("acct", "c1", DispatchCommandState.Delivered, DispatchCommandState.Received, null, null, ct).ConfigureAwait(true);
        await store.TryTransitionAsync("acct", "c1", DispatchCommandState.Received, DispatchCommandState.Accepted, null, null, ct).ConfigureAwait(true);
        await store.TryTransitionAsync("acct", "c1", DispatchCommandState.Accepted, DispatchCommandState.Running, null, null, ct).ConfigureAwait(true);
        var done = await store.TryTransitionAsync("acct", "c1", DispatchCommandState.Running, DispatchCommandState.Completed, null, null, ct, "result").ConfigureAwait(true);
        done.Status.ShouldBe(DispatchTransitionStatus.Applied);
        var replay = await store.TryTransitionAsync("acct", "c1", DispatchCommandState.Completed, DispatchCommandState.Failed, DispatchFailureReason.Internal, null, ct).ConfigureAwait(true);
        replay.Status.ShouldBe(DispatchTransitionStatus.Rejected);
        (await store.GetCommandAsync("acct", "c1", ct).ConfigureAwait(true))!.State.ShouldBe(DispatchCommandState.Completed);
        (await store.GetCommandAsync("acct", "c1", ct).ConfigureAwait(true))!.ResultText.ShouldBe("result");
    }

    /// <summary>Terminal result text is not returned from either read path after body retention expires.</summary>
    [Fact]
    public async Task ResultTextExpiresFromListsAndSingleCommandReads()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new MutableTime();
        var store = await CreateStoreAsync(DispatchStoreLimits.Default, clock, ct).ConfigureAwait(true);
        await CreateDeviceAsync(store, "acct", "dev", ct).ConfigureAwait(true);
        await EnqueueAsync(store, "acct", "dev", "c1", "k1", "fp", ct).ConfigureAwait(true);
        await MoveToRunningAsync(store, "acct", "c1", ct).ConfigureAwait(true);
        (await store.TryTransitionAsync("acct", "c1", DispatchCommandState.Running, DispatchCommandState.Completed, null, null, ct, "private result").ConfigureAwait(true)).Status.ShouldBe(DispatchTransitionStatus.Applied);
        (await store.ListCommandsAsync("acct", null, 10, ct).ConfigureAwait(true)).Single().ResultText.ShouldBe("private result");
        clock.UtcNow += DispatchStoreLimits.Default.BodyRetention + TimeSpan.FromSeconds(1);
        (await store.ListCommandsAsync("acct", null, 10, ct).ConfigureAwait(true)).Single().ResultText.ShouldBeNull();
        (await store.GetCommandAsync("acct", "c1", ct).ConfigureAwait(true))!.ResultText.ShouldBeNull();
    }

    /// <summary>Concurrent progress writes share one cap and cannot write for another device or after completion.</summary>
    [Fact]
    public async Task ConcurrentProgressIsCappedPerActiveOwnedCommand()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = await CreateStoreAsync(DispatchStoreLimits.Default, TimeProvider.System, ct).ConfigureAwait(true);
        await CreateDeviceAsync(store, "acct", "dev", ct).ConfigureAwait(true);
        await EnqueueAsync(store, "acct", "dev", "c1", "k1", "fp", ct).ConfigureAwait(true);
        await MoveToRunningAsync(store, "acct", "c1", ct).ConfigureAwait(true);
        (await store.TryAppendProgressAsync("acct", "other", "c1", "{}", ct).ConfigureAwait(true)).ShouldBeFalse();
        var attempts = Enumerable.Range(0, 64).Select(_ => store.TryAppendProgressAsync("acct", "dev", "c1", "{}", ct).AsTask()).ToArray();
        (await Task.WhenAll(attempts).ConfigureAwait(true)).Count(static applied => applied).ShouldBe(50);
        (await store.ReadFeedAsync("acct", 0, ct).ConfigureAwait(true)).Events.Count(item => string.Equals(item.Kind, "command.evt", StringComparison.Ordinal)).ShouldBe(50);
        await EnqueueAsync(store, "acct", "dev", "c2", "k2", "fp", ct).ConfigureAwait(true);
        await MoveToRunningAsync(store, "acct", "c2", ct).ConfigureAwait(true);
        (await store.TryTransitionAsync("acct", "c2", DispatchCommandState.Running, DispatchCommandState.Completed, null, null, ct).ConfigureAwait(true)).Status.ShouldBe(DispatchTransitionStatus.Applied);
        (await store.TryAppendProgressAsync("acct", "dev", "c2", "{}", ct).ConfigureAwait(true)).ShouldBeFalse();
    }

    /// <summary>A timeout and its cancellation commit once even when the ordinary queue is full and sweepers race.</summary>
    [Fact]
    public async Task TimeoutAtomicallyEnqueuesOneCancellationPastTheQueueCap()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new MutableTime();
        var limits = DispatchStoreLimits.Default with { MaxQueuedPerDevice = 1 };
        var store = await CreateStoreAsync(limits, clock, ct).ConfigureAwait(true);
        await CreateDeviceAsync(store, "acct", "dev", ct).ConfigureAwait(true);
        await EnqueueAsync(store, "acct", "dev", "target", "target-key", "fp", ct).ConfigureAwait(true);
        await MoveToRunningAsync(store, "acct", "target", ct).ConfigureAwait(true);
        var queued = new EnqueueDispatchCommand("acct", "dev", "queued", "queued-key", "fp", DispatchCapability.Sessions, DispatchOp.SessionList, "{}", clock.UtcNow, clock.UtcNow.AddMinutes(5));
        (await store.EnqueueAsync(queued, ct).ConfigureAwait(true)).Status.ShouldBe(DispatchEnqueueStatus.Accepted);
        var cancel = new EnqueueDispatchCommand("acct", "dev", "cancel", " dispatch.timeout:target", "fp", DispatchCapability.Sessions, DispatchOp.CommandCancel, "{\"commandId\":\"target\"}", clock.UtcNow, clock.UtcNow.AddMinutes(5));
        var feed = new DispatchFeedDraft("command.state", "{\"state\":\"failed\"}");
        var wrongDevice = cancel with { DeviceId = "other", CommandId = "wrong" };
        (await store.TryTimeoutAndEnqueueAsync("acct", "target", DispatchCommandState.Running, wrongDevice, feed, ct).ConfigureAwait(true)).Status.ShouldBe(DispatchTransitionStatus.Rejected);
        var attempts = Enumerable.Range(0, 8).Select(_ => store.TryTimeoutAndEnqueueAsync("acct", "target", DispatchCommandState.Running, cancel, feed, ct).AsTask()).ToArray();
        (await Task.WhenAll(attempts).ConfigureAwait(true)).Count(static result => result.Status == DispatchTransitionStatus.Applied).ShouldBe(1);
        var target = (await store.GetCommandAsync("acct", "target", ct).ConfigureAwait(true))!;
        target.State.ShouldBe(DispatchCommandState.Failed);
        target.Reason.ShouldBe(DispatchFailureReason.Timeout);
        var rows = await store.ListQueuedAfterAsync("acct", "dev", 0, ct).ConfigureAwait(true);
        rows.Select(static row => row.CommandId).ShouldBe(["queued", "cancel"]);
        rows[1].Op.ShouldBe(DispatchOp.CommandCancel);
        (await store.ReadFeedAsync("acct", 0, ct).ConfigureAwait(true)).Events.Count.ShouldBe(1);
        await MoveToRunningAsync(store, "acct", "queued", ct).ConfigureAwait(true);
        (await store.TryTransitionAsync("acct", "queued", DispatchCommandState.Running, DispatchCommandState.Completed, null, null, ct).ConfigureAwait(true)).Status.ShouldBe(DispatchTransitionStatus.Applied);
        var stale = cancel with { CommandId = "stale-cancel", IdempotencyKey = " dispatch.timeout:queued", BodyJson = "{\"commandId\":\"queued\"}" };
        (await store.TryTimeoutAndEnqueueAsync("acct", "queued", DispatchCommandState.Running, stale, feed, ct).ConfigureAwait(true)).Status.ShouldBe(DispatchTransitionStatus.Rejected);
        (await store.GetCommandAsync("acct", "stale-cancel", ct).ConfigureAwait(true)).ShouldBeNull();
    }

    /// <summary>A controller cancellation leaves a running target unchanged until the device reports its outcome.</summary>
    [Fact]
    public async Task RunningCancellationIsQueuedOnceEvenWhenTheQueueIsFull()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new MutableTime();
        var store = await CreateStoreAsync(DispatchStoreLimits.Default with { MaxQueuedPerDevice = 1 }, clock, ct).ConfigureAwait(true);
        await CreateDeviceAsync(store, "acct", "dev", ct).ConfigureAwait(true);
        await EnqueueAsync(store, "acct", "dev", "target", "target-key", "fp", ct).ConfigureAwait(true);
        await MoveToRunningAsync(store, "acct", "target", ct).ConfigureAwait(true);
        var queued = new EnqueueDispatchCommand("acct", "dev", "queued", "queued-key", "fp", DispatchCapability.Sessions, DispatchOp.SessionList, "{}", clock.UtcNow, clock.UtcNow.AddMinutes(5));
        (await store.EnqueueAsync(queued, ct).ConfigureAwait(true)).Status.ShouldBe(DispatchEnqueueStatus.Accepted);
        var cancel = new EnqueueDispatchCommand("acct", "dev", "cancel", " dispatch.cancel:target:1", "fp", DispatchCapability.Sessions, DispatchOp.CommandCancel, "{\"commandId\":\"target\"}", clock.UtcNow, clock.UtcNow.AddMinutes(5));
        var wrongDevice = cancel with { DeviceId = "other", CommandId = "wrong" };
        (await store.TryRequestCancelAsync("acct", "target", wrongDevice, ct).ConfigureAwait(true)).Status.ShouldBe(DispatchTransitionStatus.Rejected);
        var attempts = Enumerable.Range(0, 8).Select(_ => store.TryRequestCancelAsync("acct", "target", cancel, ct).AsTask()).ToArray();
        (await Task.WhenAll(attempts).ConfigureAwait(true)).Count(static result => result.Status == DispatchTransitionStatus.Applied).ShouldBe(1);
        (await store.GetCommandAsync("acct", "target", ct).ConfigureAwait(true))!.State.ShouldBe(DispatchCommandState.Running);
        var rows = await store.ListQueuedAfterAsync("acct", "dev", 0, ct).ConfigureAwait(true);
        rows.Select(static row => row.CommandId).ShouldBe(["queued", "cancel"]);
        (await store.TryTransitionAsync("acct", "target", DispatchCommandState.Running, DispatchCommandState.Completed, null, null, ct).ConfigureAwait(true)).Status.ShouldBe(DispatchTransitionStatus.Applied);
        var stale = cancel with { CommandId = "stale", IdempotencyKey = " dispatch.cancel:target:2" };
        (await store.TryRequestCancelAsync("acct", "target", stale, ct).ConfigureAwait(true)).Status.ShouldBe(DispatchTransitionStatus.Rejected);
        (await store.GetCommandAsync("acct", "stale", ct).ConfigureAwait(true)).ShouldBeNull();
    }

    /// <summary>An in-flight cancellation request never fabricates a terminal result.</summary>
    [Theory]
    [InlineData(DispatchCommandState.Delivered)]
    [InlineData(DispatchCommandState.Received)]
    [InlineData(DispatchCommandState.AwaitingApproval)]
    [InlineData(DispatchCommandState.Accepted)]
    public async Task CancellationOfInFlightCommandWaitsForTheDevice(DispatchCommandState state)
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new MutableTime();
        var store = await CreateStoreAsync(DispatchStoreLimits.Default, clock, ct).ConfigureAwait(true);
        await CreateDeviceAsync(store, "acct", "dev", ct).ConfigureAwait(true);
        await EnqueueAsync(store, "acct", "dev", "target", "target-key", "fp", ct).ConfigureAwait(true);
        (await store.TryTransitionAsync("acct", "target", DispatchCommandState.Queued, DispatchCommandState.Delivered, null, null, ct).ConfigureAwait(true)).Status.ShouldBe(DispatchTransitionStatus.Applied);
        if (state != DispatchCommandState.Delivered)
        {
            (await store.TryTransitionAsync("acct", "target", DispatchCommandState.Delivered, DispatchCommandState.Received, null, null, ct).ConfigureAwait(true)).Status.ShouldBe(DispatchTransitionStatus.Applied);
        }
        if (state is DispatchCommandState.AwaitingApproval or DispatchCommandState.Accepted)
        {
            (await store.TryTransitionAsync("acct", "target", DispatchCommandState.Received, state, null, null, ct).ConfigureAwait(true)).Status.ShouldBe(DispatchTransitionStatus.Applied);
        }

        var control = new EnqueueDispatchCommand("acct", "dev", "cancel", " dispatch.cancel:target:1", "fp", DispatchCapability.Sessions, DispatchOp.CommandCancel, "{\"commandId\":\"target\"}", clock.UtcNow, clock.UtcNow.AddMinutes(5));
        (await store.TryRequestCancelAsync("acct", "target", control, ct).ConfigureAwait(true)).Status.ShouldBe(DispatchTransitionStatus.Applied);
        (await store.GetCommandAsync("acct", "target", ct).ConfigureAwait(true))!.State.ShouldBe(state);
        (await store.ListQueuedAfterAsync("acct", "dev", 0, ct).ConfigureAwait(true)).Any(static row => string.Equals(row.CommandId, "cancel", StringComparison.Ordinal)).ShouldBeTrue();
    }

    /// <summary>A source prefix is recorded for a known device only, and reads back on that device.</summary>
    [Fact]
    public async Task SourcePrefixIsRecordedForKnownDevicesOnly()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = await CreateStoreAsync(DispatchStoreLimits.Default, TimeProvider.System, ct).ConfigureAwait(true);
        await CreateDeviceAsync(store, "acct", "dev", ct).ConfigureAwait(true);

        (await store.RecordSourcePrefixAsync("acct", "dev", "203.0.113.0/24", ct).ConfigureAwait(true)).ShouldBeTrue();
        (await store.GetDeviceAsync("acct", "dev", ct).ConfigureAwait(true))!.LastSourcePrefix.ShouldBe("203.0.113.0/24");
        (await store.RecordSourcePrefixAsync("acct", "missing", "198.51.100.0/24", ct).ConfigureAwait(true)).ShouldBeFalse();
        (await store.RecordSourcePrefixAsync("other", "dev", "198.51.100.0/24", ct).ConfigureAwait(true)).ShouldBeFalse();
        (await store.GetDeviceAsync("acct", "dev", ct).ConfigureAwait(true))!.LastSourcePrefix.ShouldBe("203.0.113.0/24");
    }

    /// <summary>Only overdue commands come back, oldest state entry first, capped by the limit. A command still inside its run limit never appears.</summary>
    [Fact]
    public async Task SweepListsOnlyOverdueCommandsOldestStateEntryFirstAndCapped()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new MutableTime();
        var store = await CreateStoreAsync(DispatchStoreLimits.Default, clock, ct).ConfigureAwait(true);
        await CreateDeviceAsync(store, "acct", "dev", ct).ConfigureAwait(true);
        await EnqueueAsync(store, "acct", "dev", "c1", "k1", "fp", ct).ConfigureAwait(true);
        await EnqueueAsync(store, "acct", "dev", "c2", "k2", "fp", ct).ConfigureAwait(true);
        await EnqueueAsync(store, "acct", "dev", "c3", "k3", "fp", ct).ConfigureAwait(true);

        clock.UtcNow = clock.UtcNow.AddMinutes(1);
        await store.TryTransitionAsync("acct", "c1", DispatchCommandState.Queued, DispatchCommandState.Delivered, null, null, ct).ConfigureAwait(true);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);
        await MoveToRunningAsync(store, "acct", "c2", ct).ConfigureAwait(true);

        clock.UtcNow = clock.UtcNow.AddSeconds(30);
        var cutoffs = new DispatchSweepCutoffs(clock.UtcNow, clock.UtcNow.AddMinutes(-5), clock.UtcNow.AddSeconds(-60), clock.UtcNow.AddHours(-4));
        var overdue = await store.ListOverdueAsync(cutoffs, 10, ct).ConfigureAwait(true);
        overdue.Select(candidate => candidate.CommandId).ShouldBe(["c3", "c1"]);
        overdue[0].State.ShouldBe(DispatchCommandState.Queued);
        overdue[1].State.ShouldBe(DispatchCommandState.Delivered);
        (await store.ListOverdueAsync(cutoffs, 1, ct).ConfigureAwait(true)).Select(candidate => candidate.CommandId).ShouldBe(["c3"]);
    }

    /// <summary>Restart reconciliation pages all in-flight rows, including rows older than the first 100, without exposing another account.</summary>
    [Fact]
    public async Task InFlightPagesReachPastOneHundredAndRemainAccountScoped()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = await CreateStoreAsync(DispatchStoreLimits.Default, TimeProvider.System, ct).ConfigureAwait(true);
        await CreateDeviceAsync(store, "acct", "dev", ct).ConfigureAwait(true);
        for (var index = 1; index <= 101; index++)
        {
            var id = "inflight-" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            (await EnqueueAsync(store, "acct", "dev", id, "key-" + id, "fp", ct).ConfigureAwait(true)).Status.ShouldBe(DispatchEnqueueStatus.Accepted);
            (await store.TryTransitionAsync("acct", id, DispatchCommandState.Queued, DispatchCommandState.Delivered, null, null, ct).ConfigureAwait(true)).Status.ShouldBe(DispatchTransitionStatus.Applied);
            (await store.TryTransitionAsync("acct", id, DispatchCommandState.Delivered, DispatchCommandState.Received, null, null, ct).ConfigureAwait(true)).Status.ShouldBe(DispatchTransitionStatus.Applied);
        }

        var first = await store.ListInFlightAfterAsync("acct", "dev", 0, 100, ct).ConfigureAwait(true);
        first.Count.ShouldBe(100);
        first[0].Seq.ShouldBe(1);
        first[^1].Seq.ShouldBe(100);
        var second = await store.ListInFlightAfterAsync("acct", "dev", first[^1].Seq, 100, ct).ConfigureAwait(true);
        second.Select(row => row.Seq).ShouldBe([101L]);
        (await store.ListInFlightAfterAsync("other", "dev", 0, 100, ct).ConfigureAwait(true)).ShouldBeEmpty();
    }

    /// <summary>Later run-phase transitions do not restart the deadline measured from receipt.</summary>
    [Fact]
    public async Task RunDeadlineStartsAtReceiptNotMostRecentTransition()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new MutableTime();
        var store = await CreateStoreAsync(DispatchStoreLimits.Default, clock, ct).ConfigureAwait(true);
        await CreateDeviceAsync(store, "acct", "dev", ct).ConfigureAwait(true);
        await EnqueueAsync(store, "acct", "dev", "deadline", "deadline-key", "fp", ct).ConfigureAwait(true);
        (await store.TryTransitionAsync("acct", "deadline", DispatchCommandState.Queued, DispatchCommandState.Delivered, null, null, ct).ConfigureAwait(true)).Status.ShouldBe(DispatchTransitionStatus.Applied);
        (await store.TryTransitionAsync("acct", "deadline", DispatchCommandState.Delivered, DispatchCommandState.Received, null, null, ct).ConfigureAwait(true)).Status.ShouldBe(DispatchTransitionStatus.Applied);
        clock.UtcNow = clock.UtcNow.AddSeconds(59);
        (await store.TryTransitionAsync("acct", "deadline", DispatchCommandState.Received, DispatchCommandState.Accepted, null, null, ct).ConfigureAwait(true)).Status.ShouldBe(DispatchTransitionStatus.Applied);
        (await store.TryTransitionAsync("acct", "deadline", DispatchCommandState.Accepted, DispatchCommandState.Running, null, null, ct).ConfigureAwait(true)).Status.ShouldBe(DispatchTransitionStatus.Applied);
        var before = new DispatchSweepCutoffs(clock.UtcNow, clock.UtcNow.AddMinutes(-5), clock.UtcNow.AddSeconds(-60), clock.UtcNow.AddHours(-4));
        (await store.ListOverdueAsync(before, 10, ct).ConfigureAwait(true)).ShouldBeEmpty();
        clock.UtcNow = clock.UtcNow.AddSeconds(2);
        var after = new DispatchSweepCutoffs(clock.UtcNow, clock.UtcNow.AddMinutes(-5), clock.UtcNow.AddSeconds(-60), clock.UtcNow.AddHours(-4));
        (await store.ListOverdueAsync(after, 10, ct).ConfigureAwait(true)).Select(candidate => candidate.CommandId).ShouldBe(["deadline"]);
    }

    /// <summary>A transition and its feed event commit together: an applied transition appends its event, and a rejected one appends nothing.</summary>
    [Fact]
    public async Task TransitionAppendsItsFeedEventOnlyWhenItApplies()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = await CreateStoreAsync(DispatchStoreLimits.Default, TimeProvider.System, ct).ConfigureAwait(true);
        await CreateDeviceAsync(store, "acct", "dev", ct).ConfigureAwait(true);
        await EnqueueAsync(store, "acct", "dev", "c1", "k1", "fp", ct).ConfigureAwait(true);

        var rejected = await store.TryTransitionAsync("acct", "c1", DispatchCommandState.Delivered, DispatchCommandState.Received, null, new DispatchFeedDraft("command.state", "{\"state\":\"received\"}"), ct).ConfigureAwait(true);
        rejected.Status.ShouldBe(DispatchTransitionStatus.Rejected);
        (await store.ReadFeedAsync("acct", 0, ct).ConfigureAwait(true)).Events.ShouldBeEmpty();

        var applied = await store.TryTransitionAsync("acct", "c1", DispatchCommandState.Queued, DispatchCommandState.Delivered, null, new DispatchFeedDraft("command.state", "{\"state\":\"delivered\"}"), ct).ConfigureAwait(true);
        applied.Status.ShouldBe(DispatchTransitionStatus.Applied);
        var events = (await store.ReadFeedAsync("acct", 0, ct).ConfigureAwait(true)).Events;
        events.Select(item => item.Kind).ShouldBe(["command.state"]);
        using var payload = System.Text.Json.JsonDocument.Parse(events[0].Payload!);
        payload.RootElement.GetProperty("state").GetString().ShouldBe("delivered");
    }


    /// <summary>The 21st non-revoked device is refused. A revoked device stops counting.</summary>
    [Fact]
    public async Task DeviceCapIsTwentyUnlessRevoked()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = await CreateStoreAsync(DispatchStoreLimits.Default, TimeProvider.System, ct).ConfigureAwait(true);
        for (var i = 0; i < 20; i++)
        {
            var created = await CreateDeviceAsync(store, "acct", "d" + i.ToString(System.Globalization.CultureInfo.InvariantCulture), ct).ConfigureAwait(true);
            created.Status.ShouldBe(DispatchDeviceCreateStatus.Created);
        }

        var blocked = await CreateDeviceAsync(store, "acct", "d20", ct).ConfigureAwait(true);
        blocked.Status.ShouldBe(DispatchDeviceCreateStatus.DeviceLimitReached);
        (await store.RevokeDeviceAsync("acct", "d0", "revoked", DateTimeOffset.UnixEpoch, ct).ConfigureAwait(true)).ShouldBeTrue();
        var after = await CreateDeviceAsync(store, "acct", "d20", ct).ConfigureAwait(true);
        after.Status.ShouldBe(DispatchDeviceCreateStatus.Created);
    }

    /// <summary>A foreign account reads and changes nothing.</summary>
    [Fact]
    public async Task ForeignAccountReadsNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = await CreateStoreAsync(DispatchStoreLimits.Default, TimeProvider.System, ct).ConfigureAwait(true);
        await CreateDeviceAsync(store, "acct", "dev", ct).ConfigureAwait(true);
        await EnqueueAsync(store, "acct", "dev", "c1", "k", "fp", ct).ConfigureAwait(true);
        await store.AppendFeedEventAsync("acct", "queued", "{}", ct).ConfigureAwait(true);

        (await store.GetDeviceAsync("other", "dev", ct).ConfigureAwait(true)).ShouldBeNull();
        (await store.ListDevicesAsync("other", ct).ConfigureAwait(true)).ShouldBeEmpty();
        (await store.RenameDeviceAsync("other", "dev", "nope", ct).ConfigureAwait(true)).ShouldBeFalse();
        (await store.GetDeviceAsync("acct", "dev", ct).ConfigureAwait(true))!.Name.ShouldBe("dev");
        (await store.GetCommandAsync("other", "c1", ct).ConfigureAwait(true)).ShouldBeNull();
        var foreignEnqueue = await EnqueueAsync(store, "other", "dev", "c2", "k2", "fp", ct).ConfigureAwait(true);
        foreignEnqueue.Status.ShouldBe(DispatchEnqueueStatus.NotFound);
        var page = await store.ReadFeedAsync("other", 0, ct).ConfigureAwait(true);
        page.Events.ShouldBeEmpty();
        page.Resync.ShouldBeFalse();
    }

    /// <summary>Credential rotation is visible to the owner and invisible to a foreign account.</summary>
    [Fact]
    public async Task CredentialJtiRotatesForOwnerOnly()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = await CreateStoreAsync(DispatchStoreLimits.Default, TimeProvider.System, ct).ConfigureAwait(true);
        await CreateDeviceAsync(store, "acct", "dev", ct).ConfigureAwait(true);
        var expiry = new DateTimeOffset(2026, 10, 30, 1, 0, 0, TimeSpan.Zero);
        (await store.UpdateCredentialJtiAsync("other", "dev", "jti-2", expiry, ct).ConfigureAwait(true)).ShouldBeFalse();
        (await store.UpdateCredentialJtiAsync("acct", "dev", "jti-2", expiry, ct).ConfigureAwait(true)).ShouldBeTrue();
        var row = await store.GetDeviceAsync("acct", "dev", ct).ConfigureAwait(true);
        row!.CredentialJti.ShouldBe("jti-2");
        row.CredentialExpiresAt.ShouldBe(expiry);
    }

    /// <summary>Feed identifiers increase per account. A pruned position is a resync. Accounts are isolated.</summary>
    [Fact]
    public async Task FeedResumesPrunesAndIsolates()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new MutableTime();
        var limits = DispatchStoreLimits.Default with { FeedRetention = TimeSpan.FromDays(30) };
        var store = await CreateStoreAsync(limits, clock, ct).ConfigureAwait(true);
        var first = await store.AppendFeedEventAsync("acct", "queued", "{}", ct).ConfigureAwait(true);
        var second = await store.AppendFeedEventAsync("acct", "delivered", "{}", ct).ConfigureAwait(true);
        first.Id.ShouldBe(1);
        second.Id.ShouldBe(2);
        var resume = await store.ReadFeedAsync("acct", 1, ct).ConfigureAwait(true);
        resume.Resync.ShouldBeFalse();
        resume.Events.Count.ShouldBe(1);
        resume.Events[0].Id.ShouldBe(2);

        var other = await store.AppendFeedEventAsync("other", "queued", "{}", ct).ConfigureAwait(true);
        other.Id.ShouldBe(1);
        (await store.ReadFeedAsync("acct", 0, ct).ConfigureAwait(true)).Events.ShouldAllBe(item => item.AccountId == "acct");

        clock.UtcNow = clock.UtcNow.AddDays(30).AddTicks(1);
        var pruned = await store.ReadFeedAsync("acct", 0, ct).ConfigureAwait(true);
        pruned.Resync.ShouldBeTrue();
        pruned.Events.ShouldBeEmpty();
    }

    /// <summary>Revoking a device cancels its queued and delivered commands and fails its running ones with reason revoked, in the same call that hides its queue (D-30).</summary>
    [Fact]
    public async Task RevokeCancelsOpenCommandsInTheSameStep()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = await CreateStoreAsync(DispatchStoreLimits.Default, TimeProvider.System, ct).ConfigureAwait(true);
        await CreateDeviceAsync(store, "acct", "dev", ct).ConfigureAwait(true);
        await EnqueueAsync(store, "acct", "dev", "queued", "k1", "fp", ct).ConfigureAwait(true);
        await EnqueueAsync(store, "acct", "dev", "running", "k2", "fp", ct).ConfigureAwait(true);
        await MoveToRunningAsync(store, "acct", "running", ct).ConfigureAwait(true);

        (await store.RevokeDeviceAsync("acct", "dev", "revoked", DateTimeOffset.UnixEpoch, ct).ConfigureAwait(true)).ShouldBeTrue();

        var rows = await store.ListCommandsAsync("acct", "dev", 10, ct).ConfigureAwait(true);
        var queued = rows.Single(row => string.Equals(row.CommandId, "queued", StringComparison.Ordinal));
        queued.State.ShouldBe(DispatchCommandState.Cancelled);
        queued.Reason.ShouldBe(DispatchFailureReason.Revoked);
        var running = rows.Single(row => string.Equals(row.CommandId, "running", StringComparison.Ordinal));
        running.State.ShouldBe(DispatchCommandState.Failed);
        running.Reason.ShouldBe(DispatchFailureReason.Revoked);
        (await store.ListQueuedAfterAsync("acct", "dev", 0, ct).ConfigureAwait(true)).ShouldBeEmpty();
    }

    /// <summary>ReplayAsync answers a stored key with the original body for the same fingerprint, refuses another fingerprint, and answers an unknown key with null (D-21).</summary>
    [Fact]
    public async Task ReplayAnswersStoredKeysAndNothingElse()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = await CreateStoreAsync(DispatchStoreLimits.Default, TimeProvider.System, ct).ConfigureAwait(true);
        await CreateDeviceAsync(store, "acct", "dev", ct).ConfigureAwait(true);
        await EnqueueAsync(store, "acct", "dev", "c1", "same", "fp-a", ct).ConfigureAwait(true);

        var replayed = await store.ReplayAsync(Command("acct", "dev", "c2", "same", "fp-a"), ct).ConfigureAwait(true);
        replayed!.Status.ShouldBe(DispatchEnqueueStatus.Replayed);
        var reused = await store.ReplayAsync(Command("acct", "dev", "c3", "same", "fp-b"), ct).ConfigureAwait(true);
        reused!.Status.ShouldBe(DispatchEnqueueStatus.IdempotencyKeyReused);
        (await store.ReplayAsync(Command("acct", "dev", "c4", "unknown", "fp-a"), ct).ConfigureAwait(true)).ShouldBeNull();
    }

    private static async Task MoveToRunningAsync(TStore store, string accountId, string commandId, CancellationToken cancellationToken)
    {
        (DispatchCommandState From, DispatchCommandState To)[] path =
        [
            (DispatchCommandState.Queued, DispatchCommandState.Delivered),
            (DispatchCommandState.Delivered, DispatchCommandState.Received),
            (DispatchCommandState.Received, DispatchCommandState.Accepted),
            (DispatchCommandState.Accepted, DispatchCommandState.Running),
        ];
        foreach (var (from, to) in path)
        {
            (await store.TryTransitionAsync(accountId, commandId, from, to, null, null, cancellationToken).ConfigureAwait(true)).Status.ShouldBe(DispatchTransitionStatus.Applied);
        }
    }

    private static EnqueueDispatchCommand Command(string accountId, string deviceId, string commandId, string key, string fingerprint) =>
        new(accountId, deviceId, commandId, key, fingerprint, DispatchCapability.Sessions, DispatchOp.SessionList, "{}", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddMinutes(5));

    private static async Task<DispatchDeviceCreateResult> CreateDeviceAsync(TStore store, string accountId, string deviceId, CancellationToken cancellationToken)
    {
        return await store.CreateDeviceAsync(
            new CreateDispatchDevice(accountId, deviceId, deviceId, null, "jti", null, [], null),
            cancellationToken).ConfigureAwait(true);
    }

    private static async Task<DispatchEnqueueResult> EnqueueAsync(
        TStore store,
        string accountId,
        string deviceId,
        string commandId,
        string key,
        string fingerprint,
        CancellationToken cancellationToken)
    {
        return await store.EnqueueAsync(
            new EnqueueDispatchCommand(
                accountId,
                deviceId,
                commandId,
                key,
                fingerprint,
                DispatchCapability.Sessions,
                DispatchOp.SessionList,
                "{}",
                DateTimeOffset.UnixEpoch,
                DateTimeOffset.UnixEpoch.AddMinutes(5)),
            cancellationToken).ConfigureAwait(true);
    }

    private sealed class MutableTime : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 10, 30, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}
