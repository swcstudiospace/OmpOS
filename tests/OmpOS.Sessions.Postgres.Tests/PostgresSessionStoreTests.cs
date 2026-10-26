using OmpOS.Contracts.Sessions;
using OmpOS.Contracts.Streaming;
using OmpOS.Harness.Abstractions;
using Shouldly;
using Xunit;
using static OmpOS.Sessions.Postgres.Tests.SessionSamples;

namespace OmpOS.Sessions.Postgres.Tests;

/// <summary>
/// Behaviour that only exists across processes: several <see cref="PostgresSessionRepository"/> instances, each with its
/// own connection pool, share one schema. Every case uses a fresh schema on the shared server.
/// </summary>
[Collection(PostgresServerGroup.Name)]
public sealed class PostgresSessionStoreTests(PostgresServerFixture server)
{
    private static readonly string[] ExpectedGrantableTools = ["Bash"];

    /// <summary>A second instance resumes from a Seq the first instance wrote, with no gaps and no duplicates.</summary>
    [Fact]
    public async Task ResumeBySeqAcrossTwoInstancesHasNoGapsOrDuplicates()
    {
        var ct = TestContext.Current.CancellationToken;
        var schema = await PostgresTestSchema.CreateAsync(server.AdminConnectionString, ct).ConfigureAwait(true);
        try
        {
            var writer = schema.CreateRepository();
            var reader = schema.CreateRepository();
            await PostgresSessionSchema.EnsureCreatedAsync(schema.CreateDataSource(), ct).ConfigureAwait(true);

            await writer.SaveAsync(Sample("s-1"), ct).ConfigureAwait(true);
            await AppendDeltasAsync(writer, "s-1", 0, 3, ct).ConfigureAwait(true);

            var resumed = await ListAsync(reader, "s-1", 2, ct).ConfigureAwait(true);
            resumed.Select(static evt => evt.Seq).ToArray().ShouldBe([2L, 3L]);

            await AppendDeltasAsync(reader, "s-1", 3, 1, ct).ConfigureAwait(true);

            var all = await ListAsync(writer, "s-1", 0, ct).ConfigureAwait(true);
            all.Select(static evt => evt.Seq).ToArray().ShouldBe([1L, 2L, 3L, 4L]);
            all.Select(static evt => evt.Payload).OfType<TextDeltaPayload>().Select(static payload => payload.Delta).ToArray().ShouldBe(["0", "1", "2", "3"]);
            (await ListAsync(reader, "s-1", 5, ct).ConfigureAwait(true)).ShouldBeEmpty();
        }
        finally
        {
            await schema.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>Concurrent appends from several instances produce exactly Seq 1..100, each once.</summary>
    [Fact]
    public async Task ConcurrentAppendsFromSeveralInstancesGetDenseUniqueSeq()
    {
        var ct = TestContext.Current.CancellationToken;
        var schema = await PostgresTestSchema.CreateAsync(server.AdminConnectionString, ct).ConfigureAwait(true);
        try
        {
            var repositories = Enumerable.Range(0, 4).Select(_ => schema.CreateRepository()).ToArray();
            await PostgresSessionSchema.EnsureCreatedAsync(schema.CreateDataSource(), ct).ConfigureAwait(true);
            await repositories[0].SaveAsync(Sample("s-1"), ct).ConfigureAwait(true);

            await Task.WhenAll(repositories.Select((repository, index) => AppendDeltasAsync(repository, "s-1", index * 25, 25, ct))).ConfigureAwait(true);

            var all = await ListAsync(repositories[1], "s-1", 0, ct).ConfigureAwait(true);
            all.Select(static evt => evt.Seq).ToArray().ShouldBe(Enumerable.Range(1, 100).Select(static i => (long)i).ToArray());
        }
        finally
        {
            await schema.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>Many concurrent first starts on an empty schema all succeed, and a repeated start leaves stored data intact.</summary>
    [Fact]
    public async Task ConcurrentAndRepeatedEnsureCreatedIsIdempotent()
    {
        var ct = TestContext.Current.CancellationToken;
        var schema = await PostgresTestSchema.CreateAsync(server.AdminConnectionString, ct).ConfigureAwait(true);
        try
        {
            var dataSources = Enumerable.Range(0, 6).Select(_ => schema.CreateDataSource()).ToArray();

            await Task.WhenAll(dataSources.Select(dataSource => PostgresSessionSchema.EnsureCreatedAsync(dataSource, ct))).ConfigureAwait(true);
            await PostgresSessionSchema.EnsureCreatedAsync(dataSources[0], ct).ConfigureAwait(true);

            var repository = new PostgresSessionRepository(dataSources[1], schema.Connection);
            await repository.SaveAsync(Sample("s-1"), ct).ConfigureAwait(true);
            await AppendDeltasAsync(repository, "s-1", 0, 2, ct).ConfigureAwait(true);

            await PostgresSessionSchema.EnsureCreatedAsync(dataSources[2], ct).ConfigureAwait(true);

            var loaded = await repository.GetAsync("s-1", ct).ConfigureAwait(true);
            loaded.ShouldNotBeNull();
            loaded.Events.Select(static evt => evt.Seq).ToArray().ShouldBe([1L, 2L]);
        }
        finally
        {
            await schema.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>Creating the schema twice leaves <c>session_events</c> as one hypertable partitioned on <c>occurred_at</c> in one-day chunks.</summary>
    [Fact]
    public async Task EnsureCreatedTwiceLeavesEventsAsOneHypertableOnOccurredAt()
    {
        var ct = TestContext.Current.CancellationToken;
        var schema = await PostgresTestSchema.CreateAsync(server.AdminConnectionString, ct).ConfigureAwait(true);
        try
        {
            var dataSource = schema.CreateDataSource();
            await PostgresSessionSchema.EnsureCreatedAsync(dataSource, ct).ConfigureAwait(true);
            await PostgresSessionSchema.EnsureCreatedAsync(dataSource, ct).ConfigureAwait(true);

            using var count = dataSource.CreateCommand(
                "SELECT count(*) FROM timescaledb_information.hypertables WHERE hypertable_schema = @schema AND hypertable_name = 'session_events'");
            count.Parameters.AddWithValue("schema", schema.SchemaName);
            (await count.ExecuteScalarAsync(ct).ConfigureAwait(true)).ShouldBe(1L);

            using var dimensions = dataSource.CreateCommand(
                "SELECT column_name::text, time_interval = INTERVAL '1 day' FROM timescaledb_information.dimensions " +
                "WHERE hypertable_schema = @schema AND hypertable_name = 'session_events'");
            dimensions.Parameters.AddWithValue("schema", schema.SchemaName);
            using var reader = await dimensions.ExecuteReaderAsync(ct).ConfigureAwait(true);
            (await reader.ReadAsync(ct).ConfigureAwait(true)).ShouldBeTrue();
            reader.GetString(0).ShouldBe("occurred_at");
            reader.GetBoolean(1).ShouldBeTrue();
            (await reader.ReadAsync(ct).ConfigureAwait(true)).ShouldBeFalse();
        }
        finally
        {
            await schema.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>A session lock taken by one instance blocks another instance until it is disposed.</summary>
    [Fact]
    public async Task SessionLockIsExclusiveAcrossInstancesAndReleasedOnDispose()
    {
        var ct = TestContext.Current.CancellationToken;
        var schema = await PostgresTestSchema.CreateAsync(server.AdminConnectionString, ct).ConfigureAwait(true);
        try
        {
            await PostgresSessionSchema.EnsureCreatedAsync(schema.CreateDataSource(), ct).ConfigureAwait(true);
            var holder = schema.CreateRepository();
            var contender = schema.CreateRepository();

            var held = await holder.LockSessionAsync("s-lock", ct).ConfigureAwait(true);
            var contending = contender.LockSessionAsync("s-lock", ct);
            await Task.Delay(TimeSpan.FromMilliseconds(500), ct).ConfigureAwait(true);
            contending.IsCompleted.ShouldBeFalse();

            await held.DisposeAsync().ConfigureAwait(true);

            var acquired = await contending.WaitAsync(TimeSpan.FromSeconds(30), ct).ConfigureAwait(true);
            try
            {
                acquired.ShouldNotBeNull();
            }
            finally
            {
                await acquired.DisposeAsync().ConfigureAwait(true);
            }
        }
        finally
        {
            await schema.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>Locks on different sessions do not block each other.</summary>
    [Fact]
    public async Task LocksOnDifferentSessionsDoNotBlockEachOther()
    {
        var ct = TestContext.Current.CancellationToken;
        var schema = await PostgresTestSchema.CreateAsync(server.AdminConnectionString, ct).ConfigureAwait(true);
        try
        {
            await PostgresSessionSchema.EnsureCreatedAsync(schema.CreateDataSource(), ct).ConfigureAwait(true);
            var first = schema.CreateRepository();
            var second = schema.CreateRepository();

            var firstLock = await first.LockSessionAsync("s-1", ct).ConfigureAwait(true);
            try
            {
                var secondLock = await second.LockSessionAsync("s-2", ct).WaitAsync(TimeSpan.FromSeconds(30), ct).ConfigureAwait(true);
                try
                {
                    secondLock.ShouldNotBeNull();
                }
                finally
                {
                    await secondLock.DisposeAsync().ConfigureAwait(true);
                }
            }
            finally
            {
                await firstLock.DisposeAsync().ConfigureAwait(true);
            }
        }
        finally
        {
            await schema.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>A lock can be taken for a session id that has no header yet.</summary>
    [Fact]
    public async Task LockIsAvailableForSessionThatDoesNotExistYet()
    {
        var ct = TestContext.Current.CancellationToken;
        var schema = await PostgresTestSchema.CreateAsync(server.AdminConnectionString, ct).ConfigureAwait(true);
        try
        {
            await PostgresSessionSchema.EnsureCreatedAsync(schema.CreateDataSource(), ct).ConfigureAwait(true);
            var repository = schema.CreateRepository();

            var held = await repository.LockSessionAsync("never-saved", ct).ConfigureAwait(true);
            try
            {
                held.ShouldNotBeNull();
            }
            finally
            {
                await held.DisposeAsync().ConfigureAwait(true);
            }
        }
        finally
        {
            await schema.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>A save that conflicts with the stored log is rejected and leaves the stored header unchanged.</summary>
    [Fact]
    public async Task RejectedSaveRollsBackTheHeaderWrite()
    {
        var ct = TestContext.Current.CancellationToken;
        var schema = await PostgresTestSchema.CreateAsync(server.AdminConnectionString, ct).ConfigureAwait(true);
        try
        {
            await PostgresSessionSchema.EnsureCreatedAsync(schema.CreateDataSource(), ct).ConfigureAwait(true);
            var repository = schema.CreateRepository();
            await repository.SaveAsync(Sample("s-1"), ct).ConfigureAwait(true);
            await AppendDeltasAsync(repository, "s-1", 0, 3, ct).ConfigureAwait(true);

            var clashing = Sample("s-1") with
            {
                Status = SessionStatus.Running,
                Events =
                [
                    Event("s-1", new TextDeltaPayload { Delta = "stale-0" }) with { Seq = 11 },
                    Event("s-1", new TextDeltaPayload { Delta = "stale-1" }) with { Seq = 12 },
                    Event("s-1", new TextDeltaPayload { Delta = "stale-2" }) with { Seq = 13 },
                ],
            };

            var error = await Should.ThrowAsync<InvalidOperationException>(() => repository.SaveAsync(clashing, ct)).ConfigureAwait(true);

            error.Message.ShouldContain("s-1");
            var loaded = await repository.GetAsync("s-1", ct).ConfigureAwait(true);
            loaded.ShouldNotBeNull();
            loaded.Status.ShouldBe(SessionStatus.Idle);
            loaded.Events.Select(static evt => evt.Seq).ToArray().ShouldBe([1L, 2L, 3L]);
        }
        finally
        {
            await schema.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>A save whose events past the stored log repeat a Seq is rejected, because the table cannot enforce Seq uniqueness itself.</summary>
    [Fact]
    public async Task SaveWithRepeatedSeqPastTheStoredLogIsRejectedAndRolledBack()
    {
        var ct = TestContext.Current.CancellationToken;
        var schema = await PostgresTestSchema.CreateAsync(server.AdminConnectionString, ct).ConfigureAwait(true);
        try
        {
            await PostgresSessionSchema.EnsureCreatedAsync(schema.CreateDataSource(), ct).ConfigureAwait(true);
            var repository = schema.CreateRepository();
            await repository.SaveAsync(Sample("s-1"), ct).ConfigureAwait(true);
            await AppendDeltasAsync(repository, "s-1", 0, 2, ct).ConfigureAwait(true);

            var repeated = Sample("s-1") with
            {
                Status = SessionStatus.Running,
                Events =
                [
                    Event("s-1", new TextDeltaPayload { Delta = "stored-0" }) with { Seq = 1 },
                    Event("s-1", new TextDeltaPayload { Delta = "stored-1" }) with { Seq = 2 },
                    Event("s-1", new TextDeltaPayload { Delta = "new-2" }) with { Seq = 3 },
                    Event("s-1", new TextDeltaPayload { Delta = "new-3" }) with { Seq = 3 },
                ],
            };

            var error = await Should.ThrowAsync<InvalidOperationException>(() => repository.SaveAsync(repeated, ct)).ConfigureAwait(true);

            error.Message.ShouldContain("s-1");
            var loaded = await repository.GetAsync("s-1", ct).ConfigureAwait(true);
            loaded.ShouldNotBeNull();
            loaded.Status.ShouldBe(SessionStatus.Idle);
            loaded.Events.Select(static evt => evt.Seq).ToArray().ShouldBe([1L, 2L]);
        }
        finally
        {
            await schema.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>A tool approval can be decided once, and the decision is visible from every instance.</summary>
    [Fact]
    public async Task ToolApprovalIsDecidedOnlyOnceAcrossInstances()
    {
        var ct = TestContext.Current.CancellationToken;
        var schema = await PostgresTestSchema.CreateAsync(server.AdminConnectionString, ct).ConfigureAwait(true);
        try
        {
            await PostgresSessionSchema.EnsureCreatedAsync(schema.CreateDataSource(), ct).ConfigureAwait(true);
            var first = schema.CreateRepository();
            var second = schema.CreateRepository();
            await first.SaveAsync(Sample("s-1"), ct).ConfigureAwait(true);

            (await first.TryAddToolApprovalAsync("s-1", Approval("s-1", "req-1"), ct).ConfigureAwait(true)).ShouldBeTrue();
            (await second.TryAddToolApprovalAsync("s-1", Approval("s-1", "req-1"), ct).ConfigureAwait(true)).ShouldBeFalse();

            var pending = await second.GetToolApprovalAsync("s-1", "req-1", ct).ConfigureAwait(true);
            pending.ShouldNotBeNull();
            pending.Decision.ShouldBeNull();
            pending.DecidedAt.ShouldBeNull();
            pending.GrantableTools.ToArray().ShouldBe(ExpectedGrantableTools);

            (await first.TryDecideToolApprovalAsync("s-1", "req-1", PermissionDecision.Allow, Epoch.AddSeconds(5), ct).ConfigureAwait(true)).ShouldBeTrue();
            (await second.TryDecideToolApprovalAsync("s-1", "req-1", PermissionDecision.Deny, Epoch.AddSeconds(6), ct).ConfigureAwait(true)).ShouldBeFalse();

            var decided = await first.GetToolApprovalAsync("s-1", "req-1", ct).ConfigureAwait(true);
            decided.ShouldNotBeNull();
            decided.Decision.ShouldBe(PermissionDecision.Allow);
            decided.DecidedAt.ShouldBe(Epoch.AddSeconds(5));
        }
        finally
        {
            await schema.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>Deciding an unknown request returns false, and reading one returns null.</summary>
    [Fact]
    public async Task UnknownToolApprovalIsNotDecidableAndReadsAsNull()
    {
        var ct = TestContext.Current.CancellationToken;
        var schema = await PostgresTestSchema.CreateAsync(server.AdminConnectionString, ct).ConfigureAwait(true);
        try
        {
            await PostgresSessionSchema.EnsureCreatedAsync(schema.CreateDataSource(), ct).ConfigureAwait(true);
            var repository = schema.CreateRepository();
            await repository.SaveAsync(Sample("s-1"), ct).ConfigureAwait(true);

            (await repository.TryDecideToolApprovalAsync("s-1", "absent", PermissionDecision.Allow, Epoch, ct).ConfigureAwait(true)).ShouldBeFalse();
            (await repository.GetToolApprovalAsync("s-1", "absent", ct).ConfigureAwait(true)).ShouldBeNull();
        }
        finally
        {
            await schema.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>Adding a tool approval to an unknown session throws, as appending does.</summary>
    [Fact]
    public async Task AddingToolApprovalToUnknownSessionThrowsKeyNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var schema = await PostgresTestSchema.CreateAsync(server.AdminConnectionString, ct).ConfigureAwait(true);
        try
        {
            await PostgresSessionSchema.EnsureCreatedAsync(schema.CreateDataSource(), ct).ConfigureAwait(true);
            var repository = schema.CreateRepository();

            await Should.ThrowAsync<KeyNotFoundException>(() => repository.TryAddToolApprovalAsync("absent", Approval("absent", "req-1"), ct)).ConfigureAwait(true);
        }
        finally
        {
            await schema.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>Listing events of an unknown session throws on enumeration.</summary>
    [Fact]
    public async Task ListingUnknownSessionThrowsKeyNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var schema = await PostgresTestSchema.CreateAsync(server.AdminConnectionString, ct).ConfigureAwait(true);
        try
        {
            await PostgresSessionSchema.EnsureCreatedAsync(schema.CreateDataSource(), ct).ConfigureAwait(true);
            var repository = schema.CreateRepository();

            await Should.ThrowAsync<KeyNotFoundException>(() => ListAsync(repository, "absent", 0, ct)).ConfigureAwait(true);
        }
        finally
        {
            await schema.DisposeAsync().ConfigureAwait(true);
        }
    }
}
