using System.Text.Json;
using System.Text.Json.Serialization;
using Autonogrammer.Harness.Automations.Hosted;
using Autonogrammer.Resilience;
using Autonogrammer.Sessions.Postgres;
using Npgsql;

namespace Autonogrammer.Api.Automations;

/// <summary>
/// PostgreSQL-backed <see cref="IAutomationStore"/>, shared by every API instance. It mirrors <see cref="FileAutomationStore"/> operation for operation: the same
/// owner-agnostic reads, the same replacement that keeps the history and cursor, the same newest-first history capped at
/// <see cref="AutomationLimits.MaxHistoryEntries"/>, and the same no-op for an unknown automation. Owner checks stay with the caller.
/// </summary>
/// <remarks>
/// <para>
/// Every call runs in one transaction. A write first takes the shared account admission (<see cref="AccountAdmission"/>) for the owner of the automation it
/// writes, inside that transaction, so an erasing or unprovisioned account is refused before any row changes. A write to an automation that does not exist does
/// nothing and takes no admission. Reads take no admission.
/// </para>
/// <para>
/// The definition and each run entry are stored as JSON in <c>record</c>, written with the same naming, enum and unmapped-member rules as the file store, so a
/// record read back from either store is the same. The tables are created by migration M0016 of the <c>MigrationRunner</c>. Every command runs through the
/// PostgreSQL <see cref="ResiliencePipeline"/>; a write retries only a serialisation failure, and only as a whole transaction.
/// </para>
/// </remarks>
internal sealed class PostgresAutomationStore : IAutomationStore
{
    private const string SelectAllSql = "SELECT record FROM automations ORDER BY id";
    private const string SelectByIdSql = "SELECT record FROM automations WHERE id = @id";
    private const string SelectOwnerSql = "SELECT owner_id FROM automations WHERE id = @id";
    private const string SelectCursorSql = "SELECT schedule_cursor FROM automations WHERE id = @id";
    private const string UpsertSql =
        "INSERT INTO automations (id, owner_id, record) VALUES (@id, @owner_id, CAST(@record AS jsonb)) " +
        "ON CONFLICT (id) DO UPDATE SET owner_id = EXCLUDED.owner_id, record = EXCLUDED.record";
    private const string DeleteSql = "DELETE FROM automations WHERE id = @id";
    private const string SetCursorSql = "UPDATE automations SET schedule_cursor = @cursor WHERE id = @id";
    private const string SelectHistorySql = "SELECT record FROM automation_runs WHERE automation_id = @id ORDER BY seq DESC";
    private const string UpsertRunSql =
        "INSERT INTO automation_runs (automation_id, owner_id, run_id, record) VALUES (@id, @owner_id, @run_id, CAST(@record AS jsonb)) " +
        "ON CONFLICT (automation_id, run_id) DO UPDATE SET record = EXCLUDED.record";
    private const string TrimHistorySql =
        "DELETE FROM automation_runs AS r WHERE r.automation_id = @id AND r.seq <= " +
        "(SELECT s.seq FROM automation_runs AS s WHERE s.automation_id = @id ORDER BY s.seq DESC OFFSET @keep LIMIT 1)";

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly NpgsqlDataSource _dataSource;
    private readonly ResiliencePipeline _pipeline;

    /// <summary>Initializes a new instance of the <see cref="PostgresAutomationStore"/> class.</summary>
    /// <param name="dataSource">The data source whose search path holds the <c>automations</c> and <c>automation_runs</c> tables.</param>
    /// <param name="resilience">The pipeline every command runs through.</param>
    public PostgresAutomationStore(NpgsqlDataSource dataSource, PostgresResilience resilience)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        ArgumentNullException.ThrowIfNull(resilience);
        _dataSource = dataSource;
        _pipeline = resilience.Pipeline;
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<UserAutomation>> ListAsync(CancellationToken cancellationToken)
    {
        var texts = await ReadAsync(
            (connection, transaction, token) => ReadTextsAsync(new NpgsqlCommand(SelectAllSql, connection, transaction), null, token),
            cancellationToken).ConfigureAwait(false);
        return texts.Select(static text => Deserialize<UserAutomation>(text)).ToArray();
    }

