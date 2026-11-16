using Autonogrammer.Resilience;
using Npgsql;
using Shouldly;
using Xunit;

namespace Autonogrammer.Sessions.Postgres.Tests;

/// <summary>
/// The all-writer account erasure fence (SEC-77-07, migration M0012) against a real PostgreSQL: a writer of every account-owned table that was admitted before the fence
/// committed cannot leave a row behind after the erasure, a writer that comes after the fence is refused with a typed error, the fence is idempotent and permanent, and an
/// erasure that failed halfway resumes with the fence still in place. Every case uses a fresh migrated schema on the shared server and separate connections for the writer and the eraser.
/// </summary>
[Collection(PostgresServerGroup.Name)]
public sealed class AccountErasureFenceTests(PostgresServerFixture server)
{
    private const string PrerequisitesSql = """
        INSERT INTO sessions (id, owner_id, created_at, updated_at, status, messages) VALUES (@a || '-s1', @a, now(), now(), 'Idle', '[]');
        INSERT INTO dispatch_devices (id, account_id, name, platform, public_key, created_at) VALUES (@a || '-dev0', @a, 'laptop', 'linux', 'key', now());
        INSERT INTO cowork_projects (id, account_id, name, created_at, updated_at) VALUES (@a || '-proj0', @a, 'project', now(), now());
        INSERT INTO artifacts (id, account_id, title, kind, latest_number, created_at, updated_at) VALUES (@a || '-art0', @a, 'title', 'doc', 1, now(), now());
        INSERT INTO artifact_versions (artifact_id, version, session_id, account_id, content_sha256, size_bytes, media_type, author, created_at, content)
            VALUES (@a || '-art0', 1, @a || '-s1', @a, 'sha', 3, 'text/plain', 'user', now(), '\x010203');
        """;

    private const string RowsOfTheAccountSql = """
        SELECT count(*) FROM (
            SELECT t::text AS row_text FROM accounts t
            UNION ALL SELECT t::text FROM dispatch_devices t
            UNION ALL SELECT t::text FROM dispatch_commands t
            UNION ALL SELECT t::text FROM dispatch_events t
            UNION ALL SELECT t::text FROM cowork_events t
            UNION ALL SELECT t::text FROM cowork_projects t
            UNION ALL SELECT t::text FROM cowork_instructions t
            UNION ALL SELECT t::text FROM cowork_memory_idempotency t
            UNION ALL SELECT t::text FROM cowork_memory_entries t
            UNION ALL SELECT t::text FROM cowork_memory_settings t
            UNION ALL SELECT t::text FROM artifacts t
            UNION ALL SELECT t::text FROM artifact_versions t
            UNION ALL SELECT t::text FROM artifact_grants t
            UNION ALL SELECT t::text FROM sessions t
            UNION ALL SELECT t::text FROM session_events t
            UNION ALL SELECT t::text FROM session_tool_approvals t
            UNION ALL SELECT t::text FROM cloud_agents t
        ) scanned WHERE row_text LIKE '%' || @a || '%'
        """;

    private const string FenceStateSql = "SELECT state FROM account_erasure_fence WHERE account_digest = autonogrammer_account_digest(@a)";

    private const string BlockedOnAnAdvisoryLockSql =
        "SELECT count(*) FROM pg_stat_activity WHERE datname = current_database() AND wait_event_type = 'Lock' AND wait_event = 'advisory' AND query LIKE '%autonogrammer_account_lock_key%' AND query NOT LIKE '%pg_stat_activity%'";

