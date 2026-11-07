using System.Globalization;
using Autonogrammer.Contracts.Dispatch;
using Autonogrammer.Resilience;
using Autonogrammer.Sessions.Postgres.Migrations;
using Shouldly;
using Xunit;

namespace Autonogrammer.Sessions.Postgres.Tests.Dispatch;

/// <summary>Runs the shared dispatch store contract, plus the Postgres allocation, owner and purge rules, against a throwaway schema.</summary>
[Collection(PostgresServerGroup.Name)]
public sealed class PostgresDispatchStoreTests(PostgresServerFixture server) : Autonogrammer.Contracts.Tests.Dispatch.Store.DispatchStoreContractTests<PostgresDispatchStore>, IAsyncLifetime
{
    private readonly List<PostgresTestSchema> _schemas = [];

    /// <inheritdoc />
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        foreach (var schema in _schemas)
        {
            await schema.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>M0010 is the step after cowork projects, and applying its DDL twice does not change the device columns.</summary>
    [Fact]
    public async Task M0010IsRegisteredAfterM0009AndIsIdempotent()
    {
        var ct = TestContext.Current.CancellationToken;
        var names = MigrationRegistry.All.Select(static step => step.Name).ToArray();
        names[^2].ShouldBe("cowork_projects_memory");
        names[^1].ShouldBe("dispatch_device_state");
        MigrationRegistry.All[^1].Version.ShouldBe(10);
        MigrationRegistry.All[^1].UpSql.ShouldContain("ADD COLUMN IF NOT EXISTS credential_jti");
        MigrationRegistry.All[^1].UpSql.ShouldContain("ADD COLUMN IF NOT EXISTS credential_expires_at");
        MigrationRegistry.All[^1].UpSql.ShouldContain("ADD COLUMN IF NOT EXISTS capabilities");
        MigrationRegistry.All[^1].UpSql.ShouldContain("ADD COLUMN IF NOT EXISTS app_version");
        MigrationRegistry.All[^1].UpSql.ShouldContain("ADD COLUMN IF NOT EXISTS protocol");
        MigrationRegistry.All[^1].UpSql.ShouldContain("ADD COLUMN IF NOT EXISTS revoke_reason");
        MigrationRegistry.All[^1].UpSql.ShouldContain("ADD COLUMN IF NOT EXISTS paired_session_id");
        MigrationRegistry.All[^1].UpSql.ShouldContain("ADD COLUMN IF NOT EXISTS last_source_prefix");
        MigrationRegistry.All[^1].UpSql.ShouldContain("CREATE UNIQUE INDEX IF NOT EXISTS dispatch_devices_id_account_uq");

        var schema = await OpenSchemaAsync(ct).ConfigureAwait(true);
        var connection = await schema.CreateDataSource().OpenConnectionAsync(ct).ConfigureAwait(true);
        await using (connection.ConfigureAwait(true))
        {
            using var again = connection.CreateCommand();
            again.CommandText = M0010Up;
            await again.ExecuteNonQueryAsync(ct).ConfigureAwait(true);
            using var columns = connection.CreateCommand();
            columns.CommandText = ColumnSql;
            var found = new List<string>();
            var reader = await columns.ExecuteReaderAsync(ct).ConfigureAwait(true);
            await using (reader.ConfigureAwait(true))
            {
                while (await reader.ReadAsync(ct).ConfigureAwait(true))
                {
                    found.Add(reader.GetString(0));
                }
            }

            found.ShouldBe(["app_version", "capabilities", "credential_expires_at", "credential_jti", "last_source_prefix", "paired_session_id", "protocol", "revoke_reason"]);
        }
    }

    /// <summary>Thirty-two concurrent inserts allocate each sequence from 1 to 32 once.</summary>
    [Fact]
    public async Task ThirtyTwoWritersAllocateEachSequenceOnce()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = await CreateStoreAsync(DispatchStoreLimits.Default, TimeProvider.System, ct).ConfigureAwait(true);
        (await store.CreateDeviceAsync(new CreateDispatchDevice("acct", "dev", "dev", null, "jti", null, [], null), ct).ConfigureAwait(true)).Status.ShouldBe(DispatchDeviceCreateStatus.Created);

        var tasks = new Task<DispatchEnqueueResult>[32];
        for (var i = 0; i < tasks.Length; i++)
        {
            var id = i.ToString(CultureInfo.InvariantCulture);
            tasks[i] = store.EnqueueAsync(
                new EnqueueDispatchCommand("acct", "dev", "c" + id, "k" + id, "fp", DispatchCapability.Sessions, DispatchOp.SessionList, "{}", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddMinutes(5)),
                ct).AsTask();
        }

        var results = await Task.WhenAll(tasks).ConfigureAwait(true);
        var seqs = results.Select(static item => item.Accepted!.Seq).Order().ToArray();
        seqs.ShouldBe(Enumerable.Range(1, 32).Select(static value => (long)value).ToArray());
    }