    /// <inheritdoc />
    public async ValueTask<UserAutomation?> GetAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        var texts = await ReadAsync(
            (connection, transaction, token) => ReadTextsAsync(new NpgsqlCommand(SelectByIdSql, connection, transaction), id, token),
            cancellationToken).ConfigureAwait(false);
        return texts.Count == 0 ? null : Deserialize<UserAutomation>(texts[0]);
    }

    /// <inheritdoc />
    public async ValueTask SaveAsync(UserAutomation automation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(automation);
        var record = Serialize(automation);
        _ = await WriteAsync(
            async (connection, transaction, token) =>
            {
                await AccountAdmission.AdmitSharedAsync(connection, transaction, automation.Owner, token).ConfigureAwait(false);
                using var upsert = new NpgsqlCommand(UpsertSql, connection, transaction);
                upsert.Parameters.AddWithValue("id", automation.Id);
                upsert.Parameters.AddWithValue("owner_id", automation.Owner);
                upsert.Parameters.AddWithValue("record", record);
                _ = await upsert.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                return true;
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<bool> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return await WriteAsync(
            async (connection, transaction, token) =>
            {
                if (await AdmitOwnerOfAsync(connection, transaction, id, token).ConfigureAwait(false) is null)
                {
                    return false;
                }

                using var delete = new NpgsqlCommand(DeleteSql, connection, transaction);
                delete.Parameters.AddWithValue("id", id);
                return await delete.ExecuteNonQueryAsync(token).ConfigureAwait(false) > 0;
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<DateTimeOffset?> GetScheduleCursorAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return await ReadAsync(
            (connection, transaction, token) => ReadCursorAsync(new NpgsqlCommand(SelectCursorSql, connection, transaction), id, token),
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask SetScheduleCursorAsync(string id, DateTimeOffset cursor, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        _ = await WriteAsync(
            async (connection, transaction, token) =>
            {
                if (await AdmitOwnerOfAsync(connection, transaction, id, token).ConfigureAwait(false) is null)
                {
                    return false;
                }

                using var update = new NpgsqlCommand(SetCursorSql, connection, transaction);
                update.Parameters.AddWithValue("id", id);
                update.Parameters.AddWithValue("cursor", cursor);
                return await update.ExecuteNonQueryAsync(token).ConfigureAwait(false) > 0;
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<AutomationRunEntry>> GetHistoryAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        var texts = await ReadAsync(
            (connection, transaction, token) => ReadTextsAsync(new NpgsqlCommand(SelectHistorySql, connection, transaction), id, token),
            cancellationToken).ConfigureAwait(false);
        return texts.Select(static text => Deserialize<AutomationRunEntry>(text)).ToArray();
    }

    /// <inheritdoc />
    public async ValueTask UpsertRunAsync(string id, AutomationRunEntry entry, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(entry);
        var record = Serialize(entry);
        _ = await WriteAsync(
            async (connection, transaction, token) =>
            {
                var owner = await AdmitOwnerOfAsync(connection, transaction, id, token).ConfigureAwait(false);
                if (owner is null)
                {
                    return false;
                }

                using (var upsert = new NpgsqlCommand(UpsertRunSql, connection, transaction))
                {
                    upsert.Parameters.AddWithValue("id", id);
                    upsert.Parameters.AddWithValue("owner_id", owner);
                    upsert.Parameters.AddWithValue("run_id", entry.RunId);
                    upsert.Parameters.AddWithValue("record", record);
                    _ = await upsert.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }

                using var trim = new NpgsqlCommand(TrimHistorySql, connection, transaction);
                trim.Parameters.AddWithValue("id", id);
                trim.Parameters.AddWithValue("keep", AutomationLimits.MaxHistoryEntries);
                _ = await trim.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                return true;
            },
            cancellationToken).ConfigureAwait(false);
    }

    private Task<T> ReadAsync<T>(Func<NpgsqlConnection, NpgsqlTransaction, CancellationToken, Task<T>> operation, CancellationToken cancellationToken) =>
        _pipeline.ExecuteAsync(OperationClass.Interactive, true, token => RunInTransactionAsync(operation, token), cancellationToken);

    private Task<T> WriteAsync<T>(Func<NpgsqlConnection, NpgsqlTransaction, CancellationToken, Task<T>> operation, CancellationToken cancellationToken) =>
        _pipeline.ExecuteTransactionAsync(OperationClass.Interactive, token => RunInTransactionAsync(operation, token), cancellationToken);

    private async Task<T> RunInTransactionAsync<T>(Func<NpgsqlConnection, NpgsqlTransaction, CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
    {
        var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await using (transaction.ConfigureAwait(false))
            {
                var result = await operation(connection, transaction, cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return result;
            }
        }
    }

    /// <summary>Reads the owner of an automation and takes the shared admission for it inside the transaction.</summary>
    /// <returns>The owner, or null when no automation has the identifier (nothing is admitted then).</returns>
    private static async Task<string?> AdmitOwnerOfAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string id, CancellationToken cancellationToken)
    {
        string? owner;
        using (var select = new NpgsqlCommand(SelectOwnerSql, connection, transaction))
        {
            select.Parameters.AddWithValue("id", id);
            owner = await select.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
        }

        if (owner is not null)
        {
            await AccountAdmission.AdmitSharedAsync(connection, transaction, owner, cancellationToken).ConfigureAwait(false);
        }

        return owner;
    }

    private static async Task<DateTimeOffset?> ReadCursorAsync(NpgsqlCommand command, string id, CancellationToken cancellationToken)
    {
        using (command)
        {
            command.Parameters.AddWithValue("id", id);
            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false) || await reader.IsDBNullAsync(0, cancellationToken).ConfigureAwait(false))
                {
                    return null;
                }

                return await reader.GetFieldValueAsync<DateTimeOffset>(0, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static async Task<List<string>> ReadTextsAsync(NpgsqlCommand command, string? id, CancellationToken cancellationToken)
    {
        using (command)
        {
            if (id is not null)
            {
                command.Parameters.AddWithValue("id", id);
            }

            var texts = new List<string>();
            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    texts.Add(reader.GetString(0));
                }
            }

            return texts;
        }
    }

    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Json);

    private static T Deserialize<T>(string text) =>
        JsonSerializer.Deserialize<T>(text, Json) ?? throw new InvalidDataException("A stored automation record is empty and was not read.");
}