    /// <summary>One writer statement per account-owned table: each inserts one row for the account <c>@a</c> and relies on <see cref="PrerequisitesSql"/>.</summary>
    private static readonly (string Table, string Sql)[] Writers =
    [
        ("accounts", "INSERT INTO accounts (id, created_at, last_seen_at) VALUES (@a, now(), now())"),
        ("sessions", "INSERT INTO sessions (id, owner_id, created_at, updated_at, status, messages) VALUES (@a || '-s2', @a, now(), now(), 'Idle', '[]')"),
        ("session_events", "INSERT INTO session_events (session_id, seq, correlation_id, occurred_at, payload) VALUES (@a || '-s1', 1, 'c', now(), '{}')"),
        ("session_tool_approvals", "INSERT INTO session_tool_approvals (session_id, request_id, tool_name, granted_tools, requested_at) VALUES (@a || '-s1', 'r1', 'Bash', '[]', now())"),
        ("cloud_agents", "INSERT INTO cloud_agents (id, owner_id, session_id, status, created_at, updated_at, record) VALUES (@a || '-agent', @a, @a || '-s1', 'Active', now(), now(), '{}')"),
        ("dispatch_devices", "INSERT INTO dispatch_devices (id, account_id, name, platform, public_key, created_at) VALUES (@a || '-dev1', @a, 'phone', 'ios', 'key', now())"),
        ("dispatch_commands", "INSERT INTO dispatch_commands (id, device_id, account_id, seq, op, state, idempotency_key, expires_at, created_at, updated_at) VALUES (@a || '-cmd1', @a || '-dev0', @a, 1, 'op', 'queued', 'key-1', now(), now(), now())"),
        ("dispatch_events", "INSERT INTO dispatch_events (account_id, id, occurred_at, kind) VALUES (@a, 1, now(), 'device.paired')"),
        ("cowork_events", "INSERT INTO cowork_events (account_id, session_id, seq, occurred_at, kind, payload) VALUES (@a, @a || '-s1', 1, now(), 'artifact.created', '{}')"),
        ("cowork_projects", "INSERT INTO cowork_projects (id, account_id, name, created_at, updated_at) VALUES (@a || '-proj1', @a, 'second', now(), now())"),
        ("cowork_instructions", "INSERT INTO cowork_instructions (account_id, body, updated_at) VALUES (@a, 'be brief', now())"),
        ("cowork_memory_entries", "INSERT INTO cowork_memory_entries (id, account_id, project_id, topic, content, created_at, updated_at) VALUES (@a || '-mem1', @a, @a || '-proj0', 'topic', 'content', now(), now())"),
        ("cowork_memory_idempotency", "INSERT INTO cowork_memory_idempotency (account_id, operation, key, fingerprint_version, fingerprint, entry_id, created_at, expires_at) VALUES (@a, 'create', 'key-1', 1, decode(repeat('ab', 32), 'hex'), @a || '-mem1', now(), now() + interval '24 hours')"),
        ("cowork_memory_settings", "INSERT INTO cowork_memory_settings (account_id, enabled) VALUES (@a, true)"),
        ("artifacts", "INSERT INTO artifacts (id, account_id, title, kind, latest_number, created_at, updated_at) VALUES (@a || '-art1', @a, 'title', 'doc', 1, now(), now())"),
        ("artifact_versions", "INSERT INTO artifact_versions (artifact_id, version, session_id, account_id, content_sha256, size_bytes, media_type, author, created_at, content) VALUES (@a || '-art0', 2, @a || '-s1', @a, 'sha2', 3, 'text/plain', 'user', now(), '\\x010203')"),
        ("artifact_grants", "INSERT INTO artifact_grants (artifact_id, version, account_id, content_sha256, origins, scope, device, granted_at) VALUES (@a || '-art0', 1, @a, 'sha', '[]', 'read', 'dev', now())"),
    ];

    /// <summary>Every account-owned table has a writer statement here, so a table added later without one fails the migration coverage test first.</summary>
    [Fact]
    public void EveryFamilyOfTheEraserHasAWriterCase() =>
        Writers.Select(static writer => writer.Table).ShouldBe(
        [
            "accounts", "sessions", "session_events", "session_tool_approvals", "cloud_agents", "dispatch_devices", "dispatch_commands", "dispatch_events",
            "cowork_events", "cowork_projects", "cowork_instructions", "cowork_memory_entries", "cowork_memory_idempotency", "cowork_memory_settings",
            "artifacts", "artifact_versions", "artifact_grants",
        ]);