    /// <summary>Every read path scoped to one account returns nothing for another account.</summary>
    [Fact]
    public async Task CrossOwnerReadMatrixIsEmpty()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = await CreateStoreAsync(DispatchStoreLimits.Default, TimeProvider.System, ct).ConfigureAwait(true);
        await store.CreateDeviceAsync(new CreateDispatchDevice("acct", "dev", "secret-name", null, "jti", null, [], null), ct).ConfigureAwait(true);
        await store.EnqueueAsync(new EnqueueDispatchCommand("acct", "dev", "c1", "k", "fp", DispatchCapability.Sessions, DispatchOp.SessionList, "{\"body\":\"secret-body\"}", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddHours(1)), ct).ConfigureAwait(true);
        await store.AppendFeedEventAsync("acct", "queued", "{}", ct).ConfigureAwait(true);

        (await store.GetDeviceAsync("other", "dev", ct).ConfigureAwait(true)).ShouldBeNull();
        (await store.ListDevicesAsync("other", ct).ConfigureAwait(true)).ShouldBeEmpty();
        (await store.RenameDeviceAsync("other", "dev", "nope", ct).ConfigureAwait(true)).ShouldBeFalse();
        (await store.RevokeDeviceAsync("other", "dev", "nope", DateTimeOffset.UnixEpoch, ct).ConfigureAwait(true)).ShouldBeFalse();
        (await store.UpdateCredentialJtiAsync("other", "dev", "x", DateTimeOffset.UnixEpoch, ct).ConfigureAwait(true)).ShouldBeFalse();
        (await store.UpdateCapabilitySnapshotAsync("other", "dev", [], ct).ConfigureAwait(true)).ShouldBeFalse();
        (await store.TouchLastSeenAsync("other", "dev", DateTimeOffset.UnixEpoch, ct).ConfigureAwait(true)).ShouldBeFalse();
        (await store.UpdatePairedSessionAsync("other", "dev", "session", ct).ConfigureAwait(true)).ShouldBeFalse();
        (await store.GetCommandAsync("other", "c1", ct).ConfigureAwait(true)).ShouldBeNull();
        (await store.ListQueuedAfterAsync("other", "dev", 0, ct).ConfigureAwait(true)).ShouldBeEmpty();
        (await store.TryTransitionAsync("other", "c1", DispatchCommandState.Queued, DispatchCommandState.Delivered, null, ct).ConfigureAwait(true)).Status.ShouldBe(DispatchTransitionStatus.NotFound);
        (await store.EnqueueAsync(new EnqueueDispatchCommand("other", "dev", "c2", "k2", "fp", DispatchCapability.Sessions, DispatchOp.SessionList, "{}", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddHours(1)), ct).ConfigureAwait(true)).Status.ShouldBe(DispatchEnqueueStatus.NotFound);
        (await store.ReadFeedAsync("other", 0, ct).ConfigureAwait(true)).Events.ShouldBeEmpty();
        (await store.GetDeviceAsync("acct", "dev", ct).ConfigureAwait(true))!.Name.ShouldBe("secret-name");
    }

    /// <summary>A terminal body and its result text are cleared seven days later, and an expired command is not delivered.</summary>
    [Fact]
    public async Task PurgeClearsBodyAndExpiredCommandIsNotDelivered()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new MutableTime();
        var store = await CreateStoreAsync(DispatchStoreLimits.Default, clock, ct).ConfigureAwait(true);
        var schema = _schemas[^1];
        await store.CreateDeviceAsync(new CreateDispatchDevice("acct", "dev", "dev", null, "jti", null, [], null), ct).ConfigureAwait(true);
        await store.EnqueueAsync(
            new EnqueueDispatchCommand("acct", "dev", "live", "k-live", "fp", DispatchCapability.Sessions, DispatchOp.SessionList, "{\"body\":\"keep\"}", clock.UtcNow, clock.UtcNow.AddMinutes(5)),
            ct).ConfigureAwait(true);
        await store.EnqueueAsync(
            new EnqueueDispatchCommand("acct", "dev", "old", "k-old", "fp", DispatchCapability.Sessions, DispatchOp.SessionList, "{\"body\":\"purge-me\"}", clock.UtcNow, clock.UtcNow.AddMinutes(-1)),
            ct).ConfigureAwait(true);

        (await store.ListQueuedAfterAsync("acct", "dev", 0, ct).ConfigureAwait(true)).Select(static item => item.CommandId).ShouldBe(["live"]);

        await store.TryTransitionAsync("acct", "old", DispatchCommandState.Queued, DispatchCommandState.Expired, DispatchFailureReason.Expired, ct).ConfigureAwait(true);
        var seedConnection = await schema.CreateDataSource().OpenConnectionAsync(ct).ConfigureAwait(true);
        await using (seedConnection.ConfigureAwait(true))
        {
            using var seed = seedConnection.CreateCommand();
            seed.CommandText = SeedResultTextSql;
            (await seed.ExecuteNonQueryAsync(ct).ConfigureAwait(true)).ShouldBe(1);
        }

        clock.UtcNow = clock.UtcNow.AddDays(7).AddTicks(1);
        var purged = await store.GetCommandAsync("acct", "old", ct).ConfigureAwait(true);
        purged!.BodyJson.ShouldBeNull();

        var verify = await schema.CreateDataSource().OpenConnectionAsync(ct).ConfigureAwait(true);
        await using (verify.ConfigureAwait(true))
        {
            using var command = verify.CreateCommand();
            command.CommandText = PurgeCheckSql;
            var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(true);
            await using (reader.ConfigureAwait(true))
            {
                var saw = false;
                while (await reader.ReadAsync(ct).ConfigureAwait(true))
                {
                    saw = true;
                    reader.GetBoolean(0).ShouldBeTrue();
                    reader.GetBoolean(1).ShouldBeTrue();
                    reader.GetBoolean(2).ShouldBeTrue();
                    reader.GetString(3).ShouldBe("tenant-hash");
                    reader.GetString(3).ShouldNotContain("acct");
                }

                saw.ShouldBeTrue();
            }
        }
    }

    /// <summary>Audit rows written with a command never hold the body or the device name.</summary>
    [Fact]
    public async Task AuditRowsOmitBodyAndName()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = await CreateStoreAsync(DispatchStoreLimits.Default, TimeProvider.System, ct).ConfigureAwait(true);
        var schema = _schemas[^1];
        await store.CreateDeviceAsync(new CreateDispatchDevice("acct", "dev", "secret-name", null, "jti", null, [], null), ct).ConfigureAwait(true);
        await store.EnqueueAsync(new EnqueueDispatchCommand("acct", "dev", "c1", "k", "fp", DispatchCapability.Sessions, DispatchOp.SessionList, "{\"prompt\":\"secret-body\"}", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddHours(1)), ct).ConfigureAwait(true);

        var connection = await schema.CreateDataSource().OpenConnectionAsync(ct).ConfigureAwait(true);
        await using (connection.ConfigureAwait(true))
        {
            using var command = connection.CreateCommand();
            command.CommandText = AuditCountSql;
            var count = (long)(await command.ExecuteScalarAsync(ct).ConfigureAwait(true) ?? 0L);
            count.ShouldBeGreaterThan(0);
            using var scan = connection.CreateCommand();
            scan.CommandText = AuditScanSql;
            var reader = await scan.ExecuteReaderAsync(ct).ConfigureAwait(true);
            await using (reader.ConfigureAwait(true))
            {
                while (await reader.ReadAsync(ct).ConfigureAwait(true))
                {
                    var text = reader.GetString(0);
                    text.ShouldNotContain("secret-body");
                    text.ShouldNotContain("secret-name");
                    text.ShouldNotContain("prompt");
                }
            }
        }
    }

    /// <inheritdoc />
    protected override async Task<PostgresDispatchStore> CreateStoreAsync(DispatchStoreLimits limits, TimeProvider time, CancellationToken cancellationToken)
    {
        var schema = await OpenSchemaAsync(cancellationToken).ConfigureAwait(false);
        var resilience = PostgresResilience.Create(new PostgresResilienceOptions
        {
            PoolInteractive = 64,
            PoolBackground = 8,
            InteractiveCommandSeconds = 30,
            BudgetSeconds = 60,
        });
        return new PostgresDispatchStore(schema.CreateDataSource(), limits, time, static _ => "tenant-hash", resilience);
    }

    private async Task<PostgresTestSchema> OpenSchemaAsync(CancellationToken cancellationToken)
    {
        var schema = await PostgresTestSchema.CreateMigratedAsync(server.AdminConnectionString, cancellationToken).ConfigureAwait(false);
        _schemas.Add(schema);
        return schema;
    }

    private const string M0010Up = """
        ALTER TABLE dispatch_devices ADD COLUMN IF NOT EXISTS credential_jti text;
        ALTER TABLE dispatch_devices ADD COLUMN IF NOT EXISTS credential_expires_at timestamptz;
        ALTER TABLE dispatch_devices ADD COLUMN IF NOT EXISTS capabilities jsonb;
        ALTER TABLE dispatch_devices ADD COLUMN IF NOT EXISTS app_version text;
        ALTER TABLE dispatch_devices ADD COLUMN IF NOT EXISTS protocol text;
        ALTER TABLE dispatch_devices ADD COLUMN IF NOT EXISTS revoke_reason text;
        ALTER TABLE dispatch_devices ADD COLUMN IF NOT EXISTS paired_session_id text;
        ALTER TABLE dispatch_devices ADD COLUMN IF NOT EXISTS last_source_prefix text;
        CREATE UNIQUE INDEX IF NOT EXISTS dispatch_devices_id_account_uq ON dispatch_devices (id, account_id);
        """;

    private const string ColumnSql = """
        SELECT column_name FROM information_schema.columns
        WHERE table_schema = current_schema() AND table_name = 'dispatch_devices'
          AND column_name IN ('credential_jti', 'credential_expires_at', 'capabilities', 'app_version', 'protocol', 'revoke_reason', 'paired_session_id', 'last_source_prefix')
        ORDER BY column_name
        """;

    private const string SeedResultTextSql = "UPDATE dispatch_commands SET result = jsonb_set(result, '{text}', '\"result-text\"') WHERE id = 'old'";

    private const string PurgeCheckSql = "SELECT body IS NULL, body_purged_at IS NOT NULL, result->>'text' IS NULL, account_id_hash FROM dispatch_commands, dispatch_audit WHERE dispatch_commands.id = 'old'";

    private const string AuditCountSql = "SELECT count(*) FROM dispatch_audit WHERE op = 'command.enqueue'";

    private const string AuditScanSql = "SELECT coalesce(device_id, '') || coalesce(command_id, '') || op || outcome || coalesce(corr, '') || account_id_hash FROM dispatch_audit";

    private sealed class MutableTime : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 10, 30, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}
