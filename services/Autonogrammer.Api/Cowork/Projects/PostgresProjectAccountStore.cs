using System.Text.Json;
using Autonogrammer.Contracts.Cloud;
using Autonogrammer.Contracts.Cowork.Projects;
using Autonogrammer.Sessions.Postgres;
using Npgsql;
using NpgsqlTypes;

namespace Autonogrammer.Api.Cowork.Projects;

/// <summary>Account-scoped project and instruction persistence using the host's application data source.</summary>
internal sealed class PostgresProjectAccountStore(NpgsqlDataSource dataSource)
{
    private const string Columns = "id, name, description, instructions, context_urls::text, use_account_memory, archived_at, updated_at";

    public async Task<IReadOnlyList<ProjectSyncRecord>> ListAsync(string account, CancellationToken token)
    {
        var command = dataSource.CreateCommand($"SELECT {Columns} FROM cowork_projects WHERE account_id = @account ORDER BY created_at, id");
        await using var commandDisposal = command.ConfigureAwait(false);
        command.Parameters.AddWithValue("account", account);
        var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        await using var readerDisposal = reader.ConfigureAwait(false);
        var rows = new List<ProjectSyncRecord>();
        while (await reader.ReadAsync(token).ConfigureAwait(false)) rows.Add(Read(reader));
        return rows;
    }

    public async Task<ProjectSyncRecord?> GetAsync(string account, string id, CancellationToken token)
    {
        var command = dataSource.CreateCommand($"SELECT {Columns} FROM cowork_projects WHERE account_id = @account AND id = @id");
        await using var commandDisposal = command.ConfigureAwait(false);
        command.Parameters.AddWithValue("account", account);
        command.Parameters.AddWithValue("id", id);
        var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        await using var readerDisposal = reader.ConfigureAwait(false);
        return await reader.ReadAsync(token).ConfigureAwait(false) ? Read(reader) : null;
    }

    // The account advisory lock serializes the count and insert across API instances, including an empty account.
    public async Task<(ProjectSyncRecord? Record, bool Created, bool Capped)> UpsertAsync(string account, ProjectSyncRecord project, DateTimeOffset now, CancellationToken token)
    {
        var connection = await dataSource.OpenConnectionAsync(token).ConfigureAwait(false);
        await using var connectionDisposal = connection.ConfigureAwait(false);
        var transaction = await connection.BeginTransactionAsync(token).ConfigureAwait(false);
        await using var transactionDisposal = transaction.ConfigureAwait(false);
        await AccountAdmission.AdmitSharedAsync(connection, transaction, account, token).ConfigureAwait(false);
        var gate = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtextextended(@account, 0))", connection, transaction);
        await using (gate.ConfigureAwait(false))
        {
            gate.Parameters.AddWithValue("account", account);
            await gate.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }

        var count = new NpgsqlCommand("SELECT count(*) FROM cowork_projects WHERE account_id = @account AND id <> @id", connection, transaction);
        await using (count.ConfigureAwait(false))
        {
            count.Parameters.AddWithValue("account", account);
            count.Parameters.AddWithValue("id", project.Id);
            if ((long)(await count.ExecuteScalarAsync(token).ConfigureAwait(false))! >= 200) return (null, false, true);
        }

