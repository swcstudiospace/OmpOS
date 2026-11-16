using Autonogrammer.Sessions.Postgres.Migrations;
using Npgsql;
using Shouldly;
using Xunit;

namespace Autonogrammer.Sessions.Postgres.Tests;

/// <summary>
/// The migrations of the account erasure fence (M0012, and M0014 for the memory claims that M0013 creates): upgrade over existing data, repeat, revert and reapply, refusal of a
/// rollback while the fence holds an erasure record, coverage of every account-owned table by a fence trigger, and the actual runtime role (SEC-77-07).
/// </summary>
[Collection(PostgresServerGroup.Name)]
public sealed class AccountErasureFenceMigrationTests(PostgresServerFixture server)
{
    /// <summary>The checksum M0012_MemoryIdempotency recorded before it became M0013: the up SQL did not change, so a ledger written by either name verifies.</summary>
    private const string MemoryIdempotencyChecksum = "93ff9240b1bbfdda4e062d29ca00e7afa20a31c1417c8b4065d46450428deb67";

    private const string UnfencedTablesSql = """
        SELECT c.relname::text
        FROM pg_class AS c
        JOIN pg_namespace AS n ON n.oid = c.relnamespace
        WHERE n.nspname = @schema
          AND c.relkind IN ('r', 'p')
          AND c.relname <> 'account_erasure_fence'
          AND (c.relname = 'accounts' OR EXISTS (
                SELECT 1 FROM pg_attribute AS a
                WHERE a.attrelid = c.oid AND a.attname IN ('account_id', 'owner_id', 'session_id') AND NOT a.attisdropped))
          AND NOT EXISTS (
                SELECT 1 FROM pg_trigger AS t
                WHERE t.tgrelid = c.oid AND t.tgname = c.relname || '_account_fence' AND NOT t.tgisinternal)
        ORDER BY 1
        """;

    private const string FenceObjectsSql = """
        SELECT (SELECT count(*) FROM pg_proc AS p JOIN pg_namespace AS n ON n.oid = p.pronamespace WHERE n.nspname = @schema AND p.proname LIKE 'autonogrammer\_%')
             + (SELECT count(*) FROM pg_trigger AS t JOIN pg_class AS c ON c.oid = t.tgrelid JOIN pg_namespace AS n ON n.oid = c.relnamespace WHERE n.nspname = @schema AND NOT t.tgisinternal AND (t.tgname LIKE '%\_account\_fence' OR t.tgname = 'account_erasure_fence_row_guard'))
             + (SELECT count(*) FROM pg_class AS c JOIN pg_namespace AS n ON n.oid = c.relnamespace WHERE n.nspname = @schema AND c.relname = 'account_erasure_fence')
        """;

    /// <summary>The registry places the fence at 12, the memory claims at 13 and the claims' fence at 14, and the memory claims keep the checksum they had as version 12.</summary>
    [Fact]
    public void RegistryOrdersTheFenceBeforeTheMemoryClaimsAndKeepsTheirChecksum()
    {
        MigrationRegistry.All.Where(static step => step.Version >= 12).Select(static step => (step.Version, step.Name)).ShouldBe(
            [(12, "account_erasure_fence"), (13, "memory_idempotency"), (14, "memory_idempotency_fence")]);
        MigrationRunner.ChecksumOf(MigrationRegistry.All.Single(static step => step.Version == 13)).ShouldBe(MemoryIdempotencyChecksum);
    }

