using System.Text.Json;
using Npgsql;
using Autonogrammer.Contracts.Cloud;

namespace Autonogrammer.Sessions.Postgres;

/// <summary>
/// PostgreSQL-backed <see cref="ICloudAgentStore"/>. Several API instances share one table. Every change runs in a transaction:
/// creation and activation take a transaction-scoped advisory lock keyed by the owner, so the cap checks and the write are one step
/// across processes; <see cref="UpdateAsync"/> takes the row lock with <c>SELECT … FOR UPDATE</c>. The lock order is always the owner
/// lock first, then the row lock, so two changes cannot wait on each other.
/// </summary>
/// <remarks>The table is created by <see cref="PostgresCloudAgentSchema"/>. No column or JSON member holds a secret value.</remarks>
public sealed class PostgresCloudAgentStore : ICloudAgentStore
{
    private const string SelectByIdSql = "SELECT record FROM cloud_agents WHERE id = @id";

    private const string SelectByIdForUpdateSql = "SELECT record FROM cloud_agents WHERE id = @id FOR UPDATE";

    private const string SelectOwnerOfSql = "SELECT owner_id FROM cloud_agents WHERE id = @id";

    private const string SelectBySessionSql = "SELECT record FROM cloud_agents WHERE session_id = @session_id";

    private const string SelectByKeySql = "SELECT record FROM cloud_agents WHERE owner_id = @owner_id AND idempotency_key = @idempotency_key";

    private const string SelectByOwnerSql = "SELECT record FROM cloud_agents WHERE owner_id = @owner_id ORDER BY created_at DESC, id";

    private const string SelectByStatusSql = "SELECT record FROM cloud_agents WHERE status = ANY(@statuses) ORDER BY updated_at, id";

    private const string SelectAllSql = "SELECT record FROM cloud_agents ORDER BY created_at, id";

    private const string CountSql =
        "SELECT count(*), count(*) FILTER (WHERE status = ANY(@active)) FROM cloud_agents WHERE owner_id = @owner_id";

    private const string InsertSql =
        "INSERT INTO cloud_agents (id, owner_id, session_id, status, created_at, updated_at, idempotency_key, record) " +
        "VALUES (@id, @owner_id, @session_id, @status, @created_at, @updated_at, @idempotency_key, CAST(@record AS jsonb))";

    private const string UpdateSql =
        "UPDATE cloud_agents SET status = @status, updated_at = @updated_at, record = CAST(@record AS jsonb) WHERE id = @id";

    private const string DeleteSql = "DELETE FROM cloud_agents WHERE id = @id";

    private const string UsageSql =
        "SELECT count(*), count(*) FILTER (WHERE status = ANY(@active)), COALESCE(sum((record ->> 'computeSeconds')::double precision), 0) " +
        "FROM cloud_agents WHERE owner_id = @owner_id";

    private const string OwnerLockSql = "SELECT pg_advisory_xact_lock(@key)";

    private readonly NpgsqlDataSource _dataSource;

    /// <summary>Initializes a new instance of the <see cref="PostgresCloudAgentStore"/> class.</summary>
    /// <param name="dataSource">The data source whose search path holds the <c>cloud_agents</c> table.</param>
    public PostgresCloudAgentStore(NpgsqlDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        _dataSource = dataSource;
    }

