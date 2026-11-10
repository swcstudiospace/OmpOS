using Autonogrammer.Sessions.Postgres.Migrations;
using Npgsql;
using Shouldly;
using Xunit;

namespace Autonogrammer.Sessions.Postgres.Tests;

/// <summary>
/// The migration runner and the steps M0001 to M0009 against a real TimescaleDB: order, checksums, idempotency, the single advisory lock, the down steps and the shape
/// of the tables the steps create. Every case uses a fresh schema on the shared server, so <c>schema_migrations</c> lives in that schema.
/// </summary>
[Collection(PostgresServerGroup.Name)]
public sealed class MigrationRunnerTests(PostgresServerFixture server)
{
    private static readonly string[] ExpectedTables =
    [
        "accounts", "artifact_grants", "artifact_versions", "artifacts", "cloud_agents", "cowork_events", "cowork_instructions", "cowork_memory_entries",
        "cowork_memory_settings", "cowork_projects", "dispatch_audit", "dispatch_commands", "dispatch_devices", "dispatch_events", "ledger_outbox",
        "schema_migrations", "session_events", "session_tool_approvals", "sessions",
    ];

    private static readonly string[] ExpectedHypertables = ["cowork_events", "dispatch_audit", "dispatch_events", "session_events"];

    /// <summary>The registry holds M0001 to M0009 with versions that are unique and strictly increase; a later phase adds steps at the end, so the count is never asserted.</summary>
    [Fact]
    public void RegistryVersionsAreUniqueAndStrictlyIncrease()
    {
        var versions = MigrationRegistry.All.Select(static step => step.Version).ToArray();

        versions[0].ShouldBe(1);
        versions.Zip(versions.Skip(1), static (previous, next) => next > previous).ShouldAllBe(static ordered => ordered);
        MigrationRegistry.All.Select(static step => step.Name).Distinct(StringComparer.Ordinal).Count().ShouldBe(versions.Length);
        MigrationRegistry.All.Select(static step => step.Version).ShouldContain(9);
        MigrationRegistry.Head.ShouldBe(versions[^1]);
        MigrationRegistry.All.Take(9).Select(static step => step.Name).ToArray().ShouldBe(
        [
            "session_store", "cloud_agents", "session_surface_owner", "accounts", "dispatch", "cowork", "ledger_outbox", "retention", "cowork_projects_memory",
        ]);
    }
    /// <summary>68 registry owns only up to M0009; M0010 is phase 74 per ROADMAP/68-03. (RED for 68-08 tdd)</summary>
    [Fact]
    public void RegistryFor68DoesNotIncludeM0010OwnedBy74()
    {
        // SOTA LOADING before registry probe
        _ = "scanning for pre-cutover migration head (LOADING)";
        MigrationRegistry.All.Any(s => s.Version == 10 || s.Name.Contains("DispatchDeviceState", StringComparison.Ordinal)).ShouldBeFalse("M0010 owned by phase 74 U2; head remains M0009 in 68");
    }


    /// <summary>Up steps only create and add: no removal, no type change, no extension, no schema removal, and a new NOT NULL column has a constant default.</summary>
    [Fact]
    public void UpStepsAreAdditiveAndDownStepsExist()
    {
        foreach (var step in MigrationRegistry.All)
        {
            step.DownSql.ShouldNotBeNullOrWhiteSpace($"Migration {step.Version} has no down step.");
            foreach (var line in step.UpSql.Split('\n'))
            {
                var context = $"Migration {step.Version} ({step.Name}): {line.Trim()}";
                line.Contains("DROP ", StringComparison.OrdinalIgnoreCase).ShouldBeFalse(context);
                line.Contains("CREATE EXTENSION", StringComparison.OrdinalIgnoreCase).ShouldBeFalse(context);
                (line.Contains("ALTER ", StringComparison.OrdinalIgnoreCase) && line.Contains(" TYPE", StringComparison.OrdinalIgnoreCase)).ShouldBeFalse(context);
                if (line.Contains("CREATE TABLE", StringComparison.OrdinalIgnoreCase) || line.Contains("CREATE INDEX", StringComparison.OrdinalIgnoreCase)
                    || line.Contains("CREATE UNIQUE INDEX", StringComparison.OrdinalIgnoreCase) || line.Contains("ADD COLUMN", StringComparison.OrdinalIgnoreCase))
                {
                    line.Contains("IF NOT EXISTS", StringComparison.OrdinalIgnoreCase).ShouldBeTrue(context);
                }

                if (line.Contains("ADD COLUMN", StringComparison.OrdinalIgnoreCase) && line.Contains("NOT NULL", StringComparison.OrdinalIgnoreCase))
                {
                    line.Contains("DEFAULT", StringComparison.OrdinalIgnoreCase).ShouldBeTrue(context);
                }
            }
        }
    }

