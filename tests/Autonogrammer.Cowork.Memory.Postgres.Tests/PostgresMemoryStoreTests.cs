#pragma warning disable CA2007, CA2100, MA0004, IDE0005, CS8604
using System.Collections.Concurrent;
using System.Text;
using Autonogrammer.Cowork.Memory;
using Autonogrammer.Cowork.Memory.Postgres;
using Autonogrammer.Cowork.Memory.Tests;
using Autonogrammer.Sessions.Postgres.Migrations;
using Microsoft.Extensions.Logging;
using Npgsql;
using Shouldly;
using Xunit;

namespace Autonogrammer.Cowork.Memory.Postgres.Tests;

/// <summary>Two-account isolation, cascade delete, the GIN plan and the log canary.</summary>
[Trait("Category", "Live")]
public sealed class PostgresMemoryStoreTests : MemoryStoreContractTests, IAsyncLifetime
{
    private const string Canary = "memory-canary-7f3a91c0";
    private const string InstructionCanary = "instruction-canary-91bc02e4";

    private readonly string _schema = "t_" + Guid.NewGuid().ToString("n");
    private readonly string _role = "r_" + Guid.NewGuid().ToString("n");
    private readonly string _password = Guid.NewGuid().ToString("n") + Guid.NewGuid().ToString("n");
    private NpgsqlDataSource? _admin;
    private NpgsqlDataSource? _app;

    /// <summary>Accounts the tests write under. Each gets its lifecycle row before the first write.</summary>
    private static readonly string[] Accounts = ["acct", "left", "right", "other", "owner", "cap-owner", "key-owner", "boundary-owner", "log-owner"];

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        var configured = Environment.GetEnvironmentVariable("AUTONOGRAMMER_TEST_POSTGRES_CONNECTION");
        if (string.IsNullOrWhiteSpace(configured))
        {
            throw new InvalidOperationException("AUTONOGRAMMER_TEST_POSTGRES_CONNECTION is unset. Source scripts/test-services.sh up before these tests.");
        }

