using Xunit;
using Autonogrammer.Contracts.Dispatch;
using Shouldly;

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

        var moved = await store.TryTransitionAsync("acct", "c0", DispatchCommandState.Queued, DispatchCommandState.Delivered, null, ct).ConfigureAwait(true);
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

        var applied = await store.TryTransitionAsync("acct", "c1", DispatchCommandState.Queued, DispatchCommandState.Delivered, null, ct).ConfigureAwait(true);
        applied.Status.ShouldBe(DispatchTransitionStatus.Applied);
        var illegal = await store.TryTransitionAsync("acct", "c1", DispatchCommandState.Delivered, DispatchCommandState.Completed, null, ct).ConfigureAwait(true);
        illegal.Status.ShouldBe(DispatchTransitionStatus.Rejected);
        (await store.GetCommandAsync("acct", "c1", ct).ConfigureAwait(true))!.State.ShouldBe(DispatchCommandState.Delivered);

        await store.TryTransitionAsync("acct", "c1", DispatchCommandState.Delivered, DispatchCommandState.Received, null, ct).ConfigureAwait(true);
        await store.TryTransitionAsync("acct", "c1", DispatchCommandState.Received, DispatchCommandState.Accepted, null, ct).ConfigureAwait(true);
        await store.TryTransitionAsync("acct", "c1", DispatchCommandState.Accepted, DispatchCommandState.Running, null, ct).ConfigureAwait(true);
        var done = await store.TryTransitionAsync("acct", "c1", DispatchCommandState.Running, DispatchCommandState.Completed, null, ct).ConfigureAwait(true);
        done.Status.ShouldBe(DispatchTransitionStatus.Applied);
        var replay = await store.TryTransitionAsync("acct", "c1", DispatchCommandState.Completed, DispatchCommandState.Failed, DispatchFailureReason.Internal, ct).ConfigureAwait(true);
        replay.Status.ShouldBe(DispatchTransitionStatus.Rejected);
        (await store.GetCommandAsync("acct", "c1", ct).ConfigureAwait(true))!.State.ShouldBe(DispatchCommandState.Completed);
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
