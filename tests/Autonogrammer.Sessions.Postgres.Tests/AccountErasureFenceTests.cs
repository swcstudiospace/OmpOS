using Autonogrammer.Contracts.Cloud;
using Autonogrammer.Resilience;
using Npgsql;
using Shouldly;
using Xunit;

namespace Autonogrammer.Sessions.Postgres.Tests;

/// <summary>
/// The account erasure fence against a real server: erasure removes an account's families and marks its lifecycle row erased, writers and direct SQL
/// are refused once the account is erasing or erased, erasure waits for a writer that holds the account's admission, and erasure gives up instead of
/// partially erasing when that writer holds the lock past the lock timeout. Every case uses a fresh migrated schema on the shared server.
/// </summary>
[Collection(PostgresServerGroup.Name)]
public sealed class AccountErasureFenceTests(PostgresServerFixture server)
{
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 9, 0, 0, TimeSpan.Zero);
    private static readonly Func<string, CancellationToken, Task> NoTenantKeys = static (_, _) => Task.CompletedTask;

    private const string SeedSql = """
        INSERT INTO accounts (id, created_at, last_seen_at) VALUES (@a, now(), now());
        INSERT INTO cowork_projects (id, account_id, name, created_at, updated_at) VALUES (@a || '-proj', @a, 'project', now(), now());
        INSERT INTO cowork_instructions (account_id, body, updated_at) VALUES (@a, 'be brief', now());
        INSERT INTO cowork_memory_entries (id, account_id, project_id, topic, content, created_at, updated_at)
            VALUES (@a || '-mem', @a, @a || '-proj', 'topic', 'content', now(), now());
        """;

    private const string InsertMemoryEntrySql =
        "INSERT INTO cowork_memory_entries (id, account_id, project_id, topic, content, created_at, updated_at) VALUES (@id, @a, @a || '-proj', 'late', 'late', now(), now())";

    private const string SetStateSql = "UPDATE account_lifecycle SET state = @state, updated_at = now() WHERE account_id = @a";

    private const string StateSql = "SELECT state FROM account_lifecycle WHERE account_id = @a";

    /// <summary>Counts the rows of the account in the families the erasure must empty: the account row, its project, instructions and memory entry.</summary>
    private const string RowCountSql = """
        SELECT (SELECT count(*) FROM accounts WHERE id = @a)
             + (SELECT count(*) FROM cowork_projects WHERE account_id = @a)
             + (SELECT count(*) FROM cowork_instructions WHERE account_id = @a)
             + (SELECT count(*) FROM cowork_memory_entries WHERE account_id = @a)
        """;

    private const string CloudAgentCountSql = "SELECT count(*) FROM cloud_agents WHERE owner_id = @a";

    private const string BlockedBehindSql = "SELECT count(*) FROM pg_stat_activity WHERE @pid = ANY(pg_blocking_pids(pid))";

    /// <summary>Erasure removes every row of the account, leaves the other account untouched, and marks the lifecycle row erased.</summary>
    [Fact]
    public async Task ErasureRemovesTheAccountsRowsAndMarksItErased()
    {
        var ct = TestContext.Current.CancellationToken;
        var schema = await PostgresTestSchema.CreateMigratedAsync(server.AdminConnectionString, ct).ConfigureAwait(true);
        try
        {
            var dataSource = schema.CreateDataSource();
            var alice = NewAccount();
            var bob = NewAccount();
            await ProvisionAsync(dataSource, alice, ct).ConfigureAwait(true);
            await ProvisionAsync(dataSource, bob, ct).ConfigureAwait(true);
            await SeedAsync(dataSource, alice, ct).ConfigureAwait(true);
            await SeedAsync(dataSource, bob, ct).ConfigureAwait(true);
            (await RowCountAsync(dataSource, alice, ct).ConfigureAwait(true)).ShouldBe(4L);

            await new PostgresAccountErasure(dataSource).EraseAsync(alice, HashOf(alice), NoTenantKeys, ct).ConfigureAwait(true);

            (await RowCountAsync(dataSource, alice, ct).ConfigureAwait(true)).ShouldBe(0L);
            (await StateAsync(dataSource, alice, ct).ConfigureAwait(true)).ShouldBe("erased");
            (await RowCountAsync(dataSource, bob, ct).ConfigureAwait(true)).ShouldBe(4L);
            (await StateAsync(dataSource, bob, ct).ConfigureAwait(true)).ShouldBe("active");
        }
        finally
        {
            await schema.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>A cloud agent write for an erased account is refused with <see cref="AccountErasingException"/> and writes nothing.</summary>
    [Fact]
    public async Task WriteForAnErasedAccountIsRefused()
    {
        var ct = TestContext.Current.CancellationToken;
        var schema = await PostgresTestSchema.CreateMigratedAsync(server.AdminConnectionString, ct).ConfigureAwait(true);
        try
        {
            var dataSource = schema.CreateDataSource();
            var alice = NewAccount();
            await ProvisionAsync(dataSource, alice, ct).ConfigureAwait(true);
            await SeedAsync(dataSource, alice, ct).ConfigureAwait(true);
            await new PostgresAccountErasure(dataSource).EraseAsync(alice, HashOf(alice), NoTenantKeys, ct).ConfigureAwait(true);

            var store = new PostgresCloudAgentStore(dataSource);
            await Should.ThrowAsync<AccountErasingException>(
                () => store.CreateAsync(Record("after-erasure", alice), new CloudAgentLimits(10, 10), ct)).ConfigureAwait(true);

            (await CloudAgentCountAsync(dataSource, alice, ct).ConfigureAwait(true)).ShouldBe(0L);
            (await StateAsync(dataSource, alice, ct).ConfigureAwait(true)).ShouldBe("erased");
        }
        finally
        {
            await schema.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>After another account is erased, a write for an untouched account still succeeds.</summary>
    [Fact]
    public async Task WriteForAnotherAccountSucceedsAfterErasure()
    {
        var ct = TestContext.Current.CancellationToken;
        var schema = await PostgresTestSchema.CreateMigratedAsync(server.AdminConnectionString, ct).ConfigureAwait(true);
        try
        {
            var dataSource = schema.CreateDataSource();
            var alice = NewAccount();
            var bob = NewAccount();
            await ProvisionAsync(dataSource, alice, ct).ConfigureAwait(true);
            await ProvisionAsync(dataSource, bob, ct).ConfigureAwait(true);
            await SeedAsync(dataSource, alice, ct).ConfigureAwait(true);
            await new PostgresAccountErasure(dataSource).EraseAsync(alice, HashOf(alice), NoTenantKeys, ct).ConfigureAwait(true);

            var store = new PostgresCloudAgentStore(dataSource);
            var created = await store.CreateAsync(Record("bob-agent", bob), new CloudAgentLimits(10, 10), ct).ConfigureAwait(true);

            created.Outcome.ShouldBe(CloudAgentCreateOutcome.Created);
            (await store.GetAsync("bob-agent", ct).ConfigureAwait(true)).ShouldNotBeNull();
            (await CloudAgentCountAsync(dataSource, bob, ct).ConfigureAwait(true)).ShouldBe(1L);
        }
        finally
        {
            await schema.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>Direct SQL that bypasses the application is refused by the trigger with SQLSTATE AE001 once the account is erasing.</summary>
    [Fact]
    public async Task DirectInsertIsRefusedByTriggerOnceTheAccountIsErasing()
    {
        var ct = TestContext.Current.CancellationToken;
        var schema = await PostgresTestSchema.CreateMigratedAsync(server.AdminConnectionString, ct).ConfigureAwait(true);
        try
        {
            var dataSource = schema.CreateDataSource();
            var alice = NewAccount();
            await ProvisionAsync(dataSource, alice, ct).ConfigureAwait(true);
            await SeedAsync(dataSource, alice, ct).ConfigureAwait(true);

            await InsertLateMemoryEntryAsync(dataSource, alice, alice + "-late-active", ct).ConfigureAwait(true);
            (await RowCountAsync(dataSource, alice, ct).ConfigureAwait(true)).ShouldBe(5L);

            await SetStateAsync(dataSource, alice, "erasing", ct).ConfigureAwait(true);
            var refused = await Should.ThrowAsync<PostgresException>(
                () => InsertLateMemoryEntryAsync(dataSource, alice, alice + "-late-erasing", ct)).ConfigureAwait(true);

            refused.SqlState.ShouldBe("AE001");
            (await RowCountAsync(dataSource, alice, ct).ConfigureAwait(true)).ShouldBe(5L);
        }
        finally
        {
            await schema.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>Erasure waits for a writer that holds the account's admission, and proceeds once that writer commits.</summary>
    [Fact]
    public async Task ErasureBlocksWhileAWriterHoldsAdmissionAndProceedsAfterItCommits()
    {
        var ct = TestContext.Current.CancellationToken;
        var schema = await PostgresTestSchema.CreateMigratedAsync(server.AdminConnectionString, ct).ConfigureAwait(true);
        try
        {
            var dataSource = schema.CreateDataSource();
            var alice = NewAccount();
            await ProvisionAsync(dataSource, alice, ct).ConfigureAwait(true);
            await SeedAsync(dataSource, alice, ct).ConfigureAwait(true);

            using var writer = await dataSource.OpenConnectionAsync(ct).ConfigureAwait(true);
            using var transaction = await writer.BeginTransactionAsync(ct).ConfigureAwait(true);
            await AccountAdmission.AdmitSharedAsync(writer, transaction, alice, ct).ConfigureAwait(true);

            var erasing = new PostgresAccountErasure(dataSource).EraseAsync(alice, HashOf(alice), NoTenantKeys, ct);
            await WaitUntilBlockedBehindAsync(dataSource, writer.ProcessID, ct).ConfigureAwait(true);

            erasing.IsCompleted.ShouldBeFalse("erasure must wait for the writer's shared admission");
            (await StateAsync(dataSource, alice, ct).ConfigureAwait(true)).ShouldBe("active");
            (await RowCountAsync(dataSource, alice, ct).ConfigureAwait(true)).ShouldBe(4L);

            await transaction.CommitAsync(ct).ConfigureAwait(true);
            await erasing.ConfigureAwait(true);

            (await StateAsync(dataSource, alice, ct).ConfigureAwait(true)).ShouldBe("erased");
            (await RowCountAsync(dataSource, alice, ct).ConfigureAwait(true)).ShouldBe(0L);
        }
        finally
        {
            await schema.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>
    /// A writer that holds the admission past the lock timeout makes erasure give up: it throws, nothing is erased, and the account is not marked erased.
    /// </summary>
    [Fact]
    public async Task ErasureFailsClosedWhenTheLockIsHeldPastTheLockTimeout()
    {
        var ct = TestContext.Current.CancellationToken;
        var schema = await PostgresTestSchema.CreateMigratedAsync(server.AdminConnectionString, ct).ConfigureAwait(true);
        try
        {
            var dataSource = schema.CreateDataSource();
            var alice = NewAccount();
            await ProvisionAsync(dataSource, alice, ct).ConfigureAwait(true);
            await SeedAsync(dataSource, alice, ct).ConfigureAwait(true);

            using var writer = await dataSource.OpenConnectionAsync(ct).ConfigureAwait(true);
            using var transaction = await writer.BeginTransactionAsync(ct).ConfigureAwait(true);
            await AccountAdmission.AdmitSharedAsync(writer, transaction, alice, ct).ConfigureAwait(true);

            // The erasure runs while the writer still holds its admission. Awaiting it here proves it gave up on the 30 second lock timeout before the writer commits.
            var failure = await Should.ThrowAsync<Exception>(() => new PostgresAccountErasure(dataSource).EraseAsync(alice, HashOf(alice), NoTenantKeys, ct)).ConfigureAwait(true);

            // The lock timeout is server-side (AccountErasureLockTimeoutException); the client's deadline may fire first (DataPlaneTimeoutException or
            // an NpgsqlException timeout). Each of them is a refusal that changes nothing; what matters is the state and the rows below.
            (failure is AccountErasureLockTimeoutException or DataPlaneTimeoutException or NpgsqlException).ShouldBeTrue(failure.ToString());
            (await StateAsync(dataSource, alice, ct).ConfigureAwait(true)).ShouldNotBe("erased", StringComparer.Ordinal);
            (await RowCountAsync(dataSource, alice, ct).ConfigureAwait(true)).ShouldBe(4L);

            await transaction.CommitAsync(ct).ConfigureAwait(true);

            (await StateAsync(dataSource, alice, ct).ConfigureAwait(true)).ShouldNotBe("erased", StringComparer.Ordinal);
            (await RowCountAsync(dataSource, alice, ct).ConfigureAwait(true)).ShouldBe(4L);
        }
        finally
        {
            await schema.DisposeAsync().ConfigureAwait(true);
        }
    }

    private static string NewAccount() => "acct-" + Guid.NewGuid().ToString("N");

    /// <summary>A stand-in for the keyed tenant hash: it is handed to the delegate unchanged and never read by the erasure itself.</summary>
    private static string HashOf(string account) => "hash-" + account;

    private static async Task ProvisionAsync(NpgsqlDataSource dataSource, string accountId, CancellationToken cancellationToken)
    {
        using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(true);
        await AccountAdmission.EnsureRowAsync(connection, accountId, cancellationToken).ConfigureAwait(true);
    }

    private static async Task SeedAsync(NpgsqlDataSource dataSource, string account, CancellationToken cancellationToken)
    {
        using var command = dataSource.CreateCommand(SeedSql);
        command.Parameters.AddWithValue("a", account);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(true);
    }

    private static async Task InsertLateMemoryEntryAsync(NpgsqlDataSource dataSource, string account, string entryId, CancellationToken cancellationToken)
    {
        using var command = dataSource.CreateCommand(InsertMemoryEntrySql);
        command.Parameters.AddWithValue("id", entryId);
        command.Parameters.AddWithValue("a", account);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(true);
    }

    private static async Task SetStateAsync(NpgsqlDataSource dataSource, string account, string state, CancellationToken cancellationToken)
    {
        using var command = dataSource.CreateCommand(SetStateSql);
        command.Parameters.AddWithValue("state", state);
        command.Parameters.AddWithValue("a", account);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(true);
    }

    private static async Task<string?> StateAsync(NpgsqlDataSource dataSource, string account, CancellationToken cancellationToken)
    {
        using var command = dataSource.CreateCommand(StateSql);
        command.Parameters.AddWithValue("a", account);
        return (string?)await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(true);
    }

    private static async Task<long> RowCountAsync(NpgsqlDataSource dataSource, string account, CancellationToken cancellationToken)
    {
        using var command = dataSource.CreateCommand(RowCountSql);
        command.Parameters.AddWithValue("a", account);
        return (long)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(true))!;
    }

    private static async Task<long> CloudAgentCountAsync(NpgsqlDataSource dataSource, string account, CancellationToken cancellationToken)
    {
        using var command = dataSource.CreateCommand(CloudAgentCountSql);
        command.Parameters.AddWithValue("a", account);
        return (long)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(true))!;
    }

    /// <summary>Polls until some other session is blocked by <paramref name="blockerPid"/>: proof that the erasure is waiting on the writer's lock, not merely slow.</summary>
    private static async Task WaitUntilBlockedBehindAsync(NpgsqlDataSource dataSource, int blockerPid, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(15);
        using var command = dataSource.CreateCommand(BlockedBehindSql);
        command.Parameters.AddWithValue("pid", blockerPid);
        while (true)
        {
            if ((long)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(true))! > 0L)
            {
                return;
            }

            if (DateTimeOffset.UtcNow > deadline)
            {
                throw new TimeoutException("No session was observed waiting on the writer's lock.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken).ConfigureAwait(true);
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
            Branch = "autonogrammer/cloud/" + id,
            IdleMinutes = 15,
            LastActivityAt = Now,
        };
}