        _admin = NpgsqlDataSource.Create(configured);
        await new MigrationRunner(_admin, _schema).MigrateAsync(CancellationToken.None).ConfigureAwait(true);
        await using (var grants = _admin.CreateCommand($"""
            CREATE ROLE "{_role}" LOGIN PASSWORD '{_password}' NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT;
            GRANT USAGE ON SCHEMA "{_schema}" TO "{_role}";
            GRANT SELECT, INSERT, UPDATE, DELETE ON "{_schema}".cowork_projects, "{_schema}".cowork_instructions,
                "{_schema}".cowork_memory_entries, "{_schema}".cowork_memory_settings, "{_schema}".cowork_memory_idempotency,
                "{_schema}".account_lifecycle TO "{_role}";
            """))
        {
            await grants.ExecuteNonQueryAsync(CancellationToken.None).ConfigureAwait(true);
        }
        await ProvisionAccountsAsync(CancellationToken.None).ConfigureAwait(true);
        var builder = new NpgsqlConnectionStringBuilder(configured)
        {
            SearchPath = _schema + ",public",
            ApplicationName = _schema,
            Username = _role,
            Password = _password,
        };
        _app = NpgsqlDataSource.Create(builder.ConnectionString);
        await using var check = _app.CreateCommand("""
            SELECT NOT rolsuper AND NOT rolcreatedb AND NOT rolcreaterole
                AND NOT has_schema_privilege(current_user, current_schema(), 'CREATE')
                AND NOT has_table_privilege(current_user, 'cowork_projects', 'TRUNCATE')
            FROM pg_roles WHERE rolname = current_user
            """);
        ((bool)(await check.ExecuteScalarAsync(CancellationToken.None).ConfigureAwait(true))!).ShouldBeTrue();
    }

    /// <summary>Inserts the lifecycle row for each test account. Writes never insert it, so a missing row fails the write.</summary>
    /// <param name="token">The cancellation token.</param>
    /// <returns>A task that completes when every row exists.</returns>
    private async Task ProvisionAccountsAsync(CancellationToken token)
    {
        foreach (var account in Accounts)
        {
            using var provision = _admin!.CreateCommand($"""
                INSERT INTO "{_schema}".account_lifecycle (account_id) VALUES (@id) ON CONFLICT (account_id) DO NOTHING
                """);
            provision.Parameters.AddWithValue("id", account);
            _ = await provision.ExecuteNonQueryAsync(token).ConfigureAwait(true);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync().ConfigureAwait(true);
        }

        if (_admin is null)
        {
            return;
        }

        await using var connection = await _admin.OpenConnectionAsync(CancellationToken.None).ConfigureAwait(true);
        await using var drop = new NpgsqlCommand("DROP SCHEMA IF EXISTS " + _schema + " CASCADE; DROP ROLE IF EXISTS " + _role, connection);
        _ = await drop.ExecuteNonQueryAsync(CancellationToken.None).ConfigureAwait(true);
        await _admin.DisposeAsync().ConfigureAwait(true);
    }

    /// <inheritdoc />
    protected override IMemoryStore Create(string accountId, IMemoryNoticeSink notices)
    {
        _ = accountId;
        return new PostgresMemoryStore(_app!, notices: notices);
    }

    /// <inheritdoc />
    protected override async Task EnsureProjectAsync(string accountId, string projectId)
    {
        await using var command = _app!.CreateCommand(
            """
            INSERT INTO cowork_projects (id, account_id, name, created_at, updated_at)
            VALUES (@id, @account_id, @name, now(), now())
            ON CONFLICT (id) DO NOTHING
            """);
        command.Parameters.AddWithValue("id", projectId);
        command.Parameters.AddWithValue("account_id", accountId);
        command.Parameters.AddWithValue("name", "p");
        _ = await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Deleting a project cascades its memory and leaves the other account untouched.</summary>
    [Fact]
    public async Task CascadeDeleteRemovesProjectMemoryOnly()
    {
        var store = new PostgresMemoryStore(_app!);
        await EnsureProjectAsync("left", "proj").ConfigureAwait(true);
        await EnsureProjectAsync("right", "other").ConfigureAwait(true);
        var token = TestContext.Current.CancellationToken;
        _ = await store.SaveAsync(new MemoryScope("left", "proj", false, "sess"), new MemoryDraft("tea", Canary, null), token).ConfigureAwait(true);
        _ = await store.SaveAsync(new MemoryScope("right", "other", false, "sess"), new MemoryDraft("tea", "right stays", null), token).ConfigureAwait(true);
        await using var delete = _app!.CreateCommand("DELETE FROM cowork_projects WHERE id = @id AND account_id = @account_id");
        delete.Parameters.AddWithValue("id", "proj");
        delete.Parameters.AddWithValue("account_id", "left");
        _ = await delete.ExecuteNonQueryAsync(token).ConfigureAwait(true);
        (await store.RecallAsync(new MemoryScope("left", "proj", false, "sess"), null, 5, token).ConfigureAwait(true)).Entries.ShouldBeEmpty();
        (await store.RecallAsync(new MemoryScope("right", "other", false, "sess"), null, 5, token).ConfigureAwait(true)).Entries.Single().Content.ShouldBe("right stays");
    }

    /// <summary>Foreign and missing projects are indistinguishable and never receive a row.</summary>
    [Fact]
    public async Task SaveRejectsForeignAndMissingProjects()
    {
        await EnsureProjectAsync("owner", "owned").ConfigureAwait(true);
        var store = new PostgresMemoryStore(_app!);
        var token = TestContext.Current.CancellationToken;
        var foreign = await store.SaveAsync(new MemoryScope("other", "owned", false, "s"), new MemoryDraft(null, "foreign", null), token).ConfigureAwait(true);
        var missing = await store.SaveAsync(new MemoryScope("other", "missing", false, "s"), new MemoryDraft(null, "missing", null), token).ConfigureAwait(true);
        foreign.Succeeded.ShouldBeFalse();
        foreign.Code.ShouldBe(MemoryCodes.MemoryNotFound);
        missing.Code.ShouldBe(foreign.Code);
        missing.Sentence.ShouldBe(foreign.Sentence);
        await using var count = _app!.CreateCommand("SELECT count(*) FROM cowork_memory_entries");
        ((long)(await count.ExecuteScalarAsync(token).ConfigureAwait(true))!).ShouldBe(0);
    }

    /// <summary>Independent store instances serialize their final account and session cap slots.</summary>
    [Theory]
    [InlineData(19, true)]
    [InlineData(999, false)]
    public async Task ConcurrentSavesRespectCaps(int seeded, bool sessionCap)
    {
        var token = TestContext.Current.CancellationToken;
        await using (var seed = _app!.CreateCommand("""
            INSERT INTO cowork_memory_entries (id, account_id, content, source_session, created_at, updated_at)
            SELECT 'seed-' || n::text, 'cap-owner', 'note',
                CASE WHEN @session_cap THEN 'same' ELSE 'old-' || n::text END, now(), now()
            FROM generate_series(1, @count) AS n
            """))
        {
            seed.Parameters.AddWithValue("session_cap", sessionCap);
            seed.Parameters.AddWithValue("count", seeded);
            await seed.ExecuteNonQueryAsync(token).ConfigureAwait(true);
        }

        await using var barrierConnection = await _app.OpenConnectionAsync(token).ConfigureAwait(true);
        await using var barrier = new NpgsqlCommand("SELECT pg_advisory_lock(hashtextextended('cowork-memory:' || @account, 0))", barrierConnection);
        barrier.Parameters.AddWithValue("account", "cap-owner");
        await barrier.ExecuteNonQueryAsync(token).ConfigureAwait(true);
        var first = new PostgresMemoryStore(_app).SaveAsync(new MemoryScope("cap-owner", null, false, "same"), new MemoryDraft(null, "first", null), token);
        var second = new PostgresMemoryStore(_app).SaveAsync(new MemoryScope("cap-owner", null, false, "same"), new MemoryDraft(null, "second", null), token);
        await WaitForBlockedAsync("SELECT pg_advisory_xact_lock", token, 2).ConfigureAwait(true);
        await using var release = new NpgsqlCommand("SELECT pg_advisory_unlock(hashtextextended('cowork-memory:' || @account, 0))", barrierConnection);
        release.Parameters.AddWithValue("account", "cap-owner");
        await release.ExecuteNonQueryAsync(token).ConfigureAwait(true);
        var results = await Task.WhenAll(first, second).ConfigureAwait(true);
        results.Count(static result => result.Succeeded).ShouldBe(1);
        results.Single(static result => !result.Succeeded).Code.ShouldBe(sessionCap ? MemoryCodes.MemoryRateLimited : MemoryCodes.MemoryCapReached);
        await using var count = _app.CreateCommand("SELECT count(*) FROM cowork_memory_entries WHERE account_id = 'cap-owner'");
        ((long)(await count.ExecuteScalarAsync(token).ConfigureAwait(true))!).ShouldBe((long)seeded + 1);
    }

    /// <summary>A delete holding the row first forces a concurrent save to observe the missing project.</summary>
    [Fact]
    public async Task DeleteWinsAgainstSave()
    {
        var token = TestContext.Current.CancellationToken;
        await EnsureProjectAsync("owner", "race").ConfigureAwait(true);
        await using var connection = await _app!.OpenConnectionAsync(token).ConfigureAwait(true);
        await using var transaction = await connection.BeginTransactionAsync(token).ConfigureAwait(true);
        await using var delete = new NpgsqlCommand("DELETE FROM cowork_projects WHERE id = 'race' AND account_id = 'owner'", connection, transaction);
        await delete.ExecuteNonQueryAsync(token).ConfigureAwait(true);
        var save = new PostgresMemoryStore(_app).SaveAsync(new MemoryScope("owner", "race", false, "s"), new MemoryDraft(null, "race", null), token);
        await WaitForBlockedAsync("SELECT 1 FROM cowork_projects", token).ConfigureAwait(true);
        await transaction.CommitAsync(token).ConfigureAwait(true);
        var result = await save.ConfigureAwait(true);
        result.Code.ShouldBe(MemoryCodes.MemoryNotFound);
        await using var count = _app.CreateCommand("SELECT count(*) FROM cowork_memory_entries");
        ((long)(await count.ExecuteScalarAsync(token).ConfigureAwait(true))!).ShouldBe(0);
    }

    /// <summary>A save holding its owned row lock first completes before deletion cascades its entry.</summary>
    [Fact]
    public async Task SaveWinsAgainstDelete()
    {
        var token = TestContext.Current.CancellationToken;
        await EnsureProjectAsync("owner", "race").ConfigureAwait(true);
        await using (var setup = _admin!.CreateCommand($"""
            CREATE FUNCTION "{_schema}".block_memory_insert() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN PERFORM pg_advisory_xact_lock(hashtextextended(TG_TABLE_SCHEMA, 0)); RETURN NEW; END $$;
            CREATE TRIGGER block_memory_insert BEFORE INSERT ON "{_schema}".cowork_memory_entries
            FOR EACH ROW EXECUTE FUNCTION "{_schema}".block_memory_insert()
            """))
        {
            await setup.ExecuteNonQueryAsync(token).ConfigureAwait(true);
        }
        await using var gateConnection = await _app!.OpenConnectionAsync(token).ConfigureAwait(true);
        await using var gate = new NpgsqlCommand("SELECT pg_advisory_lock(hashtextextended(@schema, 0))", gateConnection);
        gate.Parameters.AddWithValue("schema", _schema);
        await gate.ExecuteNonQueryAsync(token).ConfigureAwait(true);
        var save = new PostgresMemoryStore(_app).SaveAsync(new MemoryScope("owner", "race", false, "s"), new MemoryDraft(null, "race", null), token);
        await WaitForBlockedAsync("INSERT INTO cowork_memory_entries", token).ConfigureAwait(true);
        await using var delete = _app.CreateCommand("DELETE FROM cowork_projects WHERE id = 'race' AND account_id = 'owner'");
        var deleting = delete.ExecuteNonQueryAsync(token);
        await WaitForBlockedAsync("DELETE FROM cowork_projects", token).ConfigureAwait(true);
        await using var release = new NpgsqlCommand("SELECT pg_advisory_unlock(hashtextextended(@schema, 0))", gateConnection);
        release.Parameters.AddWithValue("schema", _schema);
        await release.ExecuteNonQueryAsync(token).ConfigureAwait(true);
        (await save.ConfigureAwait(true)).Succeeded.ShouldBeTrue();
        (await deleting.ConfigureAwait(true)).ShouldBe(1);
        await using var count = _app.CreateCommand("SELECT count(*) FROM cowork_memory_entries");
        ((long)(await count.ExecuteScalarAsync(token).ConfigureAwait(true))!).ShouldBe(0);
    }

    /// <summary>Claims survive independent stores, isolate accounts, reject changed fields and retain tombstones.</summary>
    [Fact]
    public async Task KeyedCreatesReplayAndIsolateAccounts()
    {
        var token = TestContext.Current.CancellationToken;
        var scope = new MemoryScope("key-owner", null, false, "session");
        var draft = new MemoryDraft("topic", "content", null);
        var firstStore = new PostgresMemoryStore(_app!);
        var builder = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("AUTONOGRAMMER_TEST_POSTGRES_CONNECTION"))
        {
            SearchPath = _schema + ",public",
            ApplicationName = _schema,
            Username = _role,
            Password = _password,
        };
        await using var independent = NpgsqlDataSource.Create(builder.ConnectionString);
        var secondStore = new PostgresMemoryStore(independent);
        await using var barrierConnection = await _app!.OpenConnectionAsync(token).ConfigureAwait(true);
        await using var barrier = new NpgsqlCommand("SELECT pg_advisory_lock(hashtextextended('cowork-memory:' || @account, 0))", barrierConnection);
        barrier.Parameters.AddWithValue("account", scope.AccountId);
        await barrier.ExecuteNonQueryAsync(token).ConfigureAwait(true);
        var first = firstStore.SaveIdempotentAsync(scope, draft, "key", token);
        var second = secondStore.SaveIdempotentAsync(scope, draft, "key", token);
        await WaitForBlockedAsync("SELECT pg_advisory_xact_lock", token, 2).ConfigureAwait(true);
        await using var release = new NpgsqlCommand("SELECT pg_advisory_unlock(hashtextextended('cowork-memory:' || @account, 0))", barrierConnection);
        release.Parameters.AddWithValue("account", scope.AccountId);
        await release.ExecuteNonQueryAsync(token).ConfigureAwait(true);
        var results = await Task.WhenAll(first, second).ConfigureAwait(true);
        results.Select(static result => result.Status).ShouldContain(MemoryIdempotentStatus.Created);
        results.Select(static result => result.Status).ShouldContain(MemoryIdempotentStatus.Replayed);
        results[0].Mutation!.Entry!.Id.ShouldBe(results[1].Mutation!.Entry!.Id);
        var replay = await new PostgresMemoryStore(_app!).SaveIdempotentAsync(scope, draft, "key", token).ConfigureAwait(true);
        replay.Status.ShouldBe(MemoryIdempotentStatus.Replayed);
        replay.Mutation!.Entry!.Id.ShouldBe(results[0].Mutation!.Entry!.Id);
        (await firstStore.SaveIdempotentAsync(scope, draft with { Content = "different" }, "key", token).ConfigureAwait(true)).Status.ShouldBe(MemoryIdempotentStatus.Conflict);
        (await firstStore.SaveIdempotentAsync(scope, draft with { Category = string.Empty }, "key", token).ConfigureAwait(true)).Status.ShouldBe(MemoryIdempotentStatus.Conflict);
        (await firstStore.SaveIdempotentAsync(scope with { UseAccountMemory = true }, draft, "key", token).ConfigureAwait(true)).Status.ShouldBe(MemoryIdempotentStatus.Conflict);
        (await firstStore.SaveIdempotentAsync(scope with { SessionId = "other-session" }, draft, "key", token).ConfigureAwait(true)).Status.ShouldBe(MemoryIdempotentStatus.Conflict);
        (await firstStore.SaveIdempotentAsync(scope with { AccountId = "other" }, draft, "key", token).ConfigureAwait(true)).Status.ShouldBe(MemoryIdempotentStatus.Created);
        await using (var count = _app!.CreateCommand("SELECT (SELECT count(*) FROM cowork_memory_entries), (SELECT count(*) FROM cowork_memory_idempotency)"))
        await using (var reader = await count.ExecuteReaderAsync(token).ConfigureAwait(true))
        {
            (await reader.ReadAsync(token).ConfigureAwait(true)).ShouldBeTrue();
            reader.GetInt64(0).ShouldBe(2);
            reader.GetInt64(1).ShouldBe(2);
        }
        await AssertFingerprintVersionAsync(firstStore, scope, draft, token).ConfigureAwait(true);

        await firstStore.ResetAsync(scope.AccountId, token).ConfigureAwait(true);
        (await secondStore.SaveIdempotentAsync(scope, draft, "key", token).ConfigureAwait(true)).Status.ShouldBe(MemoryIdempotentStatus.Gone);
        await using (var expire = _app.CreateCommand("UPDATE cowork_memory_idempotency SET expires_at = clock_timestamp() - interval '1 second' WHERE account_id = 'key-owner'"))
        {
            await expire.ExecuteNonQueryAsync(token).ConfigureAwait(true);
        }
        (await secondStore.SaveIdempotentAsync(scope, draft, "key", token).ConfigureAwait(true)).Status.ShouldBe(MemoryIdempotentStatus.Created);
    }

    /// <summary>Deleting an entry or its project leaves a non-content claim; matching replay is gone.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeletedEntryOrProjectReturnsGone(bool deleteProject)
    {
        var token = TestContext.Current.CancellationToken;
        var store = new PostgresMemoryStore(_app!);
        await EnsureProjectAsync("owner", "project").ConfigureAwait(true);
        var scope = new MemoryScope("owner", "project", false, "session");
        var draft = new MemoryDraft(null, Canary, null);
        var created = await store.SaveIdempotentAsync(scope, draft, "delete-key", token).ConfigureAwait(true);
        created.Status.ShouldBe(MemoryIdempotentStatus.Created);
        if (deleteProject)
        {
            await using var delete = _app!.CreateCommand("DELETE FROM cowork_projects WHERE account_id = 'owner' AND id = 'project'");
            (await delete.ExecuteNonQueryAsync(token).ConfigureAwait(true)).ShouldBe(1);
        }
        else
        {
            (await store.DeleteAsync(scope, created.Mutation!.Entry!.Id, token).ConfigureAwait(true)).Succeeded.ShouldBeTrue();
        }

        var replay = await store.SaveIdempotentAsync(scope, draft, "delete-key", token).ConfigureAwait(true);
        replay.Status.ShouldBe(MemoryIdempotentStatus.Gone);
        replay.Mutation.ShouldBeNull();
        (await store.SaveIdempotentAsync(scope, draft with { Content = "different" }, "delete-key", token).ConfigureAwait(true)).Status.ShouldBe(MemoryIdempotentStatus.Conflict);
    }

    /// <summary>A deletion that holds the entry row first makes a waiting replay observe a tombstone.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeletionWinsAgainstReplay(bool projectCascade)
    {
        var token = TestContext.Current.CancellationToken;
        await EnsureProjectAsync("owner", "project").ConfigureAwait(true);
        var store = new PostgresMemoryStore(_app!);
        var scope = new MemoryScope("owner", "project", false, "session");
        var draft = new MemoryDraft(null, Canary, null);
        (await store.SaveIdempotentAsync(scope, draft, "race-key", token).ConfigureAwait(true)).Status.ShouldBe(MemoryIdempotentStatus.Created);
        await using var connection = await _app!.OpenConnectionAsync(token).ConfigureAwait(true);
        await using var transaction = await connection.BeginTransactionAsync(token).ConfigureAwait(true);
        var sql = projectCascade
            ? "DELETE FROM cowork_projects WHERE account_id = 'owner' AND id = 'project'"
            : "DELETE FROM cowork_memory_entries WHERE account_id = 'owner'";
        await using (var delete = new NpgsqlCommand(sql, connection, transaction))
        {
            (await delete.ExecuteNonQueryAsync(token).ConfigureAwait(true)).ShouldBe(1);
        }
        var replay = store.SaveIdempotentAsync(scope, draft, "race-key", token);
        await WaitForBlockedAsync("FROM cowork_memory_entries WHERE account_id", token).ConfigureAwait(true);
        await transaction.CommitAsync(token).ConfigureAwait(true);
        var result = await replay.ConfigureAwait(true);
        result.Status.ShouldBe(MemoryIdempotentStatus.Gone);
        result.Mutation.ShouldBeNull();
    }

    /// <summary>A single request prunes at most 100 expired claims of its own account without deleting entries.</summary>
    [Fact]
    public async Task OpportunisticPruningIsBoundedAndAccountScoped()
    {
        var token = TestContext.Current.CancellationToken;
        var store = new PostgresMemoryStore(_app!);
        var scope = new MemoryScope("owner", null, false, "session");
        var draft = new MemoryDraft(null, Canary, null);
        var first = await store.SaveIdempotentAsync(scope, draft, "live", token).ConfigureAwait(true);
        first.Status.ShouldBe(MemoryIdempotentStatus.Created);
        await using (var seed = _app!.CreateCommand("""
            INSERT INTO cowork_memory_idempotency (account_id, operation, key, fingerprint_version, fingerprint, entry_id, created_at, expires_at)
            SELECT 'owner', 'create', 'expired-' || n, 1, decode(repeat('ab', 32), 'hex'), 'missing', clock_timestamp() - interval '2 days', clock_timestamp() - interval '1 day'
            FROM generate_series(1, 102) n;
            INSERT INTO cowork_memory_idempotency (account_id, operation, key, fingerprint_version, fingerprint, entry_id, created_at, expires_at)
            VALUES ('other', 'create', 'expired', 1, decode(repeat('ab', 32), 'hex'), 'missing', clock_timestamp() - interval '2 days', clock_timestamp() - interval '1 day')
            """))
        {
            await seed.ExecuteNonQueryAsync(token).ConfigureAwait(true);
        }
        (await store.SaveIdempotentAsync(scope, draft, "live", token).ConfigureAwait(true)).Status.ShouldBe(MemoryIdempotentStatus.Replayed);
        await using var count = _app.CreateCommand("""
            SELECT (SELECT count(*) FROM cowork_memory_idempotency WHERE account_id = 'owner' AND key LIKE 'expired-%'),
                   (SELECT count(*) FROM cowork_memory_idempotency WHERE account_id = 'other'),
                   (SELECT count(*) FROM cowork_memory_entries WHERE account_id = 'owner')
            """);
        await using var reader = await count.ExecuteReaderAsync(token).ConfigureAwait(true);
        (await reader.ReadAsync(token).ConfigureAwait(true)).ShouldBeTrue();
        reader.GetInt64(0).ShouldBe(2);
        reader.GetInt64(1).ShouldBe(1);
        reader.GetInt64(2).ShouldBe(1);
    }

    /// <summary>Database expiry preserves a live replay and permits reuse once the deadline is reached.</summary>
    [Fact]
    public async Task ExpiryPreservesLiveReplayAndThenPermitsReuse()
    {
        var token = TestContext.Current.CancellationToken;
        var store = new PostgresMemoryStore(_app!);
        var scope = new MemoryScope("owner", null, false, "session");
        var draft = new MemoryDraft(null, Canary, null);
        var first = await store.SaveIdempotentAsync(scope, draft, "expiry", token).ConfigureAwait(true);
        first.Status.ShouldBe(MemoryIdempotentStatus.Created);
        await using (var future = _app!.CreateCommand("UPDATE cowork_memory_idempotency SET expires_at = clock_timestamp() + interval '1 minute' WHERE account_id = 'owner' AND key = 'expiry'"))
        {
            (await future.ExecuteNonQueryAsync(token).ConfigureAwait(true)).ShouldBe(1);
        }
        var replay = await store.SaveIdempotentAsync(scope, draft, "expiry", token).ConfigureAwait(true);
        replay.Status.ShouldBe(MemoryIdempotentStatus.Replayed);
        replay.Mutation!.Entry!.Id.ShouldBe(first.Mutation!.Entry!.Id);
        await using (var expire = _app.CreateCommand("UPDATE cowork_memory_idempotency SET expires_at = clock_timestamp() WHERE account_id = 'owner' AND key = 'expiry'"))
        {
            (await expire.ExecuteNonQueryAsync(token).ConfigureAwait(true)).ShouldBe(1);
        }
        var reused = await store.SaveIdempotentAsync(scope, draft, "expiry", token).ConfigureAwait(true);
        reused.Status.ShouldBe(MemoryIdempotentStatus.Created);
        StringComparer.Ordinal.Equals(reused.Mutation!.Entry!.Id, first.Mutation!.Entry!.Id).ShouldBeFalse();
    }

    /// <summary>The actual database-sampled expiry predicate replays one microsecond before the deadline and creates at equality.</summary>
    [Fact]
    public async Task ExpiryBoundaryUsesDatabaseSampleAtMicrosecondPrecision()
    {
        var token = TestContext.Current.CancellationToken;
        var store = new PostgresMemoryStore(_app!);
        var scope = new MemoryScope("boundary-owner", null, false, "session");
        var draft = new MemoryDraft(null, Canary, null);
        var created = await store.SaveIdempotentAsync(scope, draft, "boundary", token).ConfigureAwait(true);
        created.Status.ShouldBe(MemoryIdempotentStatus.Created);
        var boundary = new PostgresMemoryStore(_app)
        {
            AfterExpiryTimeSample = async (asOf, ct) =>
            {
                await using var update = _app.CreateCommand("""
                    UPDATE cowork_memory_idempotency SET expires_at = @as_of + interval '1 microsecond'
                    WHERE account_id = 'boundary-owner' AND key = 'boundary'
                    """);
                update.Parameters.AddWithValue("as_of", asOf);
                (await update.ExecuteNonQueryAsync(ct).ConfigureAwait(false)).ShouldBe(1);
            },
        };
        var replay = await boundary.SaveIdempotentAsync(scope, draft, "boundary", token).ConfigureAwait(true);
        replay.Status.ShouldBe(MemoryIdempotentStatus.Replayed);
        replay.Mutation!.Entry!.Id.ShouldBe(created.Mutation!.Entry!.Id);
        boundary.AfterExpiryTimeSample = async (asOf, ct) =>
        {
            await using var update = _app.CreateCommand("""
                UPDATE cowork_memory_idempotency SET expires_at = @as_of
                WHERE account_id = 'boundary-owner' AND key = 'boundary'
                """);
            update.Parameters.AddWithValue("as_of", asOf);
            (await update.ExecuteNonQueryAsync(ct).ConfigureAwait(false)).ShouldBe(1);
        };
        var reused = await boundary.SaveIdempotentAsync(scope, draft, "boundary", token).ConfigureAwait(true);
        reused.Status.ShouldBe(MemoryIdempotentStatus.Created);
        StringComparer.Ordinal.Equals(reused.Mutation!.Entry!.Id, created.Mutation!.Entry!.Id).ShouldBeFalse();
    }

    /// <summary>Cleanup holding an expired claim serializes with reuse and leaves exactly one new claim and two entries.</summary>
    [Fact]
    public async Task ExpiredClaimCleanupSerializesWithReuse()
    {
        var token = TestContext.Current.CancellationToken;
        var store = new PostgresMemoryStore(_app!);
        var scope = new MemoryScope("owner", null, false, "session");
        var draft = new MemoryDraft(null, Canary, null);
        (await store.SaveIdempotentAsync(scope, draft, "expiry-key", token).ConfigureAwait(true)).Status.ShouldBe(MemoryIdempotentStatus.Created);
        await using (var expire = _app!.CreateCommand("UPDATE cowork_memory_idempotency SET expires_at = clock_timestamp() WHERE account_id = 'owner' AND key = 'expiry-key'"))
        {
            (await expire.ExecuteNonQueryAsync(token).ConfigureAwait(true)).ShouldBe(1);
        }
        await using var connection = await _app.OpenConnectionAsync(token).ConfigureAwait(true);
        await using var transaction = await connection.BeginTransactionAsync(token).ConfigureAwait(true);
        await using (var cleanup = new NpgsqlCommand("DELETE FROM cowork_memory_idempotency WHERE account_id = 'owner' AND key = 'expiry-key' AND expires_at <= clock_timestamp()", connection, transaction))
        {
            (await cleanup.ExecuteNonQueryAsync(token).ConfigureAwait(true)).ShouldBe(1);
        }
        var reuse = store.SaveIdempotentAsync(scope, draft, "expiry-key", token);
        await WaitForBlockedAsync("DELETE FROM cowork_memory_idempotency WHERE account_id", token).ConfigureAwait(true);
        await transaction.CommitAsync(token).ConfigureAwait(true);
        (await reuse.ConfigureAwait(true)).Status.ShouldBe(MemoryIdempotentStatus.Created);
        await using var count = _app.CreateCommand("SELECT (SELECT count(*) FROM cowork_memory_idempotency WHERE account_id = 'owner'), (SELECT count(*) FROM cowork_memory_entries WHERE account_id = 'owner')");
        await using var reader = await count.ExecuteReaderAsync(token).ConfigureAwait(true);
        (await reader.ReadAsync(token).ConfigureAwait(true)).ShouldBeTrue();
        reader.GetInt64(0).ShouldBe(1);
        reader.GetInt64(1).ShouldBe(2);
    }

    private async Task AssertFingerprintVersionAsync(PostgresMemoryStore store, MemoryScope scope, MemoryDraft draft, CancellationToken token)
    {
        await using (var window = _app!.CreateCommand("SELECT expires_at - created_at = interval '24 hours' FROM cowork_memory_idempotency WHERE account_id = 'key-owner'"))
        {
            ((bool)(await window.ExecuteScalarAsync(token).ConfigureAwait(true))!).ShouldBeTrue();
        }
        await using (var version = _app.CreateCommand("UPDATE cowork_memory_idempotency SET fingerprint_version = 2 WHERE account_id = 'key-owner'"))
        {
            await version.ExecuteNonQueryAsync(token).ConfigureAwait(true);
        }
        (await store.SaveIdempotentAsync(scope, draft, "key", token).ConfigureAwait(true)).Status.ShouldBe(MemoryIdempotentStatus.Conflict);
        await using var restore = _app.CreateCommand("UPDATE cowork_memory_idempotency SET fingerprint_version = 1 WHERE account_id = 'key-owner'");
        await restore.ExecuteNonQueryAsync(token).ConfigureAwait(true);
    }

    /// <summary>Suppressing an acknowledgement after commit leaves exactly one replayable result.</summary>
    [Fact]
    public async Task LostAcknowledgementReplaysCommittedEntry()
    {
        var token = TestContext.Current.CancellationToken;
        var calls = 0;
        var first = new PostgresMemoryStore(_app!) { SuppressCommitAcknowledgement = () => { calls++; return true; } };
        var scope = new MemoryScope("owner", null, false, "session");
        var draft = new MemoryDraft(null, "content", null);
        (await first.SaveIdempotentAsync(scope, draft, "key", token).ConfigureAwait(true)).Status.ShouldBe(MemoryIdempotentStatus.Unavailable);
        calls.ShouldBe(1);
        var replay = await new PostgresMemoryStore(_app!).SaveIdempotentAsync(scope, draft, "key", token).ConfigureAwait(true);
        replay.Status.ShouldBe(MemoryIdempotentStatus.Replayed);
        replay.Mutation!.Entry!.Content.ShouldBe("content");
        await using var count = _app!.CreateCommand("SELECT (SELECT count(*) FROM cowork_memory_entries), (SELECT count(*) FROM cowork_memory_idempotency)");
        await using var reader = await count.ExecuteReaderAsync(token).ConfigureAwait(true);
        (await reader.ReadAsync(token).ConfigureAwait(true)).ShouldBeTrue();
        reader.GetInt64(0).ShouldBe(1);
        reader.GetInt64(1).ShouldBe(1);
    }

    /// <summary>A failure after entry insertion rolls back both the entry and its claim.</summary>
    [Fact]
    public async Task ClaimFailureRollsBackInsertedEntry()
    {
        var token = TestContext.Current.CancellationToken;
        await using (var setup = _admin!.CreateCommand($"""
            CREATE FUNCTION "{_schema}".reject_claim() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'injected claim failure'; END $$;
            CREATE TRIGGER reject_claim BEFORE INSERT ON "{_schema}".cowork_memory_idempotency
            FOR EACH ROW EXECUTE FUNCTION "{_schema}".reject_claim()
            """))
        {
            await setup.ExecuteNonQueryAsync(token).ConfigureAwait(true);
        }
        var result = await new PostgresMemoryStore(_app!).SaveIdempotentAsync(
            new MemoryScope("owner", null, false, "session"), new MemoryDraft(null, "content", null), "key", token).ConfigureAwait(true);
        result.Status.ShouldBe(MemoryIdempotentStatus.Unavailable);
        await using var count = _app!.CreateCommand("SELECT (SELECT count(*) FROM cowork_memory_entries) + (SELECT count(*) FROM cowork_memory_idempotency)");
        ((long)(await count.ExecuteScalarAsync(token).ConfigureAwait(true))!).ShouldBe(0);
    }

    /// <summary>Keyed saves keep the existing ownership and cap protections.</summary>
    [Fact]
    public async Task KeyedWritesEnforceOwnershipAndCap()
    {
        var token = TestContext.Current.CancellationToken;
        await EnsureProjectAsync("owner", "project").ConfigureAwait(true);
        var store = new PostgresMemoryStore(_app!);
        var foreign = await store.SaveIdempotentAsync(new MemoryScope("other", "project", false, "session"), new MemoryDraft(null, "content", null), "foreign", token).ConfigureAwait(true);
        foreign.Status.ShouldBe(MemoryIdempotentStatus.Refused);
        foreign.Mutation!.Code.ShouldBe(MemoryCodes.MemoryNotFound);
        await using (var seed = _app!.CreateCommand("""
            INSERT INTO cowork_memory_entries (id, account_id, content, source_session, created_at, updated_at)
            SELECT 'cap-' || n::text, 'other', 'note', 'session', now(), now() FROM generate_series(1, 20) n
            """))
        {
            await seed.ExecuteNonQueryAsync(token).ConfigureAwait(true);
        }
        var capped = await store.SaveIdempotentAsync(new MemoryScope("other", null, false, "session"), new MemoryDraft(null, "content", null), "capped", token).ConfigureAwait(true);
        capped.Status.ShouldBe(MemoryIdempotentStatus.Refused);
        capped.Mutation!.Code.ShouldBe(MemoryCodes.MemoryRateLimited);
        await using var count = _app.CreateCommand("SELECT count(*) FROM cowork_memory_idempotency");
        ((long)(await count.ExecuteScalarAsync(token).ConfigureAwait(true))!).ShouldBe(0);
    }

    /// <summary>Two keyed requests cannot both consume the final account or session slot.</summary>
    [Theory]
    [InlineData(19, true)]
    [InlineData(999, false)]
    public async Task ConcurrentKeyedWritesRespectCaps(int seeded, bool sessionCap)
    {
        var token = TestContext.Current.CancellationToken;
        await using (var seed = _app!.CreateCommand("""
            INSERT INTO cowork_memory_entries (id, account_id, content, source_session, created_at, updated_at)
            SELECT 'seed-' || n::text, 'cap-owner', 'note',
                CASE WHEN @session_cap THEN 'same' ELSE 'old-' || n::text END, now(), now()
            FROM generate_series(1, @count) AS n
            """))
        {
            seed.Parameters.AddWithValue("session_cap", sessionCap);
            seed.Parameters.AddWithValue("count", seeded);
            await seed.ExecuteNonQueryAsync(token).ConfigureAwait(true);
        }
        await using var connection = await _app!.OpenConnectionAsync(token).ConfigureAwait(true);
        await using var gate = new NpgsqlCommand("SELECT pg_advisory_lock(hashtextextended('cowork-memory:' || @account, 0))", connection);
        gate.Parameters.AddWithValue("account", "cap-owner");
        await gate.ExecuteNonQueryAsync(token).ConfigureAwait(true);
        var scope = new MemoryScope("cap-owner", null, false, "same");
        var first = new PostgresMemoryStore(_app).SaveIdempotentAsync(scope, new MemoryDraft(null, "first", null), "first", token);
        var second = new PostgresMemoryStore(_app).SaveIdempotentAsync(scope, new MemoryDraft(null, "second", null), "second", token);
        await WaitForBlockedAsync("SELECT pg_advisory_xact_lock", token, 2).ConfigureAwait(true);
        await using var release = new NpgsqlCommand("SELECT pg_advisory_unlock(hashtextextended('cowork-memory:' || @account, 0))", connection);
        release.Parameters.AddWithValue("account", "cap-owner");
        await release.ExecuteNonQueryAsync(token).ConfigureAwait(true);
        var results = await Task.WhenAll(first, second).ConfigureAwait(true);
        results.Count(static result => result.Status == MemoryIdempotentStatus.Created).ShouldBe(1);
        var refused = results.Single(static result => result.Status == MemoryIdempotentStatus.Refused);
        refused.Mutation!.Code.ShouldBe(sessionCap ? MemoryCodes.MemoryRateLimited : MemoryCodes.MemoryCapReached);
        await using var count = _app.CreateCommand("SELECT count(*) FROM cowork_memory_idempotency WHERE account_id = 'cap-owner'");
        ((long)(await count.ExecuteScalarAsync(token).ConfigureAwait(true))!).ShouldBe(1);
    }

    /// <summary>Settings writes and replay serialize even when the settings row did not exist.</summary>
    [Theory]
    [InlineData(true, false)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(false, true)]
    public async Task SettingsWinsBeforeReplay(bool pause, bool existingSettings)
    {
        var token = TestContext.Current.CancellationToken;
        var store = new PostgresMemoryStore(_app!);
        var scope = new MemoryScope("owner", null, false, "session");
        var draft = new MemoryDraft(null, Canary, null);
        (await store.SaveIdempotentAsync(scope, draft, "key", token).ConfigureAwait(true)).Status.ShouldBe(MemoryIdempotentStatus.Created);
        if (existingSettings)
        {
            await store.SetSettingsAsync("owner", new MemorySettings(true, !pause, false), token).ConfigureAwait(true);
        }
        await using (var setup = _admin!.CreateCommand($"""
            CREATE FUNCTION "{_schema}".block_settings() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN PERFORM pg_advisory_xact_lock(hashtextextended(TG_TABLE_SCHEMA, 0)); RETURN NEW; END $$;
            CREATE TRIGGER block_settings BEFORE INSERT OR UPDATE ON "{_schema}".cowork_memory_settings
            FOR EACH ROW EXECUTE FUNCTION "{_schema}".block_settings()
            """))
        {
            await setup.ExecuteNonQueryAsync(token).ConfigureAwait(true);
        }
        await using var gateConnection = await _app!.OpenConnectionAsync(token).ConfigureAwait(true);
        await using var gate = new NpgsqlCommand("SELECT pg_advisory_lock(hashtextextended(@schema, 0))", gateConnection);
        gate.Parameters.AddWithValue("schema", _schema);
        await gate.ExecuteNonQueryAsync(token).ConfigureAwait(true);
        var setting = store.SetSettingsAsync("owner", new MemorySettings(true, pause, false), token);
        await WaitForBlockedAsync("INSERT INTO cowork_memory_settings", token).ConfigureAwait(true);
        var replay = new PostgresMemoryStore(_app).SaveIdempotentAsync(scope, draft, "key", token);
        await WaitForBlockedAsync("SELECT pg_advisory_xact_lock", token).ConfigureAwait(true);
        await using var release = new NpgsqlCommand("SELECT pg_advisory_unlock(hashtextextended(@schema, 0))", gateConnection);
        release.Parameters.AddWithValue("schema", _schema);
        await release.ExecuteNonQueryAsync(token).ConfigureAwait(true);
        await setting.ConfigureAwait(true);
        var result = await replay.ConfigureAwait(true);
        result.Status.ShouldBe(pause ? MemoryIdempotentStatus.Refused : MemoryIdempotentStatus.Replayed);
        if (pause)
        {
            result.Mutation!.Code.ShouldBe(MemoryCodes.MemoryPaused);
            result.Mutation.Entry.ShouldBeNull();
        }
        (await store.GetSettingsAsync("owner", token).ConfigureAwait(true)).Paused.ShouldBe(pause);
    }

    /// <summary>A replay holding the account lock finishes before a later settings write.</summary>
    [Theory]
    [InlineData(true, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task ReplayWinsBeforeSettings(bool pause, bool existingSettings)
    {
        var token = TestContext.Current.CancellationToken;
        var store = new PostgresMemoryStore(_app!);
        var scope = new MemoryScope("owner", null, false, "session");
        var draft = new MemoryDraft(null, Canary, null);
        (await store.SaveIdempotentAsync(scope, draft, "key", token).ConfigureAwait(true)).Status.ShouldBe(MemoryIdempotentStatus.Created);
        if (existingSettings)
        {
            await store.SetSettingsAsync("owner", new MemorySettings(true, !pause, false), token).ConfigureAwait(true);
        }
        await using var connection = await _app!.OpenConnectionAsync(token).ConfigureAwait(true);
        await using var transaction = await connection.BeginTransactionAsync(token).ConfigureAwait(true);
        await using (var gate = new NpgsqlCommand("LOCK TABLE cowork_memory_settings IN ACCESS EXCLUSIVE MODE", connection, transaction))
        {
            await gate.ExecuteNonQueryAsync(token).ConfigureAwait(true);
        }
        var replay = new PostgresMemoryStore(_app).SaveIdempotentAsync(scope, draft, "key", token);
        await WaitForBlockedAsync("SELECT enabled, paused, include_sensitive", token).ConfigureAwait(true);
        var setting = store.SetSettingsAsync("owner", new MemorySettings(true, pause, false), token);
        await WaitForBlockedAsync("SELECT pg_advisory_xact_lock", token).ConfigureAwait(true);
        await transaction.CommitAsync(token).ConfigureAwait(true);
        var result = await replay.ConfigureAwait(true);
        result.Status.ShouldBe(!pause && existingSettings ? MemoryIdempotentStatus.Refused : MemoryIdempotentStatus.Replayed);
        if (!pause && existingSettings) result.Mutation!.Entry.ShouldBeNull();
        await setting.ConfigureAwait(true);
        (await store.GetSettingsAsync("owner", token).ConfigureAwait(true)).Paused.ShouldBe(pause);
    }

    private async Task WaitForBlockedAsync(string sql, CancellationToken token, int minimum = 1)
    {
        for (var attempt = 0; attempt < 200; attempt++)
        {
            await using var command = _admin!.CreateCommand("SELECT count(*) FROM pg_stat_activity WHERE application_name = @app AND wait_event_type = 'Lock' AND position(@sql in query) > 0");
            command.Parameters.AddWithValue("app", _schema);
            command.Parameters.AddWithValue("sql", sql);
            if ((long)(await command.ExecuteScalarAsync(token).ConfigureAwait(true))! >= minimum) return;
            await Task.Delay(25, token).ConfigureAwait(true);
        }
        throw new TimeoutException("The expected database lock wait was not observed.");
    }

    /// <summary>EXPLAIN uses the M0009 GIN index for the search expression.</summary>
    [Fact]
    public async Task ExplainUsesTheSearchIndex()
    {
        PostgresMemoryStore.SearchVectorExpression.ShouldBe("to_tsvector('simple', coalesce(topic, '') || ' ' || coalesce(content, ''))");
        var store = new PostgresMemoryStore(_app!);
        var token = TestContext.Current.CancellationToken;
        _ = await store.SaveAsync(new MemoryScope("acct", null, false, "sess"), new MemoryDraft("tea", "oolong leaves", null), token).ConfigureAwait(true);
        var sql = $"""
            EXPLAIN (FORMAT TEXT)
            SELECT id FROM cowork_memory_entries
            WHERE {PostgresMemoryStore.SearchVectorExpression} @@ plainto_tsquery('simple', @query)
            """;
        await using var connection = await _app!.OpenConnectionAsync(token).ConfigureAwait(true);
        await using (var off = new NpgsqlCommand("SET enable_seqscan = off", connection))
        {
            _ = await off.ExecuteNonQueryAsync(token).ConfigureAwait(true);
        }

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("query", "oolong");
        await using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(true);
        var plan = new StringBuilder();
        while (await reader.ReadAsync(token).ConfigureAwait(true))
        {
            _ = plan.AppendLine(reader.GetString(0));
        }

        plan.ToString().ShouldContain(PostgresMemoryStore.SearchIndexName);
    }

    /// <summary>Npgsql logs and errors from real keyed writes do not expose payload, key or digest.</summary>
    [Fact]
    public async Task KeyedWriteLogsAndExceptionsDoNotExposeCanaries()
    {
        const string key = "key-canary-8172cfa9";
        var token = TestContext.Current.CancellationToken;
        var connectionString = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("AUTONOGRAMMER_TEST_POSTGRES_CONNECTION"))
        {
            SearchPath = _schema + ",public",
            ApplicationName = _schema,
            Username = _role,
            Password = _password,
        }.ConnectionString;
        using var logs = new CapturingLoggerFactory();
        var builder = new NpgsqlDataSourceBuilder(connectionString);
        builder.UseLoggerFactory(logs);
        await using var source = builder.Build();
        var store = new PostgresMemoryStore(source);
        var scope = new MemoryScope("log-owner", null, false, "session");
        var draft = new MemoryDraft(InstructionCanary, Canary, null);
        (await store.SaveIdempotentAsync(scope, draft, key, token).ConfigureAwait(true)).Status.ShouldBe(MemoryIdempotentStatus.Created);
        byte[] digest;
        await using (var command = _app!.CreateCommand("SELECT fingerprint FROM cowork_memory_idempotency WHERE account_id = 'log-owner'"))
        {
            digest = (byte[])(await command.ExecuteScalarAsync(token).ConfigureAwait(true))!;
        }
        await using (var setup = _admin!.CreateCommand($"""
            CREATE FUNCTION "{_schema}".reject_log_claim() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'injected claim failure'; END $$;
            CREATE TRIGGER reject_log_claim BEFORE INSERT ON "{_schema}".cowork_memory_idempotency
            FOR EACH ROW EXECUTE FUNCTION "{_schema}".reject_log_claim()
            """))
        {
            await setup.ExecuteNonQueryAsync(token).ConfigureAwait(true);
        }
        (await store.SaveIdempotentAsync(scope, draft, key + "-failure", token).ConfigureAwait(true)).Status.ShouldBe(MemoryIdempotentStatus.Unavailable);
        var exception = await Should.ThrowAsync<ArgumentException>(
            () => store.SaveIdempotentAsync(scope, draft, key + " invalid", token)).ConfigureAwait(true);
        logs.Lines.ShouldNotBeEmpty();
        var output = string.Join("\n", logs.Lines) + "\n" + exception;
        output.ShouldNotContain(Canary);
        output.ShouldNotContain(InstructionCanary);
        output.ShouldNotContain(key);
        output.ShouldNotContain(Convert.ToHexString(digest));
        output.ShouldNotContain(Convert.ToHexStringLower(digest));
        output.ShouldNotContain(Convert.ToBase64String(digest));
    }

    private sealed class CapturingLoggerFactory : ILoggerFactory
    {
        private readonly ConcurrentQueue<string> _lines = new();
        public IReadOnlyCollection<string> Lines => _lines;
        public void AddProvider(ILoggerProvider provider) { }
        public ILogger CreateLogger(string categoryName) => new CapturingLogger(_lines);
        public void Dispose() { }

        private sealed class CapturingLogger(ConcurrentQueue<string> lines) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                lines.Enqueue(formatter(state, exception) + (exception is null ? string.Empty : " | " + exception));
        }
    }

    /// <summary>Memory and instruction text never appear in ToString, a refusal sentence, or an exception message.</summary>
    [Fact]
    public async Task LogCanaryStaysOutOfExceptionsAndToString()
    {
        var store = new PostgresMemoryStore(_app!);
        var token = TestContext.Current.CancellationToken;
        var saved = await store.SaveAsync(new MemoryScope("acct", null, false, "sess"), new MemoryDraft(InstructionCanary, Canary, null), token).ConfigureAwait(true);
        saved.Entry.ShouldNotBeNull();
        saved.Entry.ToString().ShouldNotContain(Canary);
        saved.Entry.ToString().ShouldNotContain(InstructionCanary);
        var refused = await store.SaveAsync(new MemoryScope("acct", null, false, "sess"), new MemoryDraft("id", "SSN 219-09-9999 " + Canary, null), token).ConfigureAwait(true);
        refused.Sentence.ShouldNotBeNull();
        refused.Sentence.ShouldNotContain(Canary);
        refused.Sentence.ShouldNotContain(InstructionCanary);
        var thrown = await Should.ThrowAsync<ArgumentException>(() => new PostgresMemoryStore(_app!).GetSettingsAsync("  ", token)).ConfigureAwait(true);
        thrown.ToString().ShouldNotContain(Canary);
        thrown.ToString().ShouldNotContain(InstructionCanary);
    }
}