    /// <inheritdoc />
    public async Task<CloudAgentCreateResult> CreateAsync(CloudAgentRecord record, CloudAgentLimits limits, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(limits);
        var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await using (transaction.ConfigureAwait(false))
            {
                await LockOwnerAsync(connection, transaction, record.OwnerId, cancellationToken).ConfigureAwait(false);
                if (record.IdempotencyKey is { } key)
                {
                    var existing = await QueryOneAsync(connection, transaction, command =>
                    {
                        command.CommandText = SelectByKeySql;
                        command.Parameters.AddWithValue("owner_id", record.OwnerId);
                        command.Parameters.AddWithValue("idempotency_key", key);
                    }, cancellationToken).ConfigureAwait(false);
                    if (existing is not null)
                    {
                        return string.Equals(existing.RequestFingerprint, record.RequestFingerprint, StringComparison.Ordinal)
                            ? new CloudAgentCreateResult(CloudAgentCreateOutcome.Replayed, existing)
                            : new CloudAgentCreateResult(CloudAgentCreateOutcome.IdempotencyKeyReused, null);
                    }
                }

                var (agents, active) = await CountAsync(connection, transaction, record.OwnerId, cancellationToken).ConfigureAwait(false);
                if (agents >= limits.MaxAgents)
                {
                    return new CloudAgentCreateResult(CloudAgentCreateOutcome.AgentLimitReached, null);
                }

                if (active >= limits.MaxActive)
                {
                    return new CloudAgentCreateResult(CloudAgentCreateOutcome.ActiveLimitReached, null);
                }

                using var insert = PostgresSql.Create(connection, transaction);
                insert.CommandText = InsertSql;
                insert.Parameters.AddWithValue("id", record.Id);
                insert.Parameters.AddWithValue("owner_id", record.OwnerId);
                insert.Parameters.AddWithValue("session_id", record.SessionId);
                insert.Parameters.AddWithValue("status", record.Status.ToString());
                insert.Parameters.AddWithValue("created_at", record.CreatedAt);
                insert.Parameters.AddWithValue("updated_at", record.UpdatedAt);
                insert.Parameters.AddWithValue("idempotency_key", (object?)record.IdempotencyKey ?? DBNull.Value);
                insert.Parameters.AddWithValue("record", Serialize(record));
                await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return new CloudAgentCreateResult(CloudAgentCreateOutcome.Created, record);
            }
        }
    }

    /// <inheritdoc />
    public Task<CloudAgentRecord?> GetAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        return QueryOneAsync(
            command =>
            {
                command.CommandText = SelectByIdSql;
                command.Parameters.AddWithValue("id", id);
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<CloudAgentRecord?> GetBySessionAsync(string sessionId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        return QueryOneAsync(
            command =>
            {
                command.CommandText = SelectBySessionSql;
                command.Parameters.AddWithValue("session_id", sessionId);
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<CloudAgentRecord?> FindByIdempotencyKeyAsync(string ownerId, string idempotencyKey, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ownerId);
        ArgumentNullException.ThrowIfNull(idempotencyKey);
        return QueryOneAsync(
            command =>
            {
                command.CommandText = SelectByKeySql;
                command.Parameters.AddWithValue("owner_id", ownerId);
                command.Parameters.AddWithValue("idempotency_key", idempotencyKey);
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<CloudAgentRecord>> ListByOwnerAsync(string ownerId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ownerId);
        return QueryManyAsync(
            command =>
            {
                command.CommandText = SelectByOwnerSql;
                command.Parameters.AddWithValue("owner_id", ownerId);
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<CloudAgentRecord>> ListByStatusAsync(IReadOnlyCollection<CloudAgentStatus> statuses, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(statuses);
        var names = statuses.Select(static status => status.ToString()).ToArray();
        return QueryManyAsync(
            command =>
            {
                command.CommandText = SelectByStatusSql;
                command.Parameters.AddWithValue("statuses", names);
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<CloudAgentRecord>> ListAllAsync(CancellationToken cancellationToken) =>
        QueryManyAsync(static command => command.CommandText = SelectAllSql, cancellationToken);

    /// <inheritdoc />
    public async Task<CloudAgentRecord?> UpdateAsync(string id, Func<CloudAgentRecord, CloudAgentRecord> mutate, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(mutate);
        var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await using (transaction.ConfigureAwait(false))
            {
                var current = await QueryOneAsync(connection, transaction, command => BindForUpdate(command, id), cancellationToken).ConfigureAwait(false);
                if (current is null)
                {
                    return null;
                }

                var updated = mutate(current);
                if (!string.Equals(updated.Id, current.Id, StringComparison.Ordinal)
                    || !string.Equals(updated.OwnerId, current.OwnerId, StringComparison.Ordinal)
                    || !string.Equals(updated.SessionId, current.SessionId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("A cloud agent update must not change the identifier, the owner or the session.");
                }

                await WriteAsync(connection, transaction, updated, cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return updated;
            }
        }
    }

    /// <inheritdoc />
    public async Task<CloudAgentActivation> TryActivateAsync(string id, int maxActive, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await using (transaction.ConfigureAwait(false))
            {
                string? owner;
                using (var ownerCommand = PostgresSql.Create(connection, transaction))
                {
                    ownerCommand.CommandText = SelectOwnerOfSql;
                    ownerCommand.Parameters.AddWithValue("id", id);
                    owner = await ownerCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
                }

                if (owner is null)
                {
                    return new CloudAgentActivation(CloudAgentActivationOutcome.NotFound, null);
                }

                await LockOwnerAsync(connection, transaction, owner, cancellationToken).ConfigureAwait(false);
                var current = await QueryOneAsync(connection, transaction, command => BindForUpdate(command, id), cancellationToken).ConfigureAwait(false);
                if (current is null)
                {
                    return new CloudAgentActivation(CloudAgentActivationOutcome.NotFound, null);
                }

                if (CloudAgentStatusRules.IsActive(current.Status))
                {
                    return new CloudAgentActivation(CloudAgentActivationOutcome.AlreadyActive, current);
                }

                if (!CloudAgentStatusRules.CanActivate(current.Status))
                {
                    return new CloudAgentActivation(CloudAgentActivationOutcome.NotWakeable, current);
                }

                var (_, active) = await CountAsync(connection, transaction, owner, cancellationToken).ConfigureAwait(false);
                if (active >= maxActive)
                {
                    return new CloudAgentActivation(CloudAgentActivationOutcome.LimitReached, current);
                }

                var moved = CloudAgentStatusRules.Activate(current, now);
                await WriteAsync(connection, transaction, moved, cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return new CloudAgentActivation(CloudAgentActivationOutcome.Activated, moved);
            }
        }
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            using var command = PostgresSql.Create(connection, null);
            command.CommandText = DeleteSql;
            command.Parameters.AddWithValue("id", id);
            return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) > 0;
        }
    }

    /// <inheritdoc />
    public async Task<CloudAgentUsage> GetUsageAsync(string ownerId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ownerId);
        var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            using var command = PostgresSql.Create(connection, null);
            command.CommandText = UsageSql;
            command.Parameters.AddWithValue("owner_id", ownerId);
            command.Parameters.AddWithValue("active", ActiveNames());
            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
                return new CloudAgentUsage((int)reader.GetInt64(0), (int)reader.GetInt64(1), reader.GetDouble(2));
            }
        }
    }

    private static void BindForUpdate(NpgsqlCommand command, string id)
    {
        command.CommandText = SelectByIdForUpdateSql;
        command.Parameters.AddWithValue("id", id);
    }

    private static string[] ActiveNames() => CloudAgentStatusRules.ActiveStatuses.Select(static status => status.ToString()).ToArray();

    private static string Serialize(CloudAgentRecord record) => JsonSerializer.Serialize(record, CloudJsonContext.Default.CloudAgentRecord);

    private static CloudAgentRecord Deserialize(string json) =>
        JsonSerializer.Deserialize(json, CloudJsonContext.Default.CloudAgentRecord)
        ?? throw new InvalidOperationException("A stored cloud agent record is empty.");

    private static async Task LockOwnerAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string ownerId, CancellationToken cancellationToken)
    {
        using var command = PostgresSql.Create(connection, transaction);
        command.CommandText = OwnerLockSql;
        command.Parameters.AddWithValue("key", PostgresSql.AdvisoryKey("autonogrammer.cloud-agents.owner:" + ownerId));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<(int Agents, int Active)> CountAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string ownerId, CancellationToken cancellationToken)
    {
        using var command = PostgresSql.Create(connection, transaction);
        command.CommandText = CountSql;
        command.Parameters.AddWithValue("owner_id", ownerId);
        command.Parameters.AddWithValue("active", ActiveNames());
        var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        await using (reader.ConfigureAwait(false))
        {
            await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            return ((int)reader.GetInt64(0), (int)reader.GetInt64(1));
        }
    }

    private static async Task WriteAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, CloudAgentRecord record, CancellationToken cancellationToken)
    {
        using var command = PostgresSql.Create(connection, transaction);
        command.CommandText = UpdateSql;
        command.Parameters.AddWithValue("id", record.Id);
        command.Parameters.AddWithValue("status", record.Status.ToString());
        command.Parameters.AddWithValue("updated_at", record.UpdatedAt);
        command.Parameters.AddWithValue("record", Serialize(record));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<CloudAgentRecord?> QueryOneAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Action<NpgsqlCommand> bind,
        CancellationToken cancellationToken)
    {
        using var command = PostgresSql.Create(connection, transaction);
        bind(command);
        var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        await using (reader.ConfigureAwait(false))
        {
            return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? Deserialize(reader.GetString(0)) : null;
        }
    }

    private async Task<CloudAgentRecord?> QueryOneAsync(Action<NpgsqlCommand> bind, CancellationToken cancellationToken)
    {
        var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            return await QueryOneAsync(connection, null, bind, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<IReadOnlyList<CloudAgentRecord>> QueryManyAsync(Action<NpgsqlCommand> bind, CancellationToken cancellationToken)
    {
        var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            using var command = PostgresSql.Create(connection, null);
            bind(command);
            var records = new List<CloudAgentRecord>();
            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    records.Add(Deserialize(reader.GetString(0)));
                }
            }

            return records;
        }
    }
}
