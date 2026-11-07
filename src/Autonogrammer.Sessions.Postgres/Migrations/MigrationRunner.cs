using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using Autonogrammer.Resilience;
using Npgsql;

namespace Autonogrammer.Sessions.Postgres.Migrations;

/// <summary>
/// Applies and reverts the numbered migration steps of <see cref="MigrationRegistry"/> to one schema. The runner is hand-written: it needs no package and its rules
/// are the ones of ADR-0031 D5.
/// </summary>
/// <remarks>
/// <para>
/// Every step runs in its own transaction under the transaction-scoped advisory lock <see cref="LockName"/>, so two runners (the one-shot migrate service and a
/// development host with <c>AutoMigrate</c>, say) serialize and a step is applied once. The transaction puts the target schema first in its search path, so a step
/// names tables without a schema and the same step runs in <c>autonogrammer</c> in production and in a <c>t_&lt;guid&gt;</c> schema in a test.
/// </para>
/// <para>
/// <c>schema_migrations</c> records the version, name, checksum, time and role of every applied step. The checksum is the SHA-256 of the step's up SQL; a recorded
/// checksum that differs from the compiled step stops the run with <see cref="MigrationChecksumMismatchException"/> before anything is applied. A schema that is
/// ahead of this build (it has steps this build does not know) is accepted and left alone.
/// </para>
/// <para>
/// Every database command runs through the PostgreSQL <see cref="ResiliencePipeline"/> as a <see cref="OperationClass.Background"/> call. A step is safe to retry:
/// the applied check and the apply are one transaction under the lock, so a repeated step finds its row and does nothing.
/// </para>
/// </remarks>
public sealed class MigrationRunner
{
    /// <summary>The schema the runner targets unless told otherwise.</summary>
    public const string DefaultSchema = "autonogrammer";

    /// <summary>The advisory lock every migration transaction takes: the one lock string of the schema.</summary>
    public const string LockName = "autonogrammer.schema.migrations";

    private const int MaxSchemaNameLength = 63;

    private const string LockSql = "SELECT pg_advisory_xact_lock(@key)";

    // The search path and the schema name for the DO block are transaction-local settings; the schema name never reaches the SQL text.
    private const string PrepareSql =
        "SELECT set_config('search_path', quote_ident(@schema) || ', public', true), set_config('autonogrammer.migration_schema', @schema, true)";

    private const string EnsureSchemaSql = """
        DO $ensure$
        BEGIN
            IF NOT EXISTS (SELECT 1 FROM pg_namespace WHERE nspname = current_setting('autonogrammer.migration_schema')) THEN
                EXECUTE format('CREATE SCHEMA %I', current_setting('autonogrammer.migration_schema'));
            END IF;
        END
        $ensure$
        """;

    private const string CreateLedgerSql = """
        CREATE TABLE IF NOT EXISTS schema_migrations (
            version int PRIMARY KEY,
            name text NOT NULL,
            checksum text NOT NULL,
            applied_at timestamptz NOT NULL DEFAULT now(),
            applied_by text NOT NULL DEFAULT current_user
        )
        """;

    private const string LedgerExistsSql = "SELECT to_regclass(quote_ident(@schema) || '.schema_migrations') IS NOT NULL";

    private const string SelectAppliedSql = "SELECT version, name, checksum FROM schema_migrations ORDER BY version";

    private const string SelectChecksumSql = "SELECT checksum FROM schema_migrations WHERE version = @version";

    private const string InsertAppliedSql = "INSERT INTO schema_migrations (version, name, checksum) VALUES (@version, @name, @checksum)";

    private const string DeleteAppliedSql = "DELETE FROM schema_migrations WHERE version = @version";

    private static readonly long LockKey = PostgresSql.AdvisoryKey(LockName);

    private readonly NpgsqlDataSource _dataSource;
    private readonly string _schema;
    private readonly ImmutableArray<IMigrationStep> _steps;
    private readonly ResiliencePipeline _pipeline;

