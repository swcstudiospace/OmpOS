using OmpOS.Api.Cloud;
using OmpOS.Contracts.Cloud;
using Shouldly;
using Xunit;

namespace OmpOS.Api.IntegrationTests.Cloud;

/// <summary>The in-process agent store: caps and idempotency decided with the insert, atomic updates, and the activation rule.</summary>
public sealed class InMemoryCloudAgentStoreTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 9, 0, 0, TimeSpan.Zero);
    private static readonly CloudAgentLimits Limits = new(MaxAgents: 3, MaxActive: 2);

    /// <summary>A record is stored and read back by id, by session and by owner.</summary>
    [Fact]
    public async Task Create_StoresARecordReadableByIdSessionAndOwner()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = new InMemoryCloudAgentStore();

        var created = await store.CreateAsync(Record("a1", "alice"), Limits, ct).ConfigureAwait(true);

        created.Outcome.ShouldBe(CloudAgentCreateOutcome.Created);
        (await store.GetAsync("a1", ct).ConfigureAwait(true)).ShouldNotBeNull().OwnerId.ShouldBe("alice");
        (await store.GetBySessionAsync("cloud-a1", ct).ConfigureAwait(true)).ShouldNotBeNull().Id.ShouldBe("a1");
        (await store.ListByOwnerAsync("alice", ct).ConfigureAwait(true)).ShouldHaveSingleItem();
        (await store.ListByOwnerAsync("bob", ct).ConfigureAwait(true)).ShouldBeEmpty();
        (await store.GetAsync("missing", ct).ConfigureAwait(true)).ShouldBeNull();
    }

    /// <summary>The agent cap and the active cap are decided with the insert; another owner is unaffected.</summary>
    [Fact]
    public async Task Create_RefusesBeyondTheCaps()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = new InMemoryCloudAgentStore();
        _ = await store.CreateAsync(Record("a1", "alice"), Limits, ct).ConfigureAwait(true);
        _ = await store.CreateAsync(Record("a2", "alice"), Limits, ct).ConfigureAwait(true);

        var activeLimit = await store.CreateAsync(Record("a3", "alice"), Limits, ct).ConfigureAwait(true);
        activeLimit.Outcome.ShouldBe(CloudAgentCreateOutcome.ActiveLimitReached);
        _ = await store.UpdateAsync("a2", static record => record with { Status = CloudAgentStatus.Idle }, ct).ConfigureAwait(true);
        _ = await store.CreateAsync(Record("a3", "alice"), Limits, ct).ConfigureAwait(true);
        var agentLimit = await store.CreateAsync(Record("a4", "alice"), Limits, ct).ConfigureAwait(true);

        agentLimit.Outcome.ShouldBe(CloudAgentCreateOutcome.AgentLimitReached);
        (await store.CreateAsync(Record("b1", "bob"), Limits, ct).ConfigureAwait(true)).Outcome.ShouldBe(CloudAgentCreateOutcome.Created);
    }

    /// <summary>The same owner, key and fingerprint replays the stored record; another fingerprint is a reuse; another owner's key is independent.</summary>
    [Fact]
    public async Task Create_ReplaysOrRefusesAnIdempotencyKey()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = new InMemoryCloudAgentStore();
        _ = await store.CreateAsync(Record("a1", "alice") with { IdempotencyKey = "k", RequestFingerprint = "f1" }, Limits, ct).ConfigureAwait(true);

        var replay = await store.CreateAsync(Record("a2", "alice") with { IdempotencyKey = "k", RequestFingerprint = "f1" }, Limits, ct).ConfigureAwait(true);
        var reuse = await store.CreateAsync(Record("a3", "alice") with { IdempotencyKey = "k", RequestFingerprint = "f2" }, Limits, ct).ConfigureAwait(true);
        var other = await store.CreateAsync(Record("b1", "bob") with { IdempotencyKey = "k", RequestFingerprint = "f1" }, Limits, ct).ConfigureAwait(true);

        replay.Outcome.ShouldBe(CloudAgentCreateOutcome.Replayed);
        replay.Record.ShouldNotBeNull().Id.ShouldBe("a1");
        reuse.Outcome.ShouldBe(CloudAgentCreateOutcome.IdempotencyKeyReused);
        other.Outcome.ShouldBe(CloudAgentCreateOutcome.Created);
        (await store.FindByIdempotencyKeyAsync("alice", "k", ct).ConfigureAwait(true)).ShouldNotBeNull().Id.ShouldBe("a1");
        (await store.FindByIdempotencyKeyAsync("alice", "other", ct).ConfigureAwait(true)).ShouldBeNull();
        (await store.ListByOwnerAsync("alice", ct).ConfigureAwait(true)).Count.ShouldBe(1);
    }

    /// <summary>Concurrent updates never lose one another: fifty increments make exactly fifty.</summary>
    [Fact]
    public async Task Update_IsAtomicUnderConcurrency()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = new InMemoryCloudAgentStore();
        _ = await store.CreateAsync(Record("a1", "alice"), Limits, ct).ConfigureAwait(true);

        await Task.WhenAll(Enumerable.Range(0, 50).Select(_ => store.UpdateAsync("a1", static record => record with { ComputeSeconds = record.ComputeSeconds + 1 }, ct))).ConfigureAwait(true);

        (await store.GetAsync("a1", ct).ConfigureAwait(true)).ShouldNotBeNull().ComputeSeconds.ShouldBe(50);
        (await store.UpdateAsync("missing", static record => record, ct).ConfigureAwait(true)).ShouldBeNull();
    }

    /// <summary>An update that tries to change the identity, owner or session is refused, and the record stays as it was.</summary>
    [Fact]
    public async Task Update_CannotChangeIdentity()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = new InMemoryCloudAgentStore();
        _ = await store.CreateAsync(Record("a1", "alice"), Limits, ct).ConfigureAwait(true);

        await Should.ThrowAsync<InvalidOperationException>(() => store.UpdateAsync("a1", static record => record with { OwnerId = "mallory" }, ct)).ConfigureAwait(true);
        await Should.ThrowAsync<InvalidOperationException>(() => store.UpdateAsync("a1", static record => record with { SessionId = "cloud-other" }, ct)).ConfigureAwait(true);

        (await store.GetAsync("a1", ct).ConfigureAwait(true)).ShouldNotBeNull().OwnerId.ShouldBe("alice");
    }

    /// <summary>Activation moves an idle, stopped, failed or orphaned agent to starting under the cap, and tells the other cases apart.</summary>
    [Fact]
    public async Task TryActivate_FollowsTheStatusAndTheCap()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = new InMemoryCloudAgentStore();
        foreach (var (id, status) in new[]
        {
            ("idle", CloudAgentStatus.Idle), ("stopped", CloudAgentStatus.Stopped), ("failed", CloudAgentStatus.Failed), ("orphaned", CloudAgentStatus.Orphaned),
            ("running", CloudAgentStatus.Running), ("deleting", CloudAgentStatus.Deleting), ("limit", CloudAgentStatus.LimitReached), ("stopping", CloudAgentStatus.Stopping),
        })
        {
            _ = await store.CreateAsync(Record(id, "alice") with { Status = status }, new CloudAgentLimits(50, 50), ct).ConfigureAwait(true);
        }

        (await store.TryActivateAsync("running", 2, Now, ct).ConfigureAwait(true)).Outcome.ShouldBe(CloudAgentActivationOutcome.AlreadyActive);
        (await store.TryActivateAsync("deleting", 2, Now, ct).ConfigureAwait(true)).Outcome.ShouldBe(CloudAgentActivationOutcome.NotWakeable);
        (await store.TryActivateAsync("limit", 2, Now, ct).ConfigureAwait(true)).Outcome.ShouldBe(CloudAgentActivationOutcome.NotWakeable);
        (await store.TryActivateAsync("stopping", 2, Now, ct).ConfigureAwait(true)).Outcome.ShouldBe(CloudAgentActivationOutcome.NotWakeable);
        (await store.TryActivateAsync("nope", 2, Now, ct).ConfigureAwait(true)).Outcome.ShouldBe(CloudAgentActivationOutcome.NotFound);

        var woken = await store.TryActivateAsync("idle", 2, Now, ct).ConfigureAwait(true);
        woken.Outcome.ShouldBe(CloudAgentActivationOutcome.Activated);
        var record = woken.Record.ShouldNotBeNull();
        record.Status.ShouldBe(CloudAgentStatus.Starting);
        record.IdleAt.ShouldBeNull();
        record.Error.ShouldBeNull();
        record.LastActivityAt.ShouldBe(Now);
        (await store.TryActivateAsync("stopped", 2, Now, ct).ConfigureAwait(true)).Outcome.ShouldBe(CloudAgentActivationOutcome.LimitReached);
        (await store.GetAsync("stopped", ct).ConfigureAwait(true)).ShouldNotBeNull().Status.ShouldBe(CloudAgentStatus.Stopped);
        (await store.TryActivateAsync("failed", 3, Now, ct).ConfigureAwait(true)).Outcome.ShouldBe(CloudAgentActivationOutcome.Activated);
    }

    /// <summary>Two concurrent activations under a cap of one wake exactly one agent.</summary>
    [Fact]
    public async Task TryActivate_IsAtomicUnderTheCap()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = new InMemoryCloudAgentStore();
        _ = await store.CreateAsync(Record("a1", "alice") with { Status = CloudAgentStatus.Idle }, new CloudAgentLimits(10, 10), ct).ConfigureAwait(true);
        _ = await store.CreateAsync(Record("a2", "alice") with { Status = CloudAgentStatus.Idle }, new CloudAgentLimits(10, 10), ct).ConfigureAwait(true);

        var outcomes = await Task.WhenAll(store.TryActivateAsync("a1", 1, Now, ct), store.TryActivateAsync("a2", 1, Now, ct)).ConfigureAwait(true);

        outcomes.Count(static outcome => outcome.Outcome == CloudAgentActivationOutcome.Activated).ShouldBe(1);
        outcomes.Count(static outcome => outcome.Outcome == CloudAgentActivationOutcome.LimitReached).ShouldBe(1);
    }

    /// <summary>Listing by status is ordered by the last update, usage counts the owner's agents, and delete removes a record once.</summary>
    [Fact]
    public async Task ListByStatus_UsageAndDelete()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = new InMemoryCloudAgentStore();
        var big = new CloudAgentLimits(10, 10);
        _ = await store.CreateAsync(Record("a1", "alice") with { UpdatedAt = Now.AddMinutes(2), ComputeSeconds = 10 }, big, ct).ConfigureAwait(true);
        _ = await store.CreateAsync(Record("a2", "alice") with { UpdatedAt = Now.AddMinutes(1), ComputeSeconds = 5.5, Status = CloudAgentStatus.Idle }, big, ct).ConfigureAwait(true);
        _ = await store.CreateAsync(Record("a3", "alice") with { UpdatedAt = Now, Status = CloudAgentStatus.Running }, big, ct).ConfigureAwait(true);

        var active = await store.ListByStatusAsync([CloudAgentStatus.Provisioning, CloudAgentStatus.Running], ct).ConfigureAwait(true);
        var usage = await store.GetUsageAsync("alice", ct).ConfigureAwait(true);

        active.Select(static record => record.Id).ToArray().ShouldBe(["a3", "a1"]);
        usage.ShouldBe(new CloudAgentUsage(3, 2, 15.5));
        (await store.DeleteAsync("a1", ct).ConfigureAwait(true)).ShouldBeTrue();
        (await store.DeleteAsync("a1", ct).ConfigureAwait(true)).ShouldBeFalse();
        (await store.ListAllAsync(ct).ConfigureAwait(true)).Count.ShouldBe(2);
    }

    /// <summary>The shared rules say which statuses count against the active cap and which may be woken.</summary>
    [Fact]
    public void StatusRules_AreTheDocumentedOnes()
    {
        CloudAgentStatusRules.ActiveStatuses.ToArray().ShouldBe([CloudAgentStatus.Provisioning, CloudAgentStatus.Starting, CloudAgentStatus.Running]);
        foreach (var status in Enum.GetValues<CloudAgentStatus>())
        {
            CloudAgentStatusRules.IsActive(status).ShouldBe(status is CloudAgentStatus.Provisioning or CloudAgentStatus.Starting or CloudAgentStatus.Running);
            CloudAgentStatusRules.CanActivate(status).ShouldBe(status is CloudAgentStatus.Idle or CloudAgentStatus.Stopped or CloudAgentStatus.Failed or CloudAgentStatus.Orphaned);
        }
    }

    private static CloudAgentRecord Record(string id, string owner) =>
        new()
        {
            Id = id,
            OwnerId = owner,
            SessionId = "cloud-" + id,
            Name = id,
            Status = CloudAgentStatus.Provisioning,
            CreatedAt = Now,
            UpdatedAt = Now,
            Repositories = [new CloudRepository { Address = "https://github.com/acme/widgets" }],
            Branch = "ompos/cloud/" + id,
            IdleMinutes = 15,
            LastActivityAt = Now,
        };
}