    /// <summary>The fence steps apply over a schema that already holds data at version 11, keep every row, and a second run applies nothing.</summary>
    [Fact]
    public async Task FenceStepsApplyOverExistingDataAndRepeat()
    {
        var ct = TestContext.Current.CancellationToken;
        var schema = await PostgresTestSchema.CreateAsync(server.AdminConnectionString, ct).ConfigureAwait(true);
        try
        {
            var dataSource = schema.CreateDataSource();
            await new MigrationRunner(dataSource, schema.SchemaName, [.. MigrationRegistry.All.Where(static step => step.Version <= 11)]).MigrateAsync(ct).ConfigureAwait(true);
            using (var seed = dataSource.CreateCommand("""
                INSERT INTO sessions (id, owner_id, created_at, updated_at, status, messages) VALUES ('s', 'kept', now(), now(), 'Idle', '[]');
                INSERT INTO dispatch_devices (id, account_id, name, platform, public_key, created_at) VALUES ('d', 'kept', 'n', 'p', 'k', now());
                """))
            {
                await seed.ExecuteNonQueryAsync(ct).ConfigureAwait(true);
            }

            var runner = new MigrationRunner(dataSource, schema.SchemaName);
            (await runner.MigrateAsync(ct).ConfigureAwait(true)).ShouldBe([12, 13, 14]);
            (await runner.MigrateAsync(ct).ConfigureAwait(true)).ShouldBeEmpty();
            (await runner.GetCurrentVersionAsync(ct).ConfigureAwait(true)).ShouldBe(14);
            (await ScalarAsync(dataSource, "SELECT (SELECT count(*) FROM sessions) + (SELECT count(*) FROM dispatch_devices)", schema.SchemaName, ct).ConfigureAwait(true)).ShouldBe(2L);
            var applied = await runner.ListAppliedAsync(ct).ConfigureAwait(true);
            foreach (var step in MigrationRegistry.All.Where(static step => step.Version >= 12))
            {
                applied.Single(migration => migration.Version == step.Version).Checksum.ShouldBe(MigrationRunner.ChecksumOf(step));
            }
        }
        finally
        {
            await schema.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>With no erasure record, the three steps revert in reverse order, leave no function, trigger or table of the fence behind, and apply again.</summary>
    [Fact]
    public async Task FenceStepsRevertAndReapplyWhenNothingWasErased()
    {
        var ct = TestContext.Current.CancellationToken;
        var schema = await PostgresTestSchema.CreateMigratedAsync(server.AdminConnectionString, ct).ConfigureAwait(true);
        try
        {
            var dataSource = schema.CreateDataSource();
            var runner = new MigrationRunner(dataSource, schema.SchemaName);
            (await ScalarAsync(dataSource, FenceObjectsSql, schema.SchemaName, ct).ConfigureAwait(true)).ShouldNotBe(0L);

            (await runner.DownAsync(11, ct).ConfigureAwait(true)).ShouldBe([14, 13, 12]);
            (await ScalarAsync(dataSource, FenceObjectsSql, schema.SchemaName, ct).ConfigureAwait(true)).ShouldBe(0L);

            (await runner.MigrateAsync(ct).ConfigureAwait(true)).ShouldBe([12, 13, 14]);
            (await ScalarAsync(dataSource, UnfencedTablesSql, schema.SchemaName, ct).ConfigureAwait(true)).ShouldBeNull();
        }
        finally
        {
            await schema.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>
    /// A rollback that would reopen an erased account is refused from every starting point: the runner reverts nothing, the memory claims table and every trigger stay, and the
    /// ledger stays at head. This is the path of <c>Autonogrammer.Api migrate --down</c>.
    /// </summary>
    [Fact]
    public async Task RollbackIsRefusedWhileTheFenceHoldsAnErasureRecordAndDropsNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var schema = await PostgresTestSchema.CreateMigratedAsync(server.AdminConnectionString, ct).ConfigureAwait(true);
        try
        {
            var dataSource = schema.CreateDataSource();
            await new PostgresAccountErasure(dataSource).EraseAsync("acct-" + Guid.NewGuid().ToString("N"), "hash", static (_, _) => Task.CompletedTask, ct).ConfigureAwait(true);
            var runner = new MigrationRunner(dataSource, schema.SchemaName);
            var objects = await ScalarAsync(dataSource, FenceObjectsSql, schema.SchemaName, ct).ConfigureAwait(true);

            foreach (var target in new[] { 13, 12, 11 })
            {
                var failure = await Should.ThrowAsync<PostgresException>(() => runner.DownAsync(target, ct)).ConfigureAwait(true);
                failure.SqlState.ShouldBe("AE005", $"a rollback to {target} must be refused");
                (await runner.GetCurrentVersionAsync(ct).ConfigureAwait(true)).ShouldBe(14);
                (await ScalarAsync(dataSource, FenceObjectsSql, schema.SchemaName, ct).ConfigureAwait(true)).ShouldBe(objects);
            }

            (await ScalarAsync(dataSource, "SELECT count(*) FROM account_erasure_fence", schema.SchemaName, ct).ConfigureAwait(true)).ShouldBe(1L);
            (await ScalarAsync(dataSource, "SELECT to_regclass(quote_ident(@schema) || '.cowork_memory_idempotency') IS NOT NULL", schema.SchemaName, ct).ConfigureAwait(true)).ShouldBe(true);
        }
        finally
        {
            await schema.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>Every table that holds account-owned rows (an <c>account_id</c>, <c>owner_id</c> or <c>session_id</c> column, and the accounts table) carries its fence trigger; a table added later without one fails here.</summary>
    [Fact]
    public async Task EveryAccountOwnedTableCarriesItsFenceTrigger()
    {
        var ct = TestContext.Current.CancellationToken;
        var schema = await PostgresTestSchema.CreateMigratedAsync(server.AdminConnectionString, ct).ConfigureAwait(true);
        try
        {
            var dataSource = schema.CreateDataSource();
            var unfenced = new List<string>();
            using var command = dataSource.CreateCommand(UnfencedTablesSql);
            command.Parameters.AddWithValue("schema", schema.SchemaName);
            var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(true);
            await using (reader.ConfigureAwait(true))
            {
                while (await reader.ReadAsync(ct).ConfigureAwait(true))
                {
                    unfenced.Add(reader.GetString(0));
                }
            }

            unfenced.ShouldBeEmpty();
        }
        finally
        {
            await schema.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>The runtime role (data manipulation only, no schema changes) writes, erases and is fenced like any writer, and cannot reopen, delete or drop a fence row.</summary>
    [Fact]
    public async Task RuntimeRoleErasesAndIsFencedAndCannotAlterTheFence()
    {
        var ct = TestContext.Current.CancellationToken;
        var roles = await RoleSchemaAsync.CreateAsync(server, "ALTER DEFAULT PRIVILEGES IN SCHEMA {schema} GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO {role}", ct).ConfigureAwait(true);
        await using var rolesDisposal = roles.ConfigureAwait(true);
        var app = roles.Role;
        using (var seed = app.CreateCommand("""
            INSERT INTO accounts (id, created_at, last_seen_at) VALUES ('acct-role', now(), now());
            INSERT INTO sessions (id, owner_id, created_at, updated_at, status, messages) VALUES ('acct-role-s1', 'acct-role', now(), now(), 'Idle', '[]');
            INSERT INTO cowork_memory_idempotency VALUES ('acct-role', 'create', 'key', 1, decode(repeat('00', 32), 'hex'), 'm', now(), now() + interval '24 hours');
            """))
        {
            await seed.ExecuteNonQueryAsync(ct).ConfigureAwait(true);
        }

        await new PostgresAccountErasure(app).EraseAsync("acct-role", "hash", static (_, _) => Task.CompletedTask, ct).ConfigureAwait(true);

        (await ScalarAsync(app, "SELECT state FROM account_erasure_fence", roles.Schema.SchemaName, ct).ConfigureAwait(true)).ShouldBe("erased");
        (await ScalarAsync(app, "SELECT (SELECT count(*) FROM sessions) + (SELECT count(*) FROM accounts) + (SELECT count(*) FROM cowork_memory_idempotency)", roles.Schema.SchemaName, ct).ConfigureAwait(true)).ShouldBe(0L);
        (await SqlStateOfAsync(app, "INSERT INTO accounts (id, created_at, last_seen_at) VALUES ('acct-role', now(), now())", ct).ConfigureAwait(true)).ShouldBe("AE001");
        (await SqlStateOfAsync(app, "INSERT INTO cowork_memory_idempotency VALUES ('acct-role', 'create', 'key', 1, decode(repeat('00', 32), 'hex'), 'm', now(), now())", ct).ConfigureAwait(true)).ShouldBe("AE001");
        (await SqlStateOfAsync(app, "DELETE FROM account_erasure_fence", ct).ConfigureAwait(true)).ShouldBe("AE004");
        (await SqlStateOfAsync(app, "UPDATE account_erasure_fence SET state = 'erasing', completed_at = NULL", ct).ConfigureAwait(true)).ShouldBe("AE004");
        (await SqlStateOfAsync(app, "DROP TABLE account_erasure_fence", ct).ConfigureAwait(true)).ShouldBe("42501");
        (await SqlStateOfAsync(app, "DROP FUNCTION autonogrammer_account_fence_check(text)", ct).ConfigureAwait(true)).ShouldBe("42501");
    }

    /// <summary>A role with grants on a single table (as the least-privilege store tests use) is fenced too: the check runs as the function owner, not with the grants of the writer.</summary>
    [Fact]
    public async Task RoleWithGrantsOnOneTableIsFencedWithoutAnyRightOnTheFence()
    {
        var ct = TestContext.Current.CancellationToken;
        var roles = await RoleSchemaAsync.CreateAsync(server, "GRANT SELECT, INSERT, UPDATE ON {schema}.cowork_memory_idempotency TO {role}", ct).ConfigureAwait(true);
        await using var rolesDisposal = roles.ConfigureAwait(true);
        var narrow = roles.Role;
        await new PostgresAccountErasure(roles.Schema.CreateDataSource()).EraseAsync("acct-erased", "hash", static (_, _) => Task.CompletedTask, ct).ConfigureAwait(true);

        (await SqlStateOfAsync(narrow, "SELECT count(*) FROM account_erasure_fence", ct).ConfigureAwait(true)).ShouldBe("42501");
        (await SqlStateOfAsync(narrow, "INSERT INTO cowork_memory_idempotency VALUES ('acct-erased', 'create', 'key', 1, decode(repeat('00', 32), 'hex'), 'm', now(), now())", ct).ConfigureAwait(true)).ShouldBe("AE001");
        (await SqlStateOfAsync(narrow, "INSERT INTO cowork_memory_idempotency VALUES ('acct-live', 'create', 'key', 1, decode(repeat('00', 32), 'hex'), 'm', now(), now())", ct).ConfigureAwait(true)).ShouldBeNull();
    }

    private static async Task<object?> ScalarAsync(NpgsqlDataSource dataSource, string sql, string schemaName, CancellationToken cancellationToken)
    {
        using var command = dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("schema", schemaName);
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return value is DBNull ? null : value;
    }

    private static async Task<string?> SqlStateOfAsync(NpgsqlDataSource dataSource, string sql, CancellationToken cancellationToken)
    {
        using var command = dataSource.CreateCommand(sql);
        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return null;
        }
        catch (PostgresException exception)
        {
            return exception.SqlState;
        }
    }

    /// <summary>A migrated schema with one login role whose grants the caller chooses; the placeholders <c>{schema}</c> and <c>{role}</c> in the grant statement are quoted identifiers.</summary>
    private sealed class RoleSchemaAsync : IAsyncDisposable
    {
        private readonly PostgresServerFixture _server;
        private readonly string _roleName;

        private RoleSchemaAsync(PostgresServerFixture server, PostgresTestSchema schema, string roleName, NpgsqlDataSource role)
        {
            _server = server;
            Schema = schema;
            _roleName = roleName;
            Role = role;
        }

        internal PostgresTestSchema Schema { get; }

        internal NpgsqlDataSource Role { get; }

        internal static async Task<RoleSchemaAsync> CreateAsync(PostgresServerFixture server, string grant, CancellationToken cancellationToken)
        {
            var schema = await PostgresTestSchema.CreateAsync(server.AdminConnectionString, cancellationToken).ConfigureAwait(false);
            var roleName = "r_" + Guid.NewGuid().ToString("n");
            var password = Guid.NewGuid().ToString("n");
            var quotedSchema = $"\"{schema.SchemaName}\"";
            var quotedRole = $"\"{roleName}\"";
            var source = schema.CreateDataSource();
            using (var setup = source.CreateCommand($"""
                CREATE ROLE {quotedRole} LOGIN PASSWORD '{password}' NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT;
                GRANT USAGE ON SCHEMA {quotedSchema} TO {quotedRole};
                """))
            {
                await setup.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            if (grant.Contains("DEFAULT PRIVILEGES", StringComparison.Ordinal))
            {
                using var defaults = source.CreateCommand(grant.Replace("{schema}", quotedSchema, StringComparison.Ordinal).Replace("{role}", quotedRole, StringComparison.Ordinal));
                await defaults.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await new MigrationRunner(source, schema.SchemaName).MigrateAsync(cancellationToken).ConfigureAwait(false);
            if (!grant.Contains("DEFAULT PRIVILEGES", StringComparison.Ordinal))
            {
                using var table = source.CreateCommand(grant.Replace("{schema}", quotedSchema, StringComparison.Ordinal).Replace("{role}", quotedRole, StringComparison.Ordinal));
                await table.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            var connection = new NpgsqlConnectionStringBuilder(schema.ConnectionString) { Username = roleName, Password = password };
            return new RoleSchemaAsync(server, schema, roleName, NpgsqlDataSource.Create(connection.ConnectionString));
        }

        public async ValueTask DisposeAsync()
        {
            await Role.DisposeAsync().ConfigureAwait(false);
            using (var revoke = Schema.CreateDataSource().CreateCommand($"ALTER DEFAULT PRIVILEGES IN SCHEMA \"{Schema.SchemaName}\" REVOKE SELECT, INSERT, UPDATE, DELETE ON TABLES FROM \"{_roleName}\""))
            {
                await revoke.ExecuteNonQueryAsync(CancellationToken.None).ConfigureAwait(false);
            }

            await Schema.DisposeAsync().ConfigureAwait(false);
            var admin = NpgsqlDataSource.Create(_server.AdminConnectionString);
            await using (admin.ConfigureAwait(false))
            {
                using var drop = admin.CreateCommand($"DROP ROLE IF EXISTS \"{_roleName}\"");
                await drop.ExecuteNonQueryAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }
    }
}