    /// <summary>While the fence says erasing, every writer of every family is refused with SQLSTATE AE001, and another account is not affected.</summary>
    [Fact]
    public async Task EveryFamilyWriterIsRefusedWhileTheFenceIsErasingAndAnotherAccountIsNot()
    {
        var ct = TestContext.Current.CancellationToken;
        var schema = await PostgresTestSchema.CreateMigratedAsync(server.AdminConnectionString, ct).ConfigureAwait(true);
        try
        {
            var dataSource = schema.CreateDataSource();
            var fenced = NewAccount("fenced");
            var other = NewAccount("other");
            await ExecuteAsync(dataSource, PrerequisitesSql, fenced, ct).ConfigureAwait(true);
            await ExecuteAsync(dataSource, PrerequisitesSql, other, ct).ConfigureAwait(true);
            await ExecuteAsync(dataSource, "INSERT INTO account_erasure_fence (account_digest, state) VALUES (autonogrammer_account_digest(@a), 'erasing')", fenced, ct).ConfigureAwait(true);

            foreach (var (table, sql) in Writers)
            {
                (await SqlStateOfAsync(dataSource, sql, fenced, ct).ConfigureAwait(true)).ShouldBe("AE001", $"the {table} writer of a fenced account must be refused");
                (await SqlStateOfAsync(dataSource, sql, other, ct).ConfigureAwait(true)).ShouldBeNull($"the {table} writer of another account must succeed");
            }

            (await SqlStateOfAsync(dataSource, "UPDATE sessions SET status = 'Running' WHERE id = @a || '-s1'", fenced, ct).ConfigureAwait(true)).ShouldBe("AE001");
            (await SqlStateOfAsync(dataSource, "UPDATE sessions SET status = 'Running' WHERE id = @a || '-s1'", other, ct).ConfigureAwait(true)).ShouldBeNull();
        }
        finally
        {
            await schema.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>After the erasure completed, no writer can recreate a row of any family: the fence row is permanent. Rows whose owner is gone are refused too (AE002).</summary>
    [Fact]
    public async Task EveryFamilyWriterIsRefusedAfterTheErasureCompleted()
    {
        var ct = TestContext.Current.CancellationToken;
        var schema = await PostgresTestSchema.CreateMigratedAsync(server.AdminConnectionString, ct).ConfigureAwait(true);
        try
        {
            var dataSource = schema.CreateDataSource();
            var account = NewAccount("erased");
            await ExecuteAsync(dataSource, PrerequisitesSql, account, ct).ConfigureAwait(true);
            await new PostgresAccountErasure(dataSource).EraseAsync(account, "hash", static (_, _) => Task.CompletedTask, ct).ConfigureAwait(true);

            (await ScalarAsync(dataSource, FenceStateSql, account, ct).ConfigureAwait(true)).ShouldBe("erased");
            foreach (var (table, sql) in Writers)
            {
                IsRefusal(await SqlStateOfAsync(dataSource, sql, account, ct).ConfigureAwait(true)).ShouldBeTrue($"the {table} writer must be refused after the erasure");
            }

            (await ScalarAsync(dataSource, RowsOfTheAccountSql, account, ct).ConfigureAwait(true)).ShouldBe(0L);
        }
        finally
        {
            await schema.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>
    /// A writer of each family that was admitted before the fence (it holds the account in an open transaction) delays the fence; once it commits, the erasure deletes its row.
    /// No row of the writer survives the erasure.
    /// </summary>
    [Fact]
    public async Task WriterAdmittedBeforeTheFenceCannotLeaveARowBehindAfterTheErasure()
    {
        var ct = TestContext.Current.CancellationToken;
        var schema = await PostgresTestSchema.CreateMigratedAsync(server.AdminConnectionString, ct).ConfigureAwait(true);
        try
        {
            var dataSource = schema.CreateDataSource();
            foreach (var (table, sql) in Writers)
            {
                var account = NewAccount(table);
                await ExecuteAsync(dataSource, PrerequisitesSql, account, ct).ConfigureAwait(true);

                var writer = await dataSource.OpenConnectionAsync(ct).ConfigureAwait(true);
                await using (writer.ConfigureAwait(true))
                {
                    var transaction = await writer.BeginTransactionAsync(ct).ConfigureAwait(true);
                    await using (transaction.ConfigureAwait(true))
                    {
                        await ExecuteAsync(writer, transaction, sql, account, ct).ConfigureAwait(true);

                        var erasure = Task.Run(() => new PostgresAccountErasure(dataSource).EraseAsync(account, "hash", static (_, _) => Task.CompletedTask, ct), ct);
                        await WaitUntilAnEraserIsBlockedAsync(dataSource, ct).ConfigureAwait(true);
                        erasure.IsCompleted.ShouldBeFalse($"the erasure must wait for the admitted {table} writer");
                        (await ScalarAsync(dataSource, FenceStateSql, account, ct).ConfigureAwait(true)).ShouldBeNull("the fence must not be visible before the admitted writer finished");

                        await transaction.CommitAsync(ct).ConfigureAwait(true);
                        await erasure.ConfigureAwait(true);
                    }
                }

                (await ScalarAsync(dataSource, FenceStateSql, account, ct).ConfigureAwait(true)).ShouldBe("erased");
                (await ScalarAsync(dataSource, RowsOfTheAccountSql, account, ct).ConfigureAwait(true)).ShouldBe(0L, $"the {table} writer must leave no row after the erasure");
            }
        }
        finally
        {
            await schema.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>A writer whose transaction began before the fence but that writes only after the erasure completed is refused for every family, and its transaction leaves no row.</summary>
    [Fact]
    public async Task WriterThatBeganBeforeTheFenceButWritesAfterItIsRefused()
    {
        var ct = TestContext.Current.CancellationToken;
        var schema = await PostgresTestSchema.CreateMigratedAsync(server.AdminConnectionString, ct).ConfigureAwait(true);
        try
        {
            var dataSource = schema.CreateDataSource();
            foreach (var (table, sql) in Writers)
            {
                var account = NewAccount(table);
                await ExecuteAsync(dataSource, PrerequisitesSql, account, ct).ConfigureAwait(true);

                var writer = await dataSource.OpenConnectionAsync(ct).ConfigureAwait(true);
                await using (writer.ConfigureAwait(true))
                {
                    var transaction = await writer.BeginTransactionAsync(ct).ConfigureAwait(true);
                    await using (transaction.ConfigureAwait(true))
                    {
                        await ExecuteAsync(writer, transaction, "SELECT 1", account, ct).ConfigureAwait(true);
                        await new PostgresAccountErasure(dataSource).EraseAsync(account, "hash", static (_, _) => Task.CompletedTask, ct).ConfigureAwait(true);

                        var failure = await Should.ThrowAsync<PostgresException>(() => ExecuteAsync(writer, transaction, sql, account, ct)).ConfigureAwait(true);
                        IsRefusal(failure.SqlState).ShouldBeTrue($"the late {table} writer must be refused");
                    }
                }

                (await ScalarAsync(dataSource, RowsOfTheAccountSql, account, ct).ConfigureAwait(true)).ShouldBe(0L, $"the late {table} writer must leave no row");
            }
        }
        finally
        {
            await schema.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>Writers that hold the account past the lock timeout make the erasure fail with a typed error and change nothing; it succeeds once they are gone.</summary>
    [Fact]
    public async Task ErasureReportsBusyWhenAdmittedWritersHoldTheAccountAndChangesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var schema = await PostgresTestSchema.CreateMigratedAsync(server.AdminConnectionString, ct).ConfigureAwait(true);
        try
        {
            var dataSource = schema.CreateDataSource();
            var account = NewAccount("busy");
            await ExecuteAsync(dataSource, PrerequisitesSql, account, ct).ConfigureAwait(true);
            var writer = await dataSource.OpenConnectionAsync(ct).ConfigureAwait(true);
            await using (writer.ConfigureAwait(true))
            {
                var transaction = await writer.BeginTransactionAsync(ct).ConfigureAwait(true);
                await using (transaction.ConfigureAwait(true))
                {
                    await ExecuteAsync(writer, transaction, Writers[0].Sql, account, ct).ConfigureAwait(true);

                    var erasure = new PostgresAccountErasure(dataSource, null, TimeSpan.FromMilliseconds(400));
                    await Should.ThrowAsync<AccountErasureBusyException>(
                        () => erasure.EraseAsync(account, "hash", static (_, _) => Task.CompletedTask, ct)).ConfigureAwait(true);

                    (await ScalarAsync(dataSource, FenceStateSql, account, ct).ConfigureAwait(true)).ShouldBeNull();
                    await transaction.CommitAsync(ct).ConfigureAwait(true);
                }
            }

            await new PostgresAccountErasure(dataSource).EraseAsync(account, "hash", static (_, _) => Task.CompletedTask, ct).ConfigureAwait(true);
            (await ScalarAsync(dataSource, FenceStateSql, account, ct).ConfigureAwait(true)).ShouldBe("erased");
        }
        finally
        {
            await schema.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>
    /// The fence is inserted once and never changes after completion: a second erasure keeps its timestamps, only runs the tenant-key delegate, and the row can be neither deleted
    /// nor reopened. The row holds a digest, not the account identifier.
    /// </summary>
    [Fact]
    public async Task FenceIsIdempotentPermanentAndHoldsNoAccountIdentifier()
    {
        var ct = TestContext.Current.CancellationToken;
        var schema = await PostgresTestSchema.CreateMigratedAsync(server.AdminConnectionString, ct).ConfigureAwait(true);
        try
        {
            var dataSource = schema.CreateDataSource();
            var account = NewAccount("idem");
            await ExecuteAsync(dataSource, PrerequisitesSql, account, ct).ConfigureAwait(true);
            var calls = 0;
            var erasure = new PostgresAccountErasure(dataSource);

            await erasure.EraseAsync(account, "hash", (_, _) => { calls++; return Task.CompletedTask; }, ct).ConfigureAwait(true);
            var first = await ScalarAsync(dataSource, "SELECT started_at::text || '/' || completed_at::text FROM account_erasure_fence", account, ct).ConfigureAwait(true);
            await erasure.EraseAsync(account, "hash", (_, _) => { calls++; return Task.CompletedTask; }, ct).ConfigureAwait(true);

            calls.ShouldBe(2);
            (await ScalarAsync(dataSource, "SELECT count(*) FROM account_erasure_fence", account, ct).ConfigureAwait(true)).ShouldBe(1L);
            (await ScalarAsync(dataSource, "SELECT started_at::text || '/' || completed_at::text FROM account_erasure_fence", account, ct).ConfigureAwait(true)).ShouldBe(first);
            (await ScalarAsync(dataSource, "SELECT count(*) FROM account_erasure_fence t WHERE t::text LIKE '%' || @a || '%'", account, ct).ConfigureAwait(true)).ShouldBe(0L);

            (await SqlStateOfAsync(dataSource, "DELETE FROM account_erasure_fence", account, ct).ConfigureAwait(true)).ShouldBe("AE004");
            (await SqlStateOfAsync(dataSource, "UPDATE account_erasure_fence SET state = 'erasing', completed_at = NULL", account, ct).ConfigureAwait(true)).ShouldBe("AE004");
            (await ScalarAsync(dataSource, FenceStateSql, account, ct).ConfigureAwait(true)).ShouldBe("erased");
        }
        finally
        {
            await schema.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>
    /// An erasure that failed in the middle of the families leaves the fence in place (writers stay refused, the account row stays), and the repeated call finishes the erasure and runs the
    /// tenant-key delegate only then.
    /// </summary>
    [Fact]
    public async Task ErasureThatFailedHalfwayKeepsTheFenceAndResumes()
    {
        var ct = TestContext.Current.CancellationToken;
        var schema = await PostgresTestSchema.CreateMigratedAsync(server.AdminConnectionString, ct).ConfigureAwait(true);
        try
        {
            var dataSource = schema.CreateDataSource();
            var account = NewAccount("resume");
            await ExecuteAsync(dataSource, PrerequisitesSql, account, ct).ConfigureAwait(true);
            await ExecuteAsync(dataSource, "INSERT INTO accounts (id, created_at, last_seen_at) VALUES (@a, now(), now())", account, ct).ConfigureAwait(true);
            await ExecuteAsync(dataSource, Writers.Single(static writer => string.Equals(writer.Table, "cloud_agents", StringComparison.Ordinal)).Sql, account, ct).ConfigureAwait(true);
            await ExecuteAsync(
                dataSource,
                """
                CREATE FUNCTION reject_cloud_agent_delete() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN RAISE EXCEPTION 'injected failure'; END $$;
                CREATE TRIGGER reject_cloud_agent_delete BEFORE DELETE ON cloud_agents FOR EACH ROW EXECUTE FUNCTION reject_cloud_agent_delete()
                """,
                account,
                ct).ConfigureAwait(true);
            var calls = 0;
            var erasure = new PostgresAccountErasure(dataSource);

            await Should.ThrowAsync<PostgresException>(
                () => erasure.EraseAsync(account, "hash", (_, _) => { calls++; return Task.CompletedTask; }, ct)).ConfigureAwait(true);

            calls.ShouldBe(0);
            (await ScalarAsync(dataSource, FenceStateSql, account, ct).ConfigureAwait(true)).ShouldBe("erasing");
            (await ScalarAsync(dataSource, "SELECT count(*) FROM dispatch_devices WHERE account_id = @a", account, ct).ConfigureAwait(true)).ShouldBe(0L, "earlier families stay deleted");
            (await ScalarAsync(dataSource, "SELECT count(*) FROM accounts WHERE id = @a", account, ct).ConfigureAwait(true)).ShouldBe(1L, "the account row goes last");
            (await SqlStateOfAsync(dataSource, Writers[0].Sql, NewAccount("bystander"), ct).ConfigureAwait(true)).ShouldBeNull("another account is not fenced");
            (await SqlStateOfAsync(dataSource, Writers.Single(static writer => string.Equals(writer.Table, "cowork_projects", StringComparison.Ordinal)).Sql, account, ct).ConfigureAwait(true)).ShouldBe("AE001");

            await ExecuteAsync(dataSource, "DROP TRIGGER reject_cloud_agent_delete ON cloud_agents", account, ct).ConfigureAwait(true);
            await erasure.EraseAsync(account, "hash", (_, _) => { calls++; return Task.CompletedTask; }, ct).ConfigureAwait(true);

            calls.ShouldBe(1);
            (await ScalarAsync(dataSource, FenceStateSql, account, ct).ConfigureAwait(true)).ShouldBe("erased");
            (await ScalarAsync(dataSource, RowsOfTheAccountSql, account, ct).ConfigureAwait(true)).ShouldBe(0L);
        }
        finally
        {
            await schema.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>A failing tenant-key delegate leaves the fence erased, and the repeated call runs the delegate again until it succeeds: the call completes only when the keys are gone.</summary>
    [Fact]
    public async Task FailedTenantKeyDeletionIsRetriedOnTheNextCallWithoutReopeningAnything()
    {
        var ct = TestContext.Current.CancellationToken;
        var schema = await PostgresTestSchema.CreateMigratedAsync(server.AdminConnectionString, ct).ConfigureAwait(true);
        try
        {
            var dataSource = schema.CreateDataSource();
            var account = NewAccount("keys");
            await ExecuteAsync(dataSource, PrerequisitesSql, account, ct).ConfigureAwait(true);
            var erasure = new PostgresAccountErasure(dataSource);

            await Should.ThrowAsync<InvalidOperationException>(
                () => erasure.EraseAsync(account, "hash", static (_, _) => throw new InvalidOperationException("dragonfly is down"), ct)).ConfigureAwait(true);
            (await ScalarAsync(dataSource, FenceStateSql, account, ct).ConfigureAwait(true)).ShouldBe("erased");

            var keys = new List<string>();
            await erasure.EraseAsync(account, "hash", (hash, _) => { keys.Add(hash); return Task.CompletedTask; }, ct).ConfigureAwait(true);
            keys.ShouldBe(["hash"]);
        }
        finally
        {
            await schema.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>The stores that share the PostgreSQL pipeline surface the refusal of the fence as one typed exception that names no account.</summary>
    [Fact]
    public async Task StoresSurfaceAFencedWriteAsATypedAccountErasingException()
    {
        var ct = TestContext.Current.CancellationToken;
        var schema = await PostgresTestSchema.CreateMigratedAsync(server.AdminConnectionString, ct).ConfigureAwait(true);
        try
        {
            var dataSource = schema.CreateDataSource();
            var account = NewAccount("typed");
            await new PostgresAccountErasure(dataSource).EraseAsync(account, "hash", static (_, _) => Task.CompletedTask, ct).ConfigureAwait(true);
            var repository = schema.CreateRepository();

            var failure = await Should.ThrowAsync<AccountErasingException>(
                () => repository.SaveAsync(SessionSamples.Sample(account + "-s9") with { OwnerId = account }, ct)).ConfigureAwait(true);

            failure.SqlState.ShouldBe("AE001");
            failure.Message.ShouldNotContain(account);
            AccountErasureFence.IsRefusal(failure).ShouldBeTrue();
            var direct = await Should.ThrowAsync<PostgresException>(() => ExecuteAsync(dataSource, Writers[0].Sql, account, ct)).ConfigureAwait(true);
            AccountErasureFence.IsRefusal(direct).ShouldBeTrue("a store that runs its own commands lets the driver exception through");
            AccountErasureFence.IsRefusal(new InvalidOperationException("other")).ShouldBeFalse();
            (await ScalarAsync(dataSource, RowsOfTheAccountSql, account, ct).ConfigureAwait(true)).ShouldBe(0L);
        }
        finally
        {
            await schema.DisposeAsync().ConfigureAwait(true);
        }
    }

    private static bool IsRefusal(string? sqlState) => sqlState is "AE001" or "AE002";

    private static string NewAccount(string label) => $"acct-{label}-{Guid.NewGuid():N}";

    private static async Task WaitUntilAnEraserIsBlockedAsync(NpgsqlDataSource dataSource, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(20);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var command = dataSource.CreateCommand(BlockedOnAnAdvisoryLockSql);
            if ((long)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))! > 0)
            {
                return;
            }

            await Task.Delay(25, cancellationToken).ConfigureAwait(false);
        }

        throw new TimeoutException("The erasure never blocked on the account lock.");
    }

    private static async Task ExecuteAsync(NpgsqlDataSource dataSource, string sql, string account, CancellationToken cancellationToken)
    {
        using var command = dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("a", account);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string sql, string account, CancellationToken cancellationToken)
    {
        var batch = new NpgsqlBatch(connection, transaction);
        await using (batch.ConfigureAwait(false))
        {
            var command = new NpgsqlBatchCommand(sql);
            command.Parameters.AddWithValue("a", account);
            batch.BatchCommands.Add(command);
            await batch.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<object?> ScalarAsync(NpgsqlDataSource dataSource, string sql, string account, CancellationToken cancellationToken)
    {
        using var command = dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("a", account);
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return value is DBNull ? null : value;
    }

    private static async Task<string?> SqlStateOfAsync(NpgsqlDataSource dataSource, string sql, string account, CancellationToken cancellationToken)
    {
        try
        {
            await ExecuteAsync(dataSource, sql, account, cancellationToken).ConfigureAwait(false);
            return null;
        }
        catch (PostgresException exception)
        {
            return exception.SqlState;
        }
    }
}
