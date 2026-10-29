using System.Security.Cryptography;
using Autonogrammer.Contracts.Sessions;
using Autonogrammer.Sessions.Postgres.Migrations;
using Npgsql;
using Shouldly;
using Xunit;
using static Autonogrammer.Sessions.Postgres.Tests.SessionSamples;

namespace Autonogrammer.Sessions.Postgres.Tests;

/// <summary>
/// <c>deploy/data-plane/bootstrap.sql</c> applied with <c>psql</c> inside the throwaway server container, on a database of its own, with random secrets that reach the
/// script only through the environment (AG-68-5). The runtime role has data manipulation only: it cannot run DDL, cannot update or delete an audit row, and cannot
/// read a table in <c>public</c>; the support role reads and cannot write; the database admits only the three roles. The cluster's roles are global, so the test removes
/// them again.
/// </summary>
[Collection(PostgresServerGroup.Name)]
public sealed class PostgresRoleTests(PostgresServerFixture server)
{
    private const string ScriptInContainer = "/tmp/autonogrammer-bootstrap-test.sql";

    private const string Outsider = "autonogrammer_role_test_outsider";

    private const string InsufficientPrivilege = "42501";

    /// <summary>The bootstrap is idempotent, the migrator applies every step under it, and the runtime role can do exactly what the decisions allow and nothing else.</summary>
    [Fact]
    public async Task RuntimeRoleHasDataManipulationOnlyAndTheAuditTableIsAppendOnly()
    {
        var ct = TestContext.Current.CancellationToken;
        var databaseName = "t_" + Guid.NewGuid().ToString("N");
        var secrets = new RoleSecrets();
        var database = PostgresTestDatabase.ForExisting(server.AdminConnectionString, databaseName);
        try
        {
            await CopyScriptAsync(ct).ConfigureAwait(true);
            await BootstrapAsync(databaseName, secrets, ct).ConfigureAwait(true);
            await BootstrapAsync(databaseName, secrets, ct).ConfigureAwait(true);

            var migratorConnection = new PostgresSessionConnection(RoleConnectionString("autonogrammer_migrator", secrets.Migrator, databaseName));
            var migratorSource = migratorConnection.CreateDataSource(PostgresRole.Migrator);
            await using (migratorSource.ConfigureAwait(true))
            {
                var applied = await new MigrationRunner(migratorSource).MigrateAsync(ct).ConfigureAwait(true);
                applied.Count.ShouldBe(MigrationRegistry.All.Length);
            }

            // A run after the migrations re-grants the existing tables and keeps the audit table append-only.
            await BootstrapAsync(databaseName, secrets, ct).ConfigureAwait(true);

            var adminSource = NpgsqlDataSource.Create(new NpgsqlConnectionStringBuilder(server.AdminConnectionString) { Database = databaseName }.ConnectionString);
            await using (adminSource.ConfigureAwait(true))
            {
                await AssertCatalogAsync(adminSource, databaseName, ct).ConfigureAwait(true);
                await AssertRuntimeRoleAsync(RoleConnectionString("autonogrammer_app", secrets.App, databaseName), ct).ConfigureAwait(true);
                await AssertSupportRoleAsync(RoleConnectionString("autonogrammer_ro", secrets.ReadOnly, databaseName), ct).ConfigureAwait(true);
            }
        }
        finally
        {
            await database.DisposeAsync().ConfigureAwait(true);
            await DropRolesAsync(ct).ConfigureAwait(true);
        }
    }

    /// <summary>The bootstrap refuses to run without a mandatory secret: it stops with a named error and creates no role.</summary>
    [Fact]
    public async Task BootstrapFailsWithoutAMandatorySecretAndCreatesNoRole()
    {
        var ct = TestContext.Current.CancellationToken;
        var databaseName = "t_" + Guid.NewGuid().ToString("N");
        var database = PostgresTestDatabase.ForExisting(server.AdminConnectionString, databaseName);
        try
        {
            await CopyScriptAsync(ct).ConfigureAwait(true);
            var run = await RunBootstrapAsync(databaseName, new RoleSecrets { Migrator = string.Empty, App = string.Empty, ReadOnly = string.Empty }, ct).ConfigureAwait(true);

            run.ExitCode.ShouldNotBe(0);
            run.StandardError.ShouldContain("secret is missing");
            var admin = NpgsqlDataSource.Create(server.AdminConnectionString);
            await using (admin.ConfigureAwait(true))
            {
                using var roles = admin.CreateCommand("SELECT count(*) FROM pg_roles WHERE rolname LIKE 'autonogrammer\\_%'");
                (await roles.ExecuteScalarAsync(ct).ConfigureAwait(true)).ShouldBe(0L);
            }
        }
        finally
        {
            await database.DisposeAsync().ConfigureAwait(true);
            await DropRolesAsync(ct).ConfigureAwait(true);
        }
    }