    /// <summary>Initializes a new instance of the <see cref="MigrationRunner"/> class.</summary>
    /// <param name="dataSource">The data source of the migration role (or, in development, the runtime role). Its lifetime is owned by the caller.</param>
    /// <param name="schema">The schema to migrate: letters, digits and underscores, at most 63 characters.</param>
    /// <param name="steps">The steps to run, or <see langword="null"/> for <see cref="MigrationRegistry.All"/>. Versions must be unique and strictly increasing.</param>
    /// <param name="resilience">The pipeline every command runs through, or <see langword="null"/> for a private pipeline with the default options.</param>
    /// <exception cref="ArgumentException">The schema name or the step order is invalid.</exception>
    public MigrationRunner(NpgsqlDataSource dataSource, string schema = DefaultSchema, IReadOnlyList<IMigrationStep>? steps = null, PostgresResilience? resilience = null)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);
        if (!IsSchemaName(schema))
        {
            throw new ArgumentException("A schema name has letters, digits and underscores only, does not start with a digit and has at most 63 characters.", nameof(schema));
        }

        _steps = steps is null ? MigrationRegistry.All : [.. steps];
        if (_steps.IsDefaultOrEmpty)
        {
            throw new ArgumentException("A migration runner needs at least one step.", nameof(steps));
        }

        for (var index = 1; index < _steps.Length; index++)
        {
            if (_steps[index].Version <= _steps[index - 1].Version)
            {
                throw new ArgumentException($"Migration versions must be unique and strictly increasing; version {_steps[index].Version} follows {_steps[index - 1].Version}.", nameof(steps));
            }
        }

        _dataSource = dataSource;
        _schema = schema;
        _pipeline = (resilience ?? PostgresResilience.CreateDefault()).Pipeline;
    }

    /// <summary>Gets the version of the last step of this runner: the version the schema must reach.</summary>
    public int Head => _steps[^1].Version;

    /// <summary>Gets the schema this runner targets.</summary>
    public string Schema => _schema;

    /// <summary>Gets the SHA-256 checksum of a step's up SQL, lower-case hexadecimal.</summary>
    /// <param name="step">The step.</param>
    /// <returns>The checksum that <c>schema_migrations</c> records for the step.</returns>
    public static string ChecksumOf(IMigrationStep step)
    {
        ArgumentNullException.ThrowIfNull(step);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(step.UpSql)));
    }

    /// <summary>Applies every pending step up to <see cref="Head"/>, in order. Steps already applied are skipped; a second call applies nothing.</summary>
    /// <param name="cancellationToken">Cancels the run between and during steps.</param>
    /// <returns>The versions applied by this call, in order. Empty when the schema was already at the head or ahead of it.</returns>
    /// <exception cref="MigrationChecksumMismatchException">An applied step differs from the compiled step; nothing is applied.</exception>
    /// <exception cref="MigrationPreconditionException">A step's precondition does not hold; the steps before it stay applied.</exception>
    public async Task<IReadOnlyList<int>> MigrateAsync(CancellationToken cancellationToken)
    {
        await RunAsync(token => InitializeAsync(token), cancellationToken).ConfigureAwait(false);

        var applied = await ListAppliedAsync(cancellationToken).ConfigureAwait(false);
        var recorded = applied.ToDictionary(static migration => migration.Version);
        foreach (var step in _steps)
        {
            if (recorded.TryGetValue(step.Version, out var migration))
            {
                VerifyChecksum(step, migration.Checksum);
            }
        }

        var done = new List<int>();
        foreach (var step in _steps)
        {
            if (recorded.ContainsKey(step.Version))
            {
                continue;
            }

            if (await RunAsync(token => ApplyStepAsync(step, token), cancellationToken).ConfigureAwait(false))
            {
                done.Add(step.Version);
            }
        }

        return done;
    }

    /// <summary>
    /// Reverts every applied step above <paramref name="version"/>, newest first, by running its down SQL. The steps up to <paramref name="version"/> stay applied.
    /// Nothing runs this on its own: an operator asks for it, never in the release that shipped the step.
    /// </summary>
    /// <param name="version">The version to keep, at least 0 (revert everything).</param>
    /// <param name="cancellationToken">Cancels the run between and during steps.</param>
    /// <returns>The versions reverted by this call, newest first.</returns>
    /// <exception cref="MigrationChecksumMismatchException">An applied step differs from the compiled step; nothing is reverted.</exception>
    /// <exception cref="InvalidOperationException">A step above <paramref name="version"/> is applied but this build has no step for it.</exception>
    public async Task<IReadOnlyList<int>> DownAsync(int version, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(version);

        var applied = await ListAppliedAsync(cancellationToken).ConfigureAwait(false);
        var targets = new List<(IMigrationStep Step, AppliedMigration Migration)>();
        foreach (var migration in applied.Where(m => m.Version > version).OrderByDescending(static m => m.Version))
        {
            var step = _steps.FirstOrDefault(candidate => candidate.Version == migration.Version)
                ?? throw new InvalidOperationException($"Migration {migration.Version} ({migration.Name}) is applied, but this build has no step for it, so it cannot be reverted.");
            VerifyChecksum(step, migration.Checksum);
            targets.Add((step, migration));
        }

        var reverted = new List<int>();
        foreach (var (step, _) in targets)
        {
            if (await RunAsync(token => RevertStepAsync(step, token), cancellationToken).ConfigureAwait(false))
            {
                reverted.Add(step.Version);
            }
        }

        return reverted;
    }

    /// <summary>Lists the applied steps, oldest first. An empty list when the schema or its ledger does not exist yet.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The applied steps.</returns>
    public Task<IReadOnlyList<AppliedMigration>> ListAppliedAsync(CancellationToken cancellationToken) =>
        _pipeline.ExecuteAsync(OperationClass.Background, true, token => ReadAppliedAsync(token), cancellationToken);

    /// <summary>Gets the highest applied version.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The highest applied version, or 0 when nothing is applied.</returns>
    public async Task<int> GetCurrentVersionAsync(CancellationToken cancellationToken)
    {
        var applied = await ListAppliedAsync(cancellationToken).ConfigureAwait(false);
        return applied.Count == 0 ? 0 : applied[^1].Version;
    }

    private static bool IsSchemaName(string value)
    {
        if (value.Length > MaxSchemaNameLength || char.IsAsciiDigit(value[0]))
        {
            return false;
        }

        foreach (var character in value)
        {
            if (!char.IsAsciiLetterOrDigit(character) && character != '_')
            {
                return false;
            }
        }

        return true;
    }

    private static void VerifyChecksum(IMigrationStep step, string recorded)
    {
        var compiled = ChecksumOf(step);
        if (!string.Equals(recorded, compiled, StringComparison.Ordinal))
        {
            throw new MigrationChecksumMismatchException(step.Version, step.Name, recorded, compiled);
        }
    }

    private Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken) =>
        _pipeline.ExecuteAsync(OperationClass.Background, true, operation, cancellationToken);

    private async Task<bool> InitializeAsync(CancellationToken cancellationToken)
    {
        var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await using (transaction.ConfigureAwait(false))
            {
                await BeginStepAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
                await ExecuteTextAsync(connection, transaction, EnsureSchemaSql, cancellationToken).ConfigureAwait(false);
                await ExecuteTextAsync(connection, transaction, CreateLedgerSql, cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return true;
            }
        }
    }

    private async Task<IReadOnlyList<AppliedMigration>> ReadAppliedAsync(CancellationToken cancellationToken)
    {
        var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await using (transaction.ConfigureAwait(false))
            {
                await PrepareAsync(connection, transaction, cancellationToken).ConfigureAwait(false);

                using (var exists = PostgresSql.Create(connection, transaction))
                {
                    exists.CommandText = LedgerExistsSql;
                    exists.Parameters.AddWithValue("schema", _schema);
                    if (await exists.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is not true)
                    {
                        return [];
                    }
                }

                var applied = new List<AppliedMigration>();
                using var command = PostgresSql.Create(connection, transaction);
                command.CommandText = SelectAppliedSql;
                var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                await using (reader.ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        applied.Add(new AppliedMigration(reader.GetInt32(0), reader.GetString(1), reader.GetString(2)));
                    }
                }

                return applied;
            }
        }
    }

    private async Task<bool> ApplyStepAsync(IMigrationStep step, CancellationToken cancellationToken)
    {
        var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await using (transaction.ConfigureAwait(false))
            {
                await BeginStepAsync(connection, transaction, cancellationToken).ConfigureAwait(false);

                // The check and the apply are one transaction under the lock: a runner that waited for the lock finds the step applied and does nothing.
                var recorded = await ReadChecksumAsync(connection, transaction, step.Version, cancellationToken).ConfigureAwait(false);
                if (recorded is not null)
                {
                    VerifyChecksum(step, recorded);
                    return false;
                }

                if (step is IMigrationPrecondition precondition)
                {
                    await precondition.CheckAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
                }

                await ExecuteTextAsync(connection, transaction, step.UpSql, cancellationToken).ConfigureAwait(false);

                using (var insert = PostgresSql.Create(connection, transaction))
                {
                    insert.CommandText = InsertAppliedSql;
                    insert.Parameters.AddWithValue("version", step.Version);
                    insert.Parameters.AddWithValue("name", step.Name);
                    insert.Parameters.AddWithValue("checksum", ChecksumOf(step));
                    await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }

                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return true;
            }
        }
    }

    private async Task<bool> RevertStepAsync(IMigrationStep step, CancellationToken cancellationToken)
    {
        var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await using (transaction.ConfigureAwait(false))
            {
                await BeginStepAsync(connection, transaction, cancellationToken).ConfigureAwait(false);

                var recorded = await ReadChecksumAsync(connection, transaction, step.Version, cancellationToken).ConfigureAwait(false);
                if (recorded is null)
                {
                    return false;
                }

                VerifyChecksum(step, recorded);
                await ExecuteTextAsync(connection, transaction, step.DownSql, cancellationToken).ConfigureAwait(false);

                using (var delete = PostgresSql.Create(connection, transaction))
                {
                    delete.CommandText = DeleteAppliedSql;
                    delete.Parameters.AddWithValue("version", step.Version);
                    await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }

                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return true;
            }
        }
    }

    /// <summary>Takes the migrations lock, then puts the target schema first in the search path of the transaction.</summary>
    private async Task BeginStepAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        using (var command = PostgresSql.Create(connection, transaction))
        {
            command.CommandText = LockSql;
            command.Parameters.AddWithValue("key", LockKey);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await PrepareAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
    }

    private async Task PrepareAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        using var command = PostgresSql.Create(connection, transaction);
        command.CommandText = PrepareSql;
        command.Parameters.AddWithValue("schema", _schema);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<string?> ReadChecksumAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, int version, CancellationToken cancellationToken)
    {
        using var command = PostgresSql.Create(connection, transaction);
        command.CommandText = SelectChecksumSql;
        command.Parameters.AddWithValue("version", version);
        return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The text is the up or down SQL of a registered step (a compile-time constant raw string of an internal step class that the registry compiles in) or one of this class's own constants. No request, configuration or user input reaches it, and a step has no parameters.")]
    private static async Task ExecuteTextAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string sql, CancellationToken cancellationToken)
    {
        using var command = PostgresSql.Create(connection, transaction);
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
