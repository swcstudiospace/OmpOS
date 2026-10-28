using Npgsql;
using Autonogrammer.Contracts.Cloud;
using Shouldly;
using Xunit;

namespace Autonogrammer.Sessions.Postgres.Tests.Cloud;

/// <summary>
/// The PostgreSQL cloud agent store against a real server: the idempotent migration, every operation of <see cref="ICloudAgentStore"/>, and the
/// guarantees that only exist across processes (two stores with their own connection pools share one table, and the caps, idempotency keys and
/// updates hold between them). Every case uses a fresh schema on the shared server.
/// </summary>
[Collection(PostgresServerGroup.Name)]
public sealed class PostgresCloudAgentStoreTests(PostgresServerFixture server)
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 9, 0, 0, TimeSpan.Zero);

    /// <summary>The migration can run again, and concurrently, without error or a second table.</summary>
    [Fact]
    public async Task Schema_IsIdempotentAndSafeConcurrently()
    {
        var ct = TestContext.Current.CancellationToken;
        var schema = await PostgresTestSchema.CreateAsync(server.AdminConnectionString, ct).ConfigureAwait(true);
        try
        {
            await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => PostgresCloudAgentSchema.EnsureCreatedAsync(schema.CreateDataSource(), ct))).ConfigureAwait(true);
            await PostgresCloudAgentSchema.EnsureCreatedAsync(schema.CreateDataSource(), ct).ConfigureAwait(true);

            var store = new PostgresCloudAgentStore(schema.CreateDataSource());
            (await store.ListAllAsync(ct).ConfigureAwait(true)).ShouldBeEmpty();
        }
        finally
        {
            await schema.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>A record round trips with every member, and is found by id, by session, by owner and by Idempotency-Key.</summary>
    [Fact]
    public async Task Record_RoundTripsAndIsFoundEveryWay()
    {
        var ct = TestContext.Current.CancellationToken;
        var schema = await PostgresTestSchema.CreateAsync(server.AdminConnectionString, ct).ConfigureAwait(true);
        try
        {
            var store = await CreateStoreAsync(schema, ct).ConfigureAwait(true);
            var record = Record("a1", "alice") with
            {
                Name = "acme/widgets",
                EnvironmentId = "node",
                SecretRefs = ["API_KEY", "DB_PASSWORD"],
                Model = "m",
                Mode = "plan",
                WorkerRef = "ctr-1",
                VolumeRef = "vol-1",
                NativeSessionId = "acp-1",
                IdleAt = Now.AddMinutes(1),
                ActiveSince = Now.AddMinutes(2),
                ComputeSeconds = 12.5,
                Error = new CloudAgentError { Code = "wake_timeout", Message = "slow" },
                IdempotencyKey = "key-1",
                RequestFingerprint = "fp-1",
                PullRequests = [new CloudPullRequest { Repository = "https://github.com/acme/widgets", Number = 7, Url = new Uri("https://github.com/acme/widgets/pull/7", UriKind.Absolute), Branch = "autonogrammer/cloud/a1", BaseBranch = "main", Draft = false, CreatedAt = Now }],
            };

            (await store.CreateAsync(record, new CloudAgentLimits(5, 5), ct).ConfigureAwait(true)).Outcome.ShouldBe(CloudAgentCreateOutcome.Created);

            var read = (await store.GetAsync("a1", ct).ConfigureAwait(true)).ShouldNotBeNull();
            read.SecretRefs.ToArray().ShouldBe(["API_KEY", "DB_PASSWORD"]);
            read.Error.ShouldNotBeNull().Code.ShouldBe("wake_timeout");
            read.PullRequests.ShouldHaveSingleItem().Number.ShouldBe(7);
            read.WorkerRef.ShouldBe("ctr-1");
            read.ActiveSince.ShouldBe(Now.AddMinutes(2));
            read.ComputeSeconds.ShouldBe(12.5);
            read.Repositories.ShouldHaveSingleItem().Address.ShouldBe("https://github.com/acme/widgets");
            (await store.GetBySessionAsync("cloud-a1", ct).ConfigureAwait(true)).ShouldNotBeNull().Id.ShouldBe("a1");
            (await store.FindByIdempotencyKeyAsync("alice", "key-1", ct).ConfigureAwait(true)).ShouldNotBeNull().Id.ShouldBe("a1");
            (await store.FindByIdempotencyKeyAsync("bob", "key-1", ct).ConfigureAwait(true)).ShouldBeNull();
            (await store.ListByOwnerAsync("alice", ct).ConfigureAwait(true)).ShouldHaveSingleItem();
            (await store.ListByOwnerAsync("bob", ct).ConfigureAwait(true)).ShouldBeEmpty();
            (await store.GetAsync("missing", ct).ConfigureAwait(true)).ShouldBeNull();
        }
        finally
        {
            await schema.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>The same owner, key and fingerprint replays; another fingerprint is a reuse; the key is per owner.</summary>
    [Fact]
    public async Task IdempotencyKey_ReplaysAndRefuses()
    {
        var ct = TestContext.Current.CancellationToken;
        var schema = await PostgresTestSchema.CreateAsync(server.AdminConnectionString, ct).ConfigureAwait(true);
        try
        {
            var store = await CreateStoreAsync(schema, ct).ConfigureAwait(true);
            var limits = new CloudAgentLimits(5, 5);
            _ = await store.CreateAsync(Record("a1", "alice") with { IdempotencyKey = "k", RequestFingerprint = "f1" }, limits, ct).ConfigureAwait(true);

            var replay = await store.CreateAsync(Record("a2", "alice") with { IdempotencyKey = "k", RequestFingerprint = "f1" }, limits, ct).ConfigureAwait(true);
            var reuse = await store.CreateAsync(Record("a3", "alice") with { IdempotencyKey = "k", RequestFingerprint = "f2" }, limits, ct).ConfigureAwait(true);
            var other = await store.CreateAsync(Record("b1", "bob") with { IdempotencyKey = "k", RequestFingerprint = "f1" }, limits, ct).ConfigureAwait(true);

            replay.Outcome.ShouldBe(CloudAgentCreateOutcome.Replayed);
            replay.Record.ShouldNotBeNull().Id.ShouldBe("a1");
            reuse.Outcome.ShouldBe(CloudAgentCreateOutcome.IdempotencyKeyReused);
            other.Outcome.ShouldBe(CloudAgentCreateOutcome.Created);
            (await store.ListByOwnerAsync("alice", ct).ConfigureAwait(true)).Count.ShouldBe(1);
        }
        finally
        {
            await schema.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>Two stores creating at once cannot exceed the agent cap: exactly three of six concurrent creates win.</summary>
    [Fact]
    public async Task AgentCap_HoldsAcrossStoresAndConcurrentCreates()
    {
        var ct = TestContext.Current.CancellationToken;
        var schema = await PostgresTestSchema.CreateAsync(server.AdminConnectionString, ct).ConfigureAwait(true);
        try
        {
            var first = await CreateStoreAsync(schema, ct).ConfigureAwait(true);
            var second = new PostgresCloudAgentStore(schema.CreateDataSource());
            var limits = new CloudAgentLimits(MaxAgents: 3, MaxActive: 10);

            var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(index => (index % 2 == 0 ? first : second).CreateAsync(Record("a" + index.ToString(System.Globalization.CultureInfo.InvariantCulture), "alice"), limits, ct))).ConfigureAwait(true);

            results.Count(static result => result.Outcome == CloudAgentCreateOutcome.Created).ShouldBe(3);
            results.Count(static result => result.Outcome == CloudAgentCreateOutcome.AgentLimitReached).ShouldBe(3);
            (await first.GetUsageAsync("alice", ct).ConfigureAwait(true)).Agents.ShouldBe(3);
        }
        finally
        {
            await schema.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>The active cap holds across stores: with a cap of two, only two of five concurrent creates are admitted while the owner has everything provisioning.</summary>
    [Fact]
    public async Task ActiveCap_HoldsAcrossStores()
    {
        var ct = TestContext.Current.CancellationToken;
        var schema = await PostgresTestSchema.CreateAsync(server.AdminConnectionString, ct).ConfigureAwait(true);
        try
        {
            var first = await CreateStoreAsync(schema, ct).ConfigureAwait(true);
            var second = new PostgresCloudAgentStore(schema.CreateDataSource());
            var limits = new CloudAgentLimits(MaxAgents: 10, MaxActive: 2);

            var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(index => (index % 2 == 0 ? first : second).CreateAsync(Record("a" + index.ToString(System.Globalization.CultureInfo.InvariantCulture), "alice"), limits, ct))).ConfigureAwait(true);

            results.Count(static result => result.Outcome == CloudAgentCreateOutcome.Created).ShouldBe(2);
            results.Count(static result => result.Outcome == CloudAgentCreateOutcome.ActiveLimitReached).ShouldBe(3);
        }
        finally
        {
            await schema.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>Concurrent updates from two stores never lose one another.</summary>
    [Fact]
    public async Task Update_IsAtomicAcrossStores()
    {
        var ct = TestContext.Current.CancellationToken;
        var schema = await PostgresTestSchema.CreateAsync(server.AdminConnectionString, ct).ConfigureAwait(true);
        try
        {
            var first = await CreateStoreAsync(schema, ct).ConfigureAwait(true);
            var second = new PostgresCloudAgentStore(schema.CreateDataSource());
            _ = await first.CreateAsync(Record("a1", "alice"), new CloudAgentLimits(5, 5), ct).ConfigureAwait(true);

            await Task.WhenAll(Enumerable.Range(0, 40).Select(index => (index % 2 == 0 ? first : second).UpdateAsync("a1", static record => record with { ComputeSeconds = record.ComputeSeconds + 1 }, ct))).ConfigureAwait(true);

            (await first.GetAsync("a1", ct).ConfigureAwait(true)).ShouldNotBeNull().ComputeSeconds.ShouldBe(40);
            (await first.UpdateAsync("missing", static record => record, ct).ConfigureAwait(true)).ShouldBeNull();
        }
        finally
        {
            await schema.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>An update that changes identity is refused and rolled back.</summary>
    [Fact]
    public async Task Update_CannotChangeIdentity()
    {
        var ct = TestContext.Current.CancellationToken;
        var schema = await PostgresTestSchema.CreateAsync(server.AdminConnectionString, ct).ConfigureAwait(true);
        try
        {
            var store = await CreateStoreAsync(schema, ct).ConfigureAwait(true);
            _ = await store.CreateAsync(Record("a1", "alice"), new CloudAgentLimits(5, 5), ct).ConfigureAwait(true);

            await Should.ThrowAsync<InvalidOperationException>(() => store.UpdateAsync("a1", static record => record with { OwnerId = "mallory" }, ct)).ConfigureAwait(true);

            (await store.GetAsync("a1", ct).ConfigureAwait(true)).ShouldNotBeNull().OwnerId.ShouldBe("alice");
        }
        finally
        {
            await schema.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>Activation follows the status rule and the cap, and two stores waking two agents under a cap of one wake exactly one.</summary>
    [Fact]
    public async Task TryActivate_FollowsTheRuleAndTheCapAcrossStores()
    {
        var ct = TestContext.Current.CancellationToken;
        var schema = await PostgresTestSchema.CreateAsync(server.AdminConnectionString, ct).ConfigureAwait(true);
        try
        {
            var first = await CreateStoreAsync(schema, ct).ConfigureAwait(true);
            var second = new PostgresCloudAgentStore(schema.CreateDataSource());
            var big = new CloudAgentLimits(20, 20);
            foreach (var (id, status) in new[] { ("idle1", CloudAgentStatus.Idle), ("idle2", CloudAgentStatus.Idle), ("running", CloudAgentStatus.Running), ("deleting", CloudAgentStatus.Deleting), ("limit", CloudAgentStatus.LimitReached) })
            {
                _ = await first.CreateAsync(Record(id, "alice") with { Status = status }, big, ct).ConfigureAwait(true);
            }

            (await first.TryActivateAsync("running", 2, Now, ct).ConfigureAwait(true)).Outcome.ShouldBe(CloudAgentActivationOutcome.AlreadyActive);
            (await first.TryActivateAsync("deleting", 2, Now, ct).ConfigureAwait(true)).Outcome.ShouldBe(CloudAgentActivationOutcome.NotWakeable);
            (await first.TryActivateAsync("limit", 2, Now, ct).ConfigureAwait(true)).Outcome.ShouldBe(CloudAgentActivationOutcome.NotWakeable);
            (await first.TryActivateAsync("nope", 2, Now, ct).ConfigureAwait(true)).Outcome.ShouldBe(CloudAgentActivationOutcome.NotFound);

            var outcomes = await Task.WhenAll(first.TryActivateAsync("idle1", 2, Now, ct), second.TryActivateAsync("idle2", 2, Now, ct)).ConfigureAwait(true);

            outcomes.Count(static outcome => outcome.Outcome == CloudAgentActivationOutcome.Activated).ShouldBe(1);
            outcomes.Count(static outcome => outcome.Outcome == CloudAgentActivationOutcome.LimitReached).ShouldBe(1);
            var woken = outcomes.Single(static outcome => outcome.Outcome == CloudAgentActivationOutcome.Activated).Record.ShouldNotBeNull();
            woken.Status.ShouldBe(CloudAgentStatus.Starting);
            woken.LastActivityAt.ShouldBe(Now);
            (await second.GetAsync(woken.Id, ct).ConfigureAwait(true)).ShouldNotBeNull().Status.ShouldBe(CloudAgentStatus.Starting);
        }
        finally
        {
            await schema.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>Listing by status and over everything is ordered, usage counts the owner's agents and compute seconds, and delete removes a record once.</summary>
    [Fact]
    public async Task ListUsageAndDelete()
    {
        var ct = TestContext.Current.CancellationToken;
        var schema = await PostgresTestSchema.CreateAsync(server.AdminConnectionString, ct).ConfigureAwait(true);
        try
        {
            var store = await CreateStoreAsync(schema, ct).ConfigureAwait(true);
            var big = new CloudAgentLimits(10, 10);
            _ = await store.CreateAsync(Record("a1", "alice") with { UpdatedAt = Now.AddMinutes(2), CreatedAt = Now, ComputeSeconds = 10 }, big, ct).ConfigureAwait(true);
            _ = await store.CreateAsync(Record("a2", "alice") with { UpdatedAt = Now.AddMinutes(1), CreatedAt = Now.AddMinutes(1), ComputeSeconds = 5.5, Status = CloudAgentStatus.Idle }, big, ct).ConfigureAwait(true);
            _ = await store.CreateAsync(Record("a3", "alice") with { UpdatedAt = Now, CreatedAt = Now.AddMinutes(2), Status = CloudAgentStatus.Running }, big, ct).ConfigureAwait(true);

            var active = await store.ListByStatusAsync([CloudAgentStatus.Provisioning, CloudAgentStatus.Running], ct).ConfigureAwait(true);
            var usage = await store.GetUsageAsync("alice", ct).ConfigureAwait(true);
            var owned = await store.ListByOwnerAsync("alice", ct).ConfigureAwait(true);
            var all = await store.ListAllAsync(ct).ConfigureAwait(true);

            active.Select(static record => record.Id).ToArray().ShouldBe(["a3", "a1"]);
            owned.Select(static record => record.Id).ToArray().ShouldBe(["a3", "a2", "a1"]);
            all.Select(static record => record.Id).ToArray().ShouldBe(["a1", "a2", "a3"]);
            usage.ShouldBe(new CloudAgentUsage(3, 2, 15.5));
            (await store.GetUsageAsync("nobody", ct).ConfigureAwait(true)).ShouldBe(new CloudAgentUsage(0, 0, 0));
            (await store.DeleteAsync("a1", ct).ConfigureAwait(true)).ShouldBeTrue();
            (await store.DeleteAsync("a1", ct).ConfigureAwait(true)).ShouldBeFalse();
        }
        finally
        {
            await schema.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>The database refuses two agents for one session even when the application would not.</summary>
    [Fact]
    public async Task UniqueSessionIndex_RefusesASecondAgentForASession()
    {
        var ct = TestContext.Current.CancellationToken;
        var schema = await PostgresTestSchema.CreateAsync(server.AdminConnectionString, ct).ConfigureAwait(true);
        try
        {
            var store = await CreateStoreAsync(schema, ct).ConfigureAwait(true);
            var big = new CloudAgentLimits(10, 10);
            _ = await store.CreateAsync(Record("a1", "alice"), big, ct).ConfigureAwait(true);

            await Should.ThrowAsync<PostgresException>(() => store.CreateAsync(Record("a2", "alice") with { SessionId = "cloud-a1" }, big, ct)).ConfigureAwait(true);
        }
        finally
        {
            await schema.DisposeAsync().ConfigureAwait(true);
        }
    }

    private static async Task<PostgresCloudAgentStore> CreateStoreAsync(PostgresTestSchema schema, CancellationToken cancellationToken)
    {
        var dataSource = schema.CreateDataSource();
        await PostgresCloudAgentSchema.EnsureCreatedAsync(dataSource, cancellationToken).ConfigureAwait(false);
        return new PostgresCloudAgentStore(dataSource);
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
            Branch = "autonogrammer/cloud/" + id,
            IdleMinutes = 15,
            LastActivityAt = Now,
        };
}