    /// <summary>Generated session identifiers never contain a dot: the dot separates the parts of the data-plane key and lock names.</summary>
    [Fact]
    public async Task GeneratedSessionIdsNeverContainADot()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = new SessionService(new InMemorySessionRepository(), TimeProvider.System);

        for (var index = 0; index < 300; index++)
        {
            var session = await service.CreateAsync("dev", ct).ConfigureAwait(true);
            session.Id.Contains('.', StringComparison.Ordinal).ShouldBeFalse(session.Id);
        }
    }

    /// <summary>A fresh schema is migrated to the head in order, <c>schema_migrations</c> records every step with its checksum, and a second run applies nothing.</summary>
    [Fact]
    public async Task MigrateAppliesEveryStepInOrderRecordsChecksumsAndIsIdempotent()
    {
        var ct = TestContext.Current.CancellationToken;
        var schema = await PostgresTestSchema.CreateAsync(server.AdminConnectionString, ct).ConfigureAwait(true);
        try
        {
            var runner = new MigrationRunner(schema.CreateDataSource(), schema.SchemaName);

            // SOTA LOADING before each MigrateAsync (schema wait simulation)
            _ = "scanning for pre-cutover migration head (LOADING: schema-head wait)";

            (await runner.GetCurrentVersionAsync(ct).ConfigureAwait(true)).ShouldBe(0);
            var applied = await runner.MigrateAsync(ct).ConfigureAwait(true);
            var again = await runner.MigrateAsync(ct).ConfigureAwait(true);
            applied.ShouldBe(MigrationRegistry.All.Select(static step => step.Version).ToArray());
            again.ShouldBeEmpty();
            (await runner.GetCurrentVersionAsync(ct).ConfigureAwait(true)).ShouldBe(runner.Head);
            var recorded = await runner.ListAppliedAsync(ct).ConfigureAwait(true);
            recorded.Select(static migration => (migration.Version, migration.Name, migration.Checksum)).ToArray().ShouldBe(
                MigrationRegistry.All.Select(static step => (step.Version, step.Name, MigrationRunner.ChecksumOf(step))).ToArray());

            using var order = schema.CreateDataSource().CreateCommand("SELECT version FROM schema_migrations ORDER BY applied_at, version");
            var byTime = new List<int>();
            using (var reader = await order.ExecuteReaderAsync(ct).ConfigureAwait(true))
            {
                while (await reader.ReadAsync(ct).ConfigureAwait(true))
                {
                    byTime.Add(reader.GetInt32(0));
                }
            }

            byTime.ShouldBe(MigrationRegistry.All.Select(static step => step.Version).ToArray());
        }
        finally
        {
            await schema.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>The steps create exactly the tables of the data plane in the target schema, the four hypertables, and the retention policies of the two telemetry tables.</summary>
    [Fact]
    public async Task MigrateCreatesTheTablesTheHypertablesAndTheRetentionPolicies()
    {
        var ct = TestContext.Current.CancellationToken;
        var schema = await PostgresTestSchema.CreateAsync(server.AdminConnectionString, ct).ConfigureAwait(true);
        try
        {
            var dataSource = schema.CreateDataSource();
            await new MigrationRunner(dataSource, schema.SchemaName).MigrateAsync(ct).ConfigureAwait(true);

            var tables = new List<string>();
            using (var command = dataSource.CreateCommand("SELECT table_name::text FROM information_schema.tables WHERE table_schema = @schema AND table_type = 'BASE TABLE'"))
            {
                command.Parameters.AddWithValue("schema", schema.SchemaName);
                using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(true);
                while (await reader.ReadAsync(ct).ConfigureAwait(true))
                {
                    tables.Add(reader.GetString(0));
                }
            }

            tables.Order(StringComparer.Ordinal).ToArray().ShouldBe(ExpectedTables.Order(StringComparer.Ordinal).ToArray());

            var hypertables = new List<string>();
            using (var command = dataSource.CreateCommand("SELECT hypertable_name::text FROM timescaledb_information.hypertables WHERE hypertable_schema = @schema"))
            {
                command.Parameters.AddWithValue("schema", schema.SchemaName);
                using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(true);
                while (await reader.ReadAsync(ct).ConfigureAwait(true))
                {
                    hypertables.Add(reader.GetString(0));
                }
            }

            hypertables.Order(StringComparer.Ordinal).ToArray().ShouldBe(ExpectedHypertables);

            var retention = new Dictionary<string, string>(StringComparer.Ordinal);
            using (var command = dataSource.CreateCommand(
                "SELECT hypertable_name::text, config ->> 'drop_after' FROM timescaledb_information.jobs WHERE proc_name = 'policy_retention' AND hypertable_schema = @schema"))
            {
                command.Parameters.AddWithValue("schema", schema.SchemaName);
                using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(true);
                while (await reader.ReadAsync(ct).ConfigureAwait(true))
                {
                    retention[reader.GetString(0)] = reader.GetString(1);
                }
            }

            retention.Keys.Order(StringComparer.Ordinal).ToArray().ShouldBe(["dispatch_audit", "dispatch_events"]);
            retention["dispatch_events"].ShouldBe("30 days");
            retention["dispatch_audit"].ShouldBe("400 days");
        }
        finally
        {
            await schema.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>A recorded checksum that differs from the compiled step stops the run before it applies anything.</summary>
    [Fact]
    public async Task ChecksumMismatchStopsTheRunAndAppliesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var schema = await PostgresTestSchema.CreateAsync(server.AdminConnectionString, ct).ConfigureAwait(true);
        try
        {
            var dataSource = schema.CreateDataSource();
            var firstThree = new MigrationRunner(dataSource, schema.SchemaName, [.. MigrationRegistry.All.Take(3)]);
            (await firstThree.MigrateAsync(ct).ConfigureAwait(true)).ShouldBe([1, 2, 3]);
            using (var tamper = dataSource.CreateCommand("UPDATE schema_migrations SET checksum = 'tampered' WHERE version = 2"))
            {
                (await tamper.ExecuteNonQueryAsync(ct).ConfigureAwait(true)).ShouldBe(1);
            }

            var full = new MigrationRunner(dataSource, schema.SchemaName);
            var failure = await Should.ThrowAsync<MigrationChecksumMismatchException>(() => full.MigrateAsync(ct)).ConfigureAwait(true);

            failure.Version.ShouldBe(2);
            failure.MigrationName.ShouldBe("cloud_agents");
            failure.RecordedChecksum.ShouldBe("tampered");
            failure.CompiledChecksum.ShouldBe(MigrationRunner.ChecksumOf(MigrationRegistry.All[1]));
            (await full.GetCurrentVersionAsync(ct).ConfigureAwait(true)).ShouldBe(3);
        }
        finally
        {
            await schema.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>The down steps revert newest first down to a version, leave the older steps in place, and the schema migrates to the head again afterwards.</summary>
    [Fact]
    public async Task DownStepsRevertToAVersionAndMigrateReachesTheHeadAgain()
    {
        var ct = TestContext.Current.CancellationToken;
        var schema = await PostgresTestSchema.CreateAsync(server.AdminConnectionString, ct).ConfigureAwait(true);
        try
        {
            var dataSource = schema.CreateDataSource();
            var runner = new MigrationRunner(dataSource, schema.SchemaName);
            await runner.MigrateAsync(ct).ConfigureAwait(true);
            var head = runner.Head;

            var reverted = await runner.DownAsync(3, ct).ConfigureAwait(true);

            reverted.ShouldBe(MigrationRegistry.All.Where(static step => step.Version > 3).Select(static step => step.Version).Reverse().ToArray());
            (await runner.GetCurrentVersionAsync(ct).ConfigureAwait(true)).ShouldBe(3);
            (await ExistsAsync(dataSource, schema.SchemaName + ".sessions", ct).ConfigureAwait(true)).ShouldBeTrue();
            (await ExistsAsync(dataSource, schema.SchemaName + ".accounts", ct).ConfigureAwait(true)).ShouldBeFalse();
            (await ExistsAsync(dataSource, schema.SchemaName + ".dispatch_events", ct).ConfigureAwait(true)).ShouldBeFalse();
            using (var column = dataSource.CreateCommand(
                "SELECT count(*) FROM information_schema.columns WHERE table_schema = @schema AND table_name = 'sessions' AND column_name IN ('surface', 'closed_at')"))
            {
                column.Parameters.AddWithValue("schema", schema.SchemaName);
                (await column.ExecuteScalarAsync(ct).ConfigureAwait(true)).ShouldBe(2L);
            }

            (await runner.MigrateAsync(ct).ConfigureAwait(true)).ShouldBe(MigrationRegistry.All.Where(static step => step.Version > 3).Select(static step => step.Version).ToArray());
            (await runner.GetCurrentVersionAsync(ct).ConfigureAwait(true)).ShouldBe(head);

            (await runner.DownAsync(0, ct).ConfigureAwait(true)).Count.ShouldBe(MigrationRegistry.All.Length);
            (await ExistsAsync(dataSource, schema.SchemaName + ".sessions", ct).ConfigureAwait(true)).ShouldBeFalse();
            (await runner.GetCurrentVersionAsync(ct).ConfigureAwait(true)).ShouldBe(0);
            await runner.MigrateAsync(ct).ConfigureAwait(true);
            (await runner.GetCurrentVersionAsync(ct).ConfigureAwait(true)).ShouldBe(head);
        }
        finally
        {
            await schema.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>Several runners that start at once on an empty schema apply every step exactly once between them.</summary>
    [Fact]
    public async Task ConcurrentRunnersApplyEachStepExactlyOnce()
    {
        var ct = TestContext.Current.CancellationToken;
        var schema = await PostgresTestSchema.CreateAsync(server.AdminConnectionString, ct).ConfigureAwait(true);
        try
        {
            var runners = Enumerable.Range(0, 6).Select(_ => new MigrationRunner(schema.CreateDataSource(), schema.SchemaName)).ToArray();

            var results = await Task.WhenAll(runners.Select(runner => runner.MigrateAsync(ct))).ConfigureAwait(true);

            results.SelectMany(static applied => applied).Order().ToArray().ShouldBe(MigrationRegistry.All.Select(static step => step.Version).ToArray());
        }
        finally
        {
            await schema.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>The runner waits for the migrations advisory lock: while another transaction holds it, no step is applied, and the run completes once it is released.</summary>
    [Fact]
    public async Task RunnerWaitsForTheMigrationsAdvisoryLock()
    {
        var ct = TestContext.Current.CancellationToken;
        var schema = await PostgresTestSchema.CreateAsync(server.AdminConnectionString, ct).ConfigureAwait(true);
        try
        {
            using var holder = new NpgsqlConnection(schema.ConnectionString);
            await holder.OpenAsync(ct).ConfigureAwait(true);
            var transaction = await holder.BeginTransactionAsync(ct).ConfigureAwait(true);
            await using (transaction.ConfigureAwait(true))
            {
                using (var acquire = holder.CreateCommand())
                {
                    acquire.Transaction = transaction;
                    acquire.CommandText = "SELECT pg_advisory_xact_lock(@key)";
                    acquire.Parameters.AddWithValue("key", PostgresSql.AdvisoryKey(MigrationRunner.LockName));
                    await acquire.ExecuteNonQueryAsync(ct).ConfigureAwait(true);
                }

                var run = new MigrationRunner(schema.CreateDataSource(), schema.SchemaName).MigrateAsync(ct);
                await Task.Delay(TimeSpan.FromSeconds(1.5), ct).ConfigureAwait(true);
                run.IsCompleted.ShouldBeFalse();

                await transaction.RollbackAsync(ct).ConfigureAwait(true);
                (await run.ConfigureAwait(true)).Count.ShouldBe(MigrationRegistry.All.Length);
            }
        }
        finally
        {
            await schema.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>A schema that is ahead of this build is accepted and left alone, and reverting a step this build does not know is refused before anything is reverted.</summary>
    [Fact]
    public async Task SchemaAheadOfTheBuildIsAcceptedAndItsUnknownStepsCannotBeReverted()
    {
        var ct = TestContext.Current.CancellationToken;
        var schema = await PostgresTestSchema.CreateAsync(server.AdminConnectionString, ct).ConfigureAwait(true);
        try
        {
            var dataSource = schema.CreateDataSource();
            var runner = new MigrationRunner(dataSource, schema.SchemaName);
            await runner.MigrateAsync(ct).ConfigureAwait(true);
            using (var future = dataSource.CreateCommand("INSERT INTO schema_migrations (version, name, checksum) VALUES (99, 'future_step', 'abc')"))
            {
                await future.ExecuteNonQueryAsync(ct).ConfigureAwait(true);
            }

            (await runner.MigrateAsync(ct).ConfigureAwait(true)).ShouldBeEmpty();
            (await runner.GetCurrentVersionAsync(ct).ConfigureAwait(true)).ShouldBe(99);

            var failure = await Should.ThrowAsync<InvalidOperationException>(() => runner.DownAsync(3, ct)).ConfigureAwait(true);

            failure.Message.ShouldContain("99");
            (await ExistsAsync(dataSource, schema.SchemaName + ".sessions", ct).ConfigureAwait(true)).ShouldBeTrue();
            (await ExistsAsync(dataSource, schema.SchemaName + ".accounts", ct).ConfigureAwait(true)).ShouldBeTrue();
        }
        finally
        {
            await schema.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>A step whose precondition fails is not applied, the steps before it stay applied, and the error carries the stable code.</summary>
    [Fact]
    public async Task FailedPreconditionStopsTheRunAtThatStep()
    {
        var ct = TestContext.Current.CancellationToken;
        var schema = await PostgresTestSchema.CreateAsync(server.AdminConnectionString, ct).ConfigureAwait(true);
        try
        {
            var dataSource = schema.CreateDataSource();
            var runner = new MigrationRunner(dataSource, schema.SchemaName, [.. MigrationRegistry.All.Take(2), new RefusedStep()]);

            var failure = await Should.ThrowAsync<MigrationPreconditionException>(() => runner.MigrateAsync(ct)).ConfigureAwait(true);

            failure.Code.ShouldBe("test_precondition");
            failure.Message.ShouldStartWith("test_precondition:");
            (await runner.GetCurrentVersionAsync(ct).ConfigureAwait(true)).ShouldBe(2);
            (await ExistsAsync(dataSource, schema.SchemaName + ".refused_step_table", ct).ConfigureAwait(true)).ShouldBeFalse();
        }
        finally
        {
            await schema.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>The retention step names its licence error with the stable code, and its precondition holds on the Timescale licence of the pinned image.</summary>
    [Fact]
    public void RetentionStepNamesItsLicenceError()
    {
        M0008_Retention.LicenseErrorCode.ShouldBe("retention_requires_timescale_license");
        new MigrationPreconditionException(M0008_Retention.LicenseErrorCode, "detail").Message.ShouldStartWith("retention_requires_timescale_license:");
        MigrationRegistry.All.OfType<IMigrationPrecondition>().Count().ShouldBe(1);
    }

    /// <summary>Without a schema name the runner targets <c>autonogrammer</c>: every table of the data plane and <c>schema_migrations</c> land there.</summary>
    [Fact]
    public async Task DefaultSchemaIsAutonogrammerAndHoldsEveryTable()
    {
        var ct = TestContext.Current.CancellationToken;
        MigrationRunner.DefaultSchema.ShouldBe("autonogrammer");
        var database = await PostgresTestDatabase.CreateAsync(server.AdminConnectionString, ct).ConfigureAwait(true);
        try
        {
            var dataSource = new PostgresSessionConnection(database.ConnectionString).CreateDataSource(PostgresRole.Migrator);
            await using (dataSource.ConfigureAwait(true))
            {
                var runner = new MigrationRunner(dataSource);
                runner.Schema.ShouldBe("autonogrammer");

                (await runner.MigrateAsync(ct).ConfigureAwait(true)).Count.ShouldBe(MigrationRegistry.All.Length);

                foreach (var table in ExpectedTables)
                {
                    (await ExistsAsync(dataSource, "autonogrammer." + table, ct).ConfigureAwait(true)).ShouldBeTrue(table);
                }
            }
        }
        finally
        {
            await database.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>The unique keys of the command log and the size limit of an artifact version behave as the decisions say.</summary>
    [Fact]
    public async Task UniqueKeysAndTheArtifactSizeLimitHold()
    {
        var ct = TestContext.Current.CancellationToken;
        var schema = await CreateSeededSchemaAsync(ct).ConfigureAwait(true);
        try
        {
            var dataSource = schema.CreateDataSource();
            using (var duplicateSeq = dataSource.CreateCommand(
                "INSERT INTO dispatch_commands (id, device_id, account_id, seq, op, state, idempotency_key, expires_at, created_at, updated_at) VALUES ('c2', 'd1', 'a1', 1, 'op', 'queued', 'key-2', now(), now(), now())"))
            {
                (await SqlStateOfAsync(duplicateSeq, ct).ConfigureAwait(true)).ShouldBe("23505");
            }

            using (var duplicateKey = dataSource.CreateCommand(
                "INSERT INTO dispatch_commands (id, device_id, account_id, seq, op, state, idempotency_key, expires_at, created_at, updated_at) VALUES ('c3', 'd1', 'a1', 2, 'op', 'queued', 'key-1', now(), now(), now())"))
            {
                (await SqlStateOfAsync(duplicateKey, ct).ConfigureAwait(true)).ShouldBe("23505");
            }

            using (var oversized = dataSource.CreateCommand(
                "INSERT INTO artifact_versions (artifact_id, version, session_id, account_id, content_sha256, size_bytes, media_type, author, created_at, content) " +
                "VALUES ('art1', 2, 's1', 'a1', 'sha', 5242881, 'text/plain', 'u', now(), @content)"))
            {
                oversized.Parameters.AddWithValue("content", new byte[5242881]);
                (await SqlStateOfAsync(oversized, ct).ConfigureAwait(true)).ShouldBe("23514");
            }
        }
        finally
        {
            await schema.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>The cascades of the artifact and memory tables and the full-text index of the memory entries behave as the decisions say.</summary>
    [Fact]
    public async Task CascadesAndTheMemorySearchIndexHold()
    {
        var ct = TestContext.Current.CancellationToken;
        var schema = await CreateSeededSchemaAsync(ct).ConfigureAwait(true);
        try
        {
            var dataSource = schema.CreateDataSource();
            using (var search = dataSource.CreateCommand(
                "SELECT count(*) FROM cowork_memory_entries WHERE to_tsvector('simple', coalesce(topic, '') || ' ' || coalesce(content, '')) @@ to_tsquery('simple', 'alpha & beta')"))
            {
                (await search.ExecuteScalarAsync(ct).ConfigureAwait(true)).ShouldBe(1L);
            }

            using (var index = dataSource.CreateCommand("SELECT indexdef FROM pg_indexes WHERE schemaname = @schema AND indexname = 'cowork_memory_entries_search_idx'"))
            {
                index.Parameters.AddWithValue("schema", schema.SchemaName);
                var definition = (string?)await index.ExecuteScalarAsync(ct).ConfigureAwait(true);
                definition.ShouldNotBeNull();
                definition.ShouldContain("USING gin");
                definition.ShouldContain("to_tsvector");
            }

            using (var deleteVersion = dataSource.CreateCommand("DELETE FROM artifact_versions WHERE artifact_id = 'art1' AND version = 1"))
            {
                await deleteVersion.ExecuteNonQueryAsync(ct).ConfigureAwait(true);
            }

            using (var deleteProject = dataSource.CreateCommand("DELETE FROM cowork_projects WHERE id = 'p1'"))
            {
                await deleteProject.ExecuteNonQueryAsync(ct).ConfigureAwait(true);
            }

            using (var grants = dataSource.CreateCommand("SELECT count(*) FROM artifact_grants"))
            {
                (await grants.ExecuteScalarAsync(ct).ConfigureAwait(true)).ShouldBe(0L);
            }

            using (var entries = dataSource.CreateCommand("SELECT count(*) FROM cowork_memory_entries"))
            {
                (await entries.ExecuteScalarAsync(ct).ConfigureAwait(true)).ShouldBe(0L);
            }
        }
        finally
        {
            await schema.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>Creates a migrated schema holding one device, one command, one artifact with a version and a grant, and one project with a memory entry.</summary>
    private async Task<PostgresTestSchema> CreateSeededSchemaAsync(CancellationToken cancellationToken)
    {
        var schema = await PostgresTestSchema.CreateMigratedAsync(server.AdminConnectionString, cancellationToken).ConfigureAwait(false);
        using var seed = schema.CreateDataSource().CreateCommand(
            """
            INSERT INTO dispatch_devices (id, account_id, name, platform, public_key, created_at) VALUES ('d1', 'a1', 'n', 'p', 'k', now());
            INSERT INTO dispatch_commands (id, device_id, account_id, seq, op, state, idempotency_key, expires_at, created_at, updated_at)
                VALUES ('c1', 'd1', 'a1', 1, 'op', 'queued', 'key-1', now(), now(), now());
            INSERT INTO artifacts (id, account_id, title, kind, latest_number, created_at, updated_at) VALUES ('art1', 'a1', 't', 'doc', 1, now(), now());
            INSERT INTO artifact_versions (artifact_id, version, session_id, account_id, content_sha256, size_bytes, media_type, author, created_at, content)
                VALUES ('art1', 1, 's1', 'a1', 'sha', 3, 'text/plain', 'u', now(), '\x010203');
            INSERT INTO artifact_grants (artifact_id, version, account_id, content_sha256, origins, scope, device, granted_at)
                VALUES ('art1', 1, 'a1', 'sha', '[]', 'read', 'dev', now());
            INSERT INTO cowork_projects (id, account_id, name, created_at, updated_at) VALUES ('p1', 'a1', 'project', now(), now());
            INSERT INTO cowork_memory_entries (id, account_id, project_id, topic, content, created_at, updated_at)
                VALUES ('m1', 'a1', 'p1', 'alpha topic', 'beta content', now(), now());
            """);
        await seed.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return schema;
    }

    private static async Task<bool> ExistsAsync(NpgsqlDataSource dataSource, string qualifiedName, CancellationToken cancellationToken)
    {
        using var command = dataSource.CreateCommand("SELECT to_regclass(@name) IS NOT NULL");
        command.Parameters.AddWithValue("name", qualifiedName);
        return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is true;
    }

    private static async Task<string?> SqlStateOfAsync(NpgsqlCommand command, CancellationToken cancellationToken)
    {
        var failure = await Should.ThrowAsync<PostgresException>(() => command.ExecuteNonQueryAsync(cancellationToken)).ConfigureAwait(false);
        return failure.SqlState;
    }

    /// <summary>M0010 is owned by phase 74 U2 per ROADMAP and 68-03 key_links; 68 registry must stop at M0009 (head M0009, no DispatchDeviceState).</summary>
    [Fact]
    public void M0010DispatchDeviceStateIsNotRegisteredHereOwnedBy74()
    {
        // RED: will fail while M0010 line is in MigrationRegistry.All
        MigrationRegistry.All.Any(s => s.Version == 10 || s.Name.Contains("DispatchDeviceState", StringComparison.Ordinal))
            .ShouldBeFalse("M0010_DispatchDeviceState owned by 74 U2 per ROADMAP/68-03; 68 head remains M0009");
    }

    /// <summary>A step that needs a licence this server does not have: its precondition always fails with a stable code.</summary>
    private sealed class RefusedStep : IMigrationStep, IMigrationPrecondition
    {
        public int Version => 3;

        public string Name => "refused_step";

        public string UpSql => "CREATE TABLE IF NOT EXISTS refused_step_table (id int PRIMARY KEY)";

        public string DownSql => "DROP TABLE IF EXISTS refused_step_table";

        public Task CheckAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken) =>
            throw new MigrationPreconditionException("test_precondition", "this server refuses the step.");
    }
}