        var command = new NpgsqlCommand($"""
            INSERT INTO cowork_projects (id, account_id, name, description, instructions, context_urls, use_account_memory, archived_at, created_at, updated_at)
            VALUES (@id, @account, @name, @description, @instructions, @urls, @use_memory, @archived, @now, @now)
            ON CONFLICT (id) DO UPDATE SET name = EXCLUDED.name, description = EXCLUDED.description,
              instructions = EXCLUDED.instructions, context_urls = EXCLUDED.context_urls, use_account_memory = EXCLUDED.use_account_memory,
              archived_at = COALESCE(EXCLUDED.archived_at, cowork_projects.archived_at), updated_at = EXCLUDED.updated_at
            WHERE cowork_projects.account_id = @account
            RETURNING {Columns}, (xmax = 0)
            """, connection, transaction);
        await using var commandDisposal = command.ConfigureAwait(false);
        command.Parameters.AddWithValue("id", project.Id);
        command.Parameters.AddWithValue("account", account);
        command.Parameters.AddWithValue("name", project.Name);
        command.Parameters.Add(new NpgsqlParameter("description", NpgsqlDbType.Text) { Value = (object?)project.Description ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter("instructions", NpgsqlDbType.Text) { Value = (object?)project.Instructions ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter("urls", NpgsqlDbType.Jsonb) { Value = JsonSerializer.Serialize(project.ContextUrls.ToArray(), CloudJsonContext.Default.StringArray) });
        command.Parameters.AddWithValue("use_memory", project.UseAccountMemory);
        command.Parameters.Add(new NpgsqlParameter("archived", NpgsqlDbType.TimestampTz) { Value = project.ArchivedAt is { } archived ? archived.ToUniversalTime() : DBNull.Value });
        command.Parameters.AddWithValue("now", now);
        ProjectSyncRecord? record;
        bool created;
        var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        await using (reader.ConfigureAwait(false))
        {
            if (!await reader.ReadAsync(token).ConfigureAwait(false)) return (null, false, false);
            record = Read(reader);
            created = reader.GetBoolean(8);
        }
        await transaction.CommitAsync(token).ConfigureAwait(false);
        return (record, created, false);
    }

    public async Task<ProjectSyncRecord?> ArchiveAsync(string account, string id, DateTimeOffset now, CancellationToken token)
    {
        var connection = await dataSource.OpenConnectionAsync(token).ConfigureAwait(false);
        await using var connectionDisposal = connection.ConfigureAwait(false);
        var transaction = await connection.BeginTransactionAsync(token).ConfigureAwait(false);
        await using var transactionDisposal = transaction.ConfigureAwait(false);
        await AccountAdmission.AdmitSharedAsync(connection, transaction, account, token).ConfigureAwait(false);
        var command = new NpgsqlCommand($"UPDATE cowork_projects SET archived_at = @now, updated_at = @now WHERE account_id = @account AND id = @id RETURNING {Columns}", connection, transaction);
        await using var commandDisposal = command.ConfigureAwait(false);
        command.Parameters.AddWithValue("account", account);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("now", now);
        ProjectSyncRecord? record;
        var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        await using (reader.ConfigureAwait(false))
        {
            record = await reader.ReadAsync(token).ConfigureAwait(false) ? Read(reader) : null;
        }
        await transaction.CommitAsync(token).ConfigureAwait(false);
        return record;
    }

    public async Task<bool> DeleteAsync(string account, string id, CancellationToken token)
    {
        var connection = await dataSource.OpenConnectionAsync(token).ConfigureAwait(false);
        await using var connectionDisposal = connection.ConfigureAwait(false);
        var transaction = await connection.BeginTransactionAsync(token).ConfigureAwait(false);
        await using var transactionDisposal = transaction.ConfigureAwait(false);
        await AccountAdmission.AdmitSharedAsync(connection, transaction, account, token).ConfigureAwait(false);
        var command = new NpgsqlCommand("DELETE FROM cowork_projects WHERE account_id = @account AND id = @id", connection, transaction);
        await using var commandDisposal = command.ConfigureAwait(false);
        command.Parameters.AddWithValue("account", account);
        command.Parameters.AddWithValue("id", id);
        var deleted = await command.ExecuteNonQueryAsync(token).ConfigureAwait(false) != 0;
        await transaction.CommitAsync(token).ConfigureAwait(false);
        return deleted;
    }

    public async Task<string> GetInstructionsAsync(string account, CancellationToken token)
    {
        var command = dataSource.CreateCommand("SELECT body FROM cowork_instructions WHERE account_id = @account");
        await using var commandDisposal = command.ConfigureAwait(false);
        command.Parameters.AddWithValue("account", account);
        return (string?)await command.ExecuteScalarAsync(token).ConfigureAwait(false) ?? string.Empty;
    }

    public async Task SetInstructionsAsync(string account, string text, CancellationToken token)
    {
        var connection = await dataSource.OpenConnectionAsync(token).ConfigureAwait(false);
        await using var connectionDisposal = connection.ConfigureAwait(false);
        var transaction = await connection.BeginTransactionAsync(token).ConfigureAwait(false);
        await using var transactionDisposal = transaction.ConfigureAwait(false);
        await AccountAdmission.AdmitSharedAsync(connection, transaction, account, token).ConfigureAwait(false);
        var command = new NpgsqlCommand("INSERT INTO cowork_instructions (account_id, body, updated_at) VALUES (@account, @body, now()) ON CONFLICT (account_id) DO UPDATE SET body = EXCLUDED.body, updated_at = EXCLUDED.updated_at", connection, transaction);
        await using var commandDisposal = command.ConfigureAwait(false);
        command.Parameters.AddWithValue("account", account);
        command.Parameters.AddWithValue("body", text);
        await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        await transaction.CommitAsync(token).ConfigureAwait(false);
    }

    private static ProjectSyncRecord Read(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetString(0),
        Name = reader.GetString(1),
        Description = reader.IsDBNull(2) ? null : reader.GetString(2),
        Instructions = reader.IsDBNull(3) ? null : reader.GetString(3),
        ContextUrls = JsonSerializer.Deserialize(reader.GetString(4), CloudJsonContext.Default.StringArray) ?? [],
        UseAccountMemory = reader.GetBoolean(5),
        ArchivedAt = reader.IsDBNull(6) ? null : reader.GetFieldValue<DateTimeOffset>(6),
        UpdatedAt = reader.GetFieldValue<DateTimeOffset>(7),
    };
}