    private static string ScriptOnHost()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "deploy", "data-plane", "bootstrap.sql");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("deploy/data-plane/bootstrap.sql was not found above the test assembly.");
    }

    private async Task CopyScriptAsync(CancellationToken cancellationToken)
    {
        var copy = await PodmanCommand.RunAsync(["cp", ScriptOnHost(), $"{server.ContainerName}:{ScriptInContainer}"], null, cancellationToken).ConfigureAwait(false);
        copy.ExitCode.ShouldBe(0, $"podman cp of the bootstrap script into '{server.ContainerName}' failed: {copy.StandardError}");
    }

    private async Task BootstrapAsync(string databaseName, RoleSecrets secrets, CancellationToken cancellationToken)
    {
        var run = await RunBootstrapAsync(databaseName, secrets, cancellationToken).ConfigureAwait(false);
        run.ExitCode.ShouldBe(0, $"bootstrap.sql failed.\nSTDERR:\n{run.StandardError}");
    }

    private async Task<PodmanResult> RunBootstrapAsync(string databaseName, RoleSecrets secrets, CancellationToken cancellationToken)
    {
        // The secrets reach podman through its environment (-e NAME without a value), never through the argument list, and reach psql through \getenv.
        var environment = new Dictionary<string, string?>(StringComparer.Ordinal);
        var arguments = new List<string> { "exec" };
        foreach (var (name, value) in new[]
        {
            ("AUTONOGRAMMER_MIGRATOR_PASSWORD", secrets.Migrator),
            ("AUTONOGRAMMER_APP_PASSWORD", secrets.App),
            ("AUTONOGRAMMER_RO_PASSWORD", secrets.ReadOnly),
        })
        {
            if (value.Length > 0)
            {
                environment[name] = value;
                arguments.Add("-e");
                arguments.Add(name);
            }
        }

        arguments.AddRange(
        [
            server.ContainerName,
            "psql", "-X", "-q",
            "-v", "ON_ERROR_STOP=1",
            "-v", $"db={databaseName}",
            "-U", new NpgsqlConnectionStringBuilder(server.AdminConnectionString).Username!,
            "-d", "postgres",
            "-f", ScriptInContainer,
        ]);
        return await PodmanCommand.RunAsync(arguments, environment, cancellationToken).ConfigureAwait(false);
    }

    private string RoleConnectionString(string role, string secret, string databaseName) =>
        new NpgsqlConnectionStringBuilder(server.AdminConnectionString) { Username = role, Password = secret, Database = databaseName }.ConnectionString;

    private async Task DropRolesAsync(CancellationToken cancellationToken)
    {
        var admin = NpgsqlDataSource.Create(server.AdminConnectionString);
        await using (admin.ConfigureAwait(false))
        {
            await DropRoleStatementsAsync(admin, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task DropRoleStatementsAsync(NpgsqlDataSource admin, CancellationToken cancellationToken)
    {
        using (var outsider = admin.CreateCommand("DROP ROLE IF EXISTS autonogrammer_role_test_outsider"))
        {
            await outsider.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        using (var readOnly = admin.CreateCommand("DROP ROLE IF EXISTS autonogrammer_ro"))
        {
            await readOnly.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        using (var runtime = admin.CreateCommand("DROP ROLE IF EXISTS autonogrammer_app"))
        {
            await runtime.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        using (var migrator = admin.CreateCommand("DROP ROLE IF EXISTS autonogrammer_migrator"))
        {
            await migrator.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        using (var owner = admin.CreateCommand("DROP ROLE IF EXISTS autonogrammer_owner"))
        {
            await owner.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task AssertCatalogAsync(NpgsqlDataSource adminSource, string databaseName, CancellationToken cancellationToken)
    {
        await PrepareCatalogAsync(adminSource, cancellationToken).ConfigureAwait(false);
        await AssertOwnershipAsync(adminSource, cancellationToken).ConfigureAwait(false);
        await AssertPrivilegesAsync(adminSource, databaseName, cancellationToken).ConfigureAwait(false);
    }

    private static async Task PrepareCatalogAsync(NpgsqlDataSource adminSource, CancellationToken cancellationToken)
    {
        using (var outsider = adminSource.CreateCommand("CREATE ROLE autonogrammer_role_test_outsider NOLOGIN"))
        {
            await outsider.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        using (var secret = adminSource.CreateCommand("CREATE TABLE public.role_test_secret (value int)"))
        {
            await secret.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task AssertOwnershipAsync(NpgsqlDataSource adminSource, CancellationToken cancellationToken)
    {
        // The extension is in public, the schema belongs to the owner role, the migrator owns the tables, and the owner role cannot log in.
        using (var extension = adminSource.CreateCommand("SELECT extnamespace::regnamespace::text FROM pg_extension WHERE extname = 'timescaledb'"))
        {
            (await extension.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)).ShouldBe("public");
        }

        using (var schemaOwner = adminSource.CreateCommand("SELECT nspowner::regrole::text FROM pg_namespace WHERE nspname = 'autonogrammer'"))
        {
            (await schemaOwner.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)).ShouldBe("autonogrammer_owner");
        }

        using (var tableOwner = adminSource.CreateCommand("SELECT tableowner::text FROM pg_tables WHERE schemaname = 'autonogrammer' AND tablename = 'sessions'"))
        {
            (await tableOwner.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)).ShouldBe("autonogrammer_migrator");
        }

        using (var ownerLogin = adminSource.CreateCommand("SELECT rolcanlogin FROM pg_roles WHERE rolname = 'autonogrammer_owner'"))
        {
            (await ownerLogin.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)).ShouldBe(false);
        }

        using (var appAttributes = adminSource.CreateCommand("SELECT rolcanlogin AND NOT rolsuper AND NOT rolcreatedb AND NOT rolcreaterole FROM pg_roles WHERE rolname = 'autonogrammer_app'"))
        {
            (await appAttributes.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)).ShouldBe(true);
        }
    }

    private static async Task AssertPrivilegesAsync(NpgsqlDataSource adminSource, string databaseName, CancellationToken cancellationToken)
    {
        // Connect: the three roles may, a role outside the bootstrap may not, and the roles can still connect to a database that grants PUBLIC.
        foreach (var role in new[] { "autonogrammer_migrator", "autonogrammer_app", "autonogrammer_ro" })
        {
            (await HasDatabasePrivilegeAsync(adminSource, role, databaseName, "CONNECT", cancellationToken).ConfigureAwait(false)).ShouldBeTrue(role);
        }

        (await HasDatabasePrivilegeAsync(adminSource, Outsider, databaseName, "CONNECT", cancellationToken).ConfigureAwait(false)).ShouldBeFalse();
        (await HasDatabasePrivilegeAsync(adminSource, "autonogrammer_app", "postgres", "CONNECT", cancellationToken).ConfigureAwait(false)).ShouldBeTrue();

        // Object privileges: none where PUBLIC grants the connection, and DML only inside the schema.
        (await HasTablePrivilegeAsync(adminSource, "autonogrammer_app", "public.role_test_secret", "SELECT", cancellationToken).ConfigureAwait(false)).ShouldBeFalse();
        (await HasSchemaPrivilegeAsync(adminSource, "autonogrammer_app", "autonogrammer", "USAGE", cancellationToken).ConfigureAwait(false)).ShouldBeTrue();
        (await HasSchemaPrivilegeAsync(adminSource, "autonogrammer_app", "autonogrammer", "CREATE", cancellationToken).ConfigureAwait(false)).ShouldBeFalse();
        (await HasSchemaPrivilegeAsync(adminSource, "autonogrammer_app", "public", "CREATE", cancellationToken).ConfigureAwait(false)).ShouldBeFalse();
        foreach (var privilege in new[] { "SELECT", "INSERT", "UPDATE", "DELETE" })
        {
            (await HasTablePrivilegeAsync(adminSource, "autonogrammer_app", "autonogrammer.sessions", privilege, cancellationToken).ConfigureAwait(false)).ShouldBeTrue(privilege);
        }

        (await HasTablePrivilegeAsync(adminSource, "autonogrammer_app", "autonogrammer.sessions", "TRUNCATE", cancellationToken).ConfigureAwait(false)).ShouldBeFalse();
        (await HasTablePrivilegeAsync(adminSource, "autonogrammer_app", "autonogrammer.dispatch_audit", "INSERT", cancellationToken).ConfigureAwait(false)).ShouldBeTrue();
        (await HasTablePrivilegeAsync(adminSource, "autonogrammer_app", "autonogrammer.dispatch_audit", "SELECT", cancellationToken).ConfigureAwait(false)).ShouldBeTrue();
        foreach (var privilege in new[] { "UPDATE", "DELETE", "TRUNCATE" })
        {
            (await HasTablePrivilegeAsync(adminSource, "autonogrammer_app", "autonogrammer.dispatch_audit", privilege, cancellationToken).ConfigureAwait(false)).ShouldBeFalse(privilege);
        }

        (await HasTablePrivilegeAsync(adminSource, "autonogrammer_ro", "autonogrammer.sessions", "SELECT", cancellationToken).ConfigureAwait(false)).ShouldBeTrue();
        (await HasTablePrivilegeAsync(adminSource, "autonogrammer_ro", "autonogrammer.sessions", "INSERT", cancellationToken).ConfigureAwait(false)).ShouldBeFalse();
    }

    private static async Task AssertRuntimeRoleAsync(string appConnectionString, CancellationToken cancellationToken)
    {
        var connection = new PostgresSessionConnection(appConnectionString);
        var appSource = connection.CreateDataSource(PostgresRole.Runtime);
        await using (appSource.ConfigureAwait(false))
        {
            await AssertDataManipulationAsync(new PostgresSessionRepository(appSource, connection), cancellationToken).ConfigureAwait(false);
            await AssertAuditIsAppendOnlyAsync(appSource, cancellationToken).ConfigureAwait(false);
            await AssertNoDdlAsync(appSource, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task AssertDataManipulationAsync(PostgresSessionRepository repository, CancellationToken cancellationToken)
    {
        // Data manipulation works through the stores themselves, hypertables included.
        await repository.SaveAsync(Sample("role-1") with { Surface = SessionSurface.Cowork }, cancellationToken).ConfigureAwait(false);
        await AppendDeltasAsync(repository, "role-1", 0, 2, cancellationToken).ConfigureAwait(false);
        (await repository.GetAsync("role-1", cancellationToken).ConfigureAwait(false))!.Events.Count.ShouldBe(2);
        (await repository.ListByOwnerAsync("dev", SessionSurface.Cowork, false, 10, null, cancellationToken).ConfigureAwait(false)).Count.ShouldBe(1);
        (await repository.SetClosedAsync("role-1", true, cancellationToken).ConfigureAwait(false)).ShouldBeTrue();
    }

    private static async Task AssertAuditIsAppendOnlyAsync(NpgsqlDataSource appSource, CancellationToken cancellationToken)
    {
        // The audit table takes inserts and reads, and nothing else.
        using (var insert = appSource.CreateCommand("INSERT INTO dispatch_audit (account_id_hash, occurred_at, op, outcome) VALUES ('hash-1', now(), 'pair', 'ok')"))
        {
            (await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false)).ShouldBe(1);
        }

        using (var read = appSource.CreateCommand("SELECT count(*) FROM dispatch_audit WHERE account_id_hash = 'hash-1'"))
        {
            (await read.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)).ShouldBe(1L);
        }

        using (var update = appSource.CreateCommand("UPDATE dispatch_audit SET outcome = 'tampered' WHERE account_id_hash = 'hash-1'"))
        {
            (await SqlStateAsync(update, cancellationToken).ConfigureAwait(false)).ShouldBe(InsufficientPrivilege);
        }

        using (var delete = appSource.CreateCommand("DELETE FROM dispatch_audit WHERE account_id_hash = 'hash-1'"))
        {
            (await SqlStateAsync(delete, cancellationToken).ConfigureAwait(false)).ShouldBe(InsufficientPrivilege);
        }
    }

    private static async Task AssertNoDdlAsync(NpgsqlDataSource appSource, CancellationToken cancellationToken)
    {
        // No DDL anywhere: not in the schema, not in public, not a new schema, not on an existing table, and no TRUNCATE.
        using (var createInSchema = appSource.CreateCommand("CREATE TABLE autonogrammer.evil (x int)"))
        {
            (await SqlStateAsync(createInSchema, cancellationToken).ConfigureAwait(false)).ShouldBe(InsufficientPrivilege);
        }

        using (var createInPublic = appSource.CreateCommand("CREATE TABLE public.evil (x int)"))
        {
            (await SqlStateAsync(createInPublic, cancellationToken).ConfigureAwait(false)).ShouldBe(InsufficientPrivilege);
        }

        using (var createSchema = appSource.CreateCommand("CREATE SCHEMA evil_schema"))
        {
            (await SqlStateAsync(createSchema, cancellationToken).ConfigureAwait(false)).ShouldBe(InsufficientPrivilege);
        }

        using (var alter = appSource.CreateCommand("ALTER TABLE autonogrammer.sessions ADD COLUMN evil int"))
        {
            (await SqlStateAsync(alter, cancellationToken).ConfigureAwait(false)).ShouldBe(InsufficientPrivilege);
        }

        using (var dropTable = appSource.CreateCommand("DROP TABLE autonogrammer.sessions"))
        {
            (await SqlStateAsync(dropTable, cancellationToken).ConfigureAwait(false)).ShouldBe(InsufficientPrivilege);
        }

        using (var truncate = appSource.CreateCommand("TRUNCATE autonogrammer.sessions"))
        {
            (await SqlStateAsync(truncate, cancellationToken).ConfigureAwait(false)).ShouldBe(InsufficientPrivilege);
        }

        // A table in public is outside the role's reach even though the role may connect to the database.
        using (var crossSchema = appSource.CreateCommand("SELECT value FROM public.role_test_secret"))
        {
            (await SqlStateAsync(crossSchema, cancellationToken).ConfigureAwait(false)).ShouldBe(InsufficientPrivilege);
        }
    }

    private static async Task AssertSupportRoleAsync(string roConnectionString, CancellationToken cancellationToken)
    {
        var roSource = new PostgresSessionConnection(roConnectionString).CreateDataSource(PostgresRole.ReadOnly);
        await using (roSource.ConfigureAwait(false))
        {
            using (var read = roSource.CreateCommand("SELECT count(*) FROM sessions"))
            {
                (await read.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)).ShouldBe(1L);
            }

            using (var write = roSource.CreateCommand("DELETE FROM sessions"))
            {
                (await SqlStateAsync(write, cancellationToken).ConfigureAwait(false)).ShouldBe(InsufficientPrivilege);
            }
        }
    }

    private static async Task<string?> SqlStateAsync(NpgsqlCommand command, CancellationToken cancellationToken)
    {
        var failure = await Should.ThrowAsync<PostgresException>(() => command.ExecuteNonQueryAsync(cancellationToken)).ConfigureAwait(false);
        return failure.SqlState;
    }

    private static async Task<bool> HasDatabasePrivilegeAsync(NpgsqlDataSource dataSource, string role, string database, string privilege, CancellationToken cancellationToken)
    {
        using var command = dataSource.CreateCommand("SELECT has_database_privilege(@role, @object, @privilege)");
        command.Parameters.AddWithValue("role", role);
        command.Parameters.AddWithValue("object", database);
        command.Parameters.AddWithValue("privilege", privilege);
        return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is true;
    }

    private static async Task<bool> HasSchemaPrivilegeAsync(NpgsqlDataSource dataSource, string role, string schema, string privilege, CancellationToken cancellationToken)
    {
        using var command = dataSource.CreateCommand("SELECT has_schema_privilege(@role, @object, @privilege)");
        command.Parameters.AddWithValue("role", role);
        command.Parameters.AddWithValue("object", schema);
        command.Parameters.AddWithValue("privilege", privilege);
        return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is true;
    }

    private static async Task<bool> HasTablePrivilegeAsync(NpgsqlDataSource dataSource, string role, string table, string privilege, CancellationToken cancellationToken)
    {
        using var command = dataSource.CreateCommand("SELECT has_table_privilege(@role, @object, @privilege)");
        command.Parameters.AddWithValue("role", role);
        command.Parameters.AddWithValue("object", table);
        command.Parameters.AddWithValue("privilege", privilege);
        return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is true;
    }

    /// <summary>Three random secrets, one per role, generated for this test only.</summary>
    private sealed class RoleSecrets
    {
        public string Migrator { get; init; } = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));

        public string App { get; init; } = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));

        public string ReadOnly { get; init; } = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
    }
}
