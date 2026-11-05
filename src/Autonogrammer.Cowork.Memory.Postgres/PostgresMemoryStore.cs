using System.Globalization;
using System.Text.Json;
using Autonogrammer.Contracts.Cowork.Memory;
using Npgsql;
using NpgsqlTypes;

namespace Autonogrammer.Cowork.Memory.Postgres;

/// <summary>
/// Account memory over the M0009 tables. The host supplies the <c>autonogrammer_app</c> data source. This type writes no migration,
/// runs DML only, and filters every query on <c>account_id</c>. There is no row-level security.
/// </summary>
public sealed class PostgresMemoryStore : IMemoryStore
{
    /// <summary>
    /// The GIN expression from M0009. Search uses this text so the index <c>cowork_memory_entries_search_idx</c> can be used.
    /// </summary>
    public const string SearchVectorExpression = "to_tsvector('simple', coalesce(topic, '') || ' ' || coalesce(content, ''))";

    /// <summary>The index M0009 creates for <see cref="SearchVectorExpression"/>.</summary>
    public const string SearchIndexName = "cowork_memory_entries_search_idx";

    private readonly NpgsqlDataSource _dataSource;
    private readonly TimeProvider _timeProvider;
    private readonly IMemoryNoticeSink? _notices;

    /// <summary>Initializes a new instance of the <see cref="PostgresMemoryStore"/> class.</summary>
    /// <param name="dataSource">The host data source. This type does not dispose it.</param>
    /// <param name="time">The clock.</param>
    /// <param name="notices">The notice sink, or null.</param>
    public PostgresMemoryStore(NpgsqlDataSource dataSource, TimeProvider? time = null, IMemoryNoticeSink? notices = null)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        _dataSource = dataSource;
        _timeProvider = time ?? TimeProvider.System;
        _notices = notices;
    }

    /// <inheritdoc />
    public async Task<MemorySettings> GetSettingsAsync(string accountId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);
        var command = _dataSource.CreateCommand("SELECT enabled, paused, include_sensitive FROM cowork_memory_settings WHERE account_id = @account_id");
        await using (command.ConfigureAwait(false))
        {
            command.Parameters.Add(Account(accountId));
            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    return new MemorySettings(false, false, false);
                }

                return new MemorySettings(reader.GetBoolean(0), reader.GetBoolean(1), reader.GetBoolean(2));
            }
        }
    }

    /// <inheritdoc />
    public async Task SetSettingsAsync(string accountId, MemorySettings settings, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);
        ArgumentNullException.ThrowIfNull(settings);
        var command = _dataSource.CreateCommand(
            """
            INSERT INTO cowork_memory_settings (account_id, enabled, paused, include_sensitive)
            VALUES (@account_id, @enabled, @paused, @include_sensitive)
            ON CONFLICT (account_id) DO UPDATE
            SET enabled = EXCLUDED.enabled, paused = EXCLUDED.paused, include_sensitive = EXCLUDED.include_sensitive
            """);
        await using (command.ConfigureAwait(false))
        {
            command.Parameters.Add(Account(accountId));
            command.Parameters.Add(new NpgsqlParameter("enabled", NpgsqlDbType.Boolean) { Value = settings.Enabled });
            command.Parameters.Add(new NpgsqlParameter("paused", NpgsqlDbType.Boolean) { Value = settings.Paused });
            command.Parameters.Add(new NpgsqlParameter("include_sensitive", NpgsqlDbType.Boolean) { Value = settings.IncludeSensitive });
            _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task<MemoryMutation> SaveAsync(MemoryScope scope, MemoryDraft draft, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(draft);
        var settings = await GetSettingsAsync(scope.AccountId, cancellationToken).ConfigureAwait(false);
        var refused = Refuse(scope, draft, settings);
        if (refused is not null)
        {
            return refused;
        }

        var capped = await CapAsync(scope, cancellationToken).ConfigureAwait(false);
        if (capped is not null)
        {
            return capped;
        }

        return await InsertAsync(scope, draft, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<MemoryRecall> RecallAsync(MemoryScope scope, string? query, int k, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if ((await GetSettingsAsync(scope.AccountId, cancellationToken).ConfigureAwait(false)).Paused)
        {
            return new MemoryRecall(false, MemoryCodes.MemoryPaused, "Memory is paused. Entries are kept, and nothing was read or written.", []);
        }

        if (query is not null && query.Length > MemoryOptions.MaxQueryChars)
        {
            return new MemoryRecall(false, MemoryCodes.MemoryQueryTooLong, "The query is limited to " + MemoryOptions.MaxQueryChars.ToString(CultureInfo.InvariantCulture) + " characters.", []);
        }

        var command = _dataSource.CreateCommand(RecallSql());
        await using (command.ConfigureAwait(false))
        {
            command.Parameters.Add(Account(scope.AccountId));
            command.Parameters.Add(NullableText("project_id", scope.ProjectId));
            command.Parameters.Add(new NpgsqlParameter("use_account", NpgsqlDbType.Boolean) { Value = scope.UseAccountMemory });
            command.Parameters.Add(new NpgsqlParameter("query", NpgsqlDbType.Text) { Value = string.IsNullOrWhiteSpace(query) ? DBNull.Value : query });
            command.Parameters.Add(new NpgsqlParameter("k", NpgsqlDbType.Integer) { Value = k < 1 ? 1 : k });
            return new MemoryRecall(true, MemoryCodes.Ok, null, await ReadEntriesAsync(command, cancellationToken).ConfigureAwait(false));
        }
    }

    /// <inheritdoc />
    public async Task<MemoryMutation> DeleteAsync(MemoryScope scope, string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if ((await GetSettingsAsync(scope.AccountId, cancellationToken).ConfigureAwait(false)).Paused)
        {
            return Notice(MemoryCodes.MemoryPaused, "Memory is paused. Entries are kept, and nothing was read or written.");
        }

        var command = _dataSource.CreateCommand(
            """
            DELETE FROM cowork_memory_entries
            WHERE account_id = @account_id AND id = @id
              AND (
                    (@project_id IS NULL AND project_id IS NULL)
                    OR project_id = @project_id
                    OR (@use_account AND @project_id IS NOT NULL AND project_id IS NULL)
                  )
            """);
        await using (command.ConfigureAwait(false))
        {
            command.Parameters.Add(Account(scope.AccountId));
            command.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Text) { Value = id });
            command.Parameters.Add(NullableText("project_id", scope.ProjectId));
            command.Parameters.Add(new NpgsqlParameter("use_account", NpgsqlDbType.Boolean) { Value = scope.UseAccountMemory });
            var deleted = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return deleted == 1
                ? new MemoryMutation(true, MemoryCodes.Ok, null, null)
                : new MemoryMutation(false, MemoryCodes.MemoryNotFound, "That memory entry was not found.", null);
        }
    }

    /// <inheritdoc />
    public async Task<MemoryMutation> ResetAsync(string accountId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);
        var command = _dataSource.CreateCommand("DELETE FROM cowork_memory_entries WHERE account_id = @account_id");
        await using (command.ConfigureAwait(false))
        {
            command.Parameters.Add(Account(accountId));
            _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return new MemoryMutation(true, MemoryCodes.Ok, null, null);
        }
    }

    /// <inheritdoc />
    public async Task<MemoryExport> ExportAsync(string accountId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);
        var command = _dataSource.CreateCommand(
            """
            SELECT id, account_id, project_id, topic, content, category, source_session, created_at, updated_at
            FROM cowork_memory_entries WHERE account_id = @account_id ORDER BY updated_at DESC
            """);
        await using (command.ConfigureAwait(false))
        {
            command.Parameters.Add(Account(accountId));
            var records = (await ReadEntriesAsync(command, cancellationToken).ConfigureAwait(false)).Select(MemorySearch.ToRecord).ToList();
            var json = JsonSerializer.Serialize(new MemoryExportDocument { Entries = records }, MemoryJsonContext.Default.MemoryExportDocument);
            return new MemoryExport(true, MemoryCodes.Ok, null, json);
        }
    }

    private async Task<MemoryMutation?> CapAsync(MemoryScope scope, CancellationToken cancellationToken)
    {
        var command = _dataSource.CreateCommand(
            """
            SELECT
                (SELECT count(*) FROM cowork_memory_entries WHERE account_id = @account_id),
                (SELECT count(*) FROM cowork_memory_entries WHERE account_id = @account_id AND source_session = @session_id AND created_at >= @since)
            """);
        await using (command.ConfigureAwait(false))
        {
            command.Parameters.Add(Account(scope.AccountId));
            command.Parameters.Add(new NpgsqlParameter("session_id", NpgsqlDbType.Text) { Value = scope.SessionId });
            command.Parameters.Add(new NpgsqlParameter("since", NpgsqlDbType.TimestampTz) { Value = _timeProvider.GetUtcNow().AddHours(-1) });
            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                _ = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
                if (reader.GetInt64(1) >= MemoryOptions.MaxSavesPerSessionPerHour)
                {
                    return Notice(MemoryCodes.MemoryRateLimited, "This was not saved. This session has used its 20 saves for the hour.");
                }

                if (reader.GetInt64(0) >= MemoryOptions.MaxEntriesPerAccount)
                {
                    return Notice(MemoryCodes.MemoryCapReached, "This was not saved. This account has 1000 memory entries.");
                }
            }
        }

        return null;
    }

    private async Task<MemoryMutation> InsertAsync(MemoryScope scope, MemoryDraft draft, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var id = Guid.NewGuid().ToString("n");
        var command = _dataSource.CreateCommand(
            """
            INSERT INTO cowork_memory_entries (id, account_id, project_id, topic, content, category, source_session, created_at, updated_at)
            VALUES (@id, @account_id, @project_id, @topic, @content, @category, @session_id, @now, @now)
            """);
        try
        {
            await using (command.ConfigureAwait(false))
            {
                command.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Text) { Value = id });
                command.Parameters.Add(Account(scope.AccountId));
                command.Parameters.Add(NullableText("project_id", scope.ProjectId));
                command.Parameters.Add(NullableText("topic", draft.Topic));
                command.Parameters.Add(new NpgsqlParameter("content", NpgsqlDbType.Text) { Value = draft.Content });
                command.Parameters.Add(NullableText("category", draft.Category));
                command.Parameters.Add(new NpgsqlParameter("session_id", NpgsqlDbType.Text) { Value = scope.SessionId });
                command.Parameters.Add(new NpgsqlParameter("now", NpgsqlDbType.TimestampTz) { Value = now });
                _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (NpgsqlException)
        {
            return new MemoryMutation(false, MemoryCodes.Offline, "Memory could not be updated.", null);
        }

        return new MemoryMutation(true, MemoryCodes.Ok, null, new MemoryEntry
        {
            Id = id,
            AccountId = scope.AccountId,
            ProjectId = scope.ProjectId,
            Topic = draft.Topic,
            Content = draft.Content,
            Category = draft.Category,
            SourceSession = scope.SessionId,
            CreatedAt = now,
            UpdatedAt = now,
        });
    }

    private static async Task<List<MemoryEntry>> ReadEntriesAsync(NpgsqlCommand command, CancellationToken cancellationToken)
    {
        var entries = new List<MemoryEntry>();
        var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        await using (reader.ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                entries.Add(new MemoryEntry
                {
                    Id = reader.GetString(0),
                    AccountId = reader.GetString(1),
                    ProjectId = await reader.IsDBNullAsync(2, cancellationToken).ConfigureAwait(false) ? null : reader.GetString(2),
                    Topic = await reader.IsDBNullAsync(3, cancellationToken).ConfigureAwait(false) ? null : reader.GetString(3),
                    Content = reader.GetString(4),
                    Category = await reader.IsDBNullAsync(5, cancellationToken).ConfigureAwait(false) ? null : reader.GetString(5),
                    SourceSession = await reader.IsDBNullAsync(6, cancellationToken).ConfigureAwait(false) ? null : reader.GetString(6),
                    CreatedAt = await reader.GetFieldValueAsync<DateTimeOffset>(7, cancellationToken).ConfigureAwait(false),
                    UpdatedAt = await reader.GetFieldValueAsync<DateTimeOffset>(8, cancellationToken).ConfigureAwait(false),
                });
            }
        }

        return entries;
    }

    private static string RecallSql() =>
        $"""
        SELECT id, account_id, project_id, topic, content, category, source_session, created_at, updated_at
        FROM cowork_memory_entries
        WHERE account_id = @account_id
          AND (
                (@project_id IS NULL AND project_id IS NULL)
                OR project_id = @project_id
                OR (@use_account AND @project_id IS NOT NULL AND project_id IS NULL)
              )
          AND (@query IS NULL OR {SearchVectorExpression} @@ plainto_tsquery('simple', @query))
        ORDER BY updated_at DESC
        LIMIT @k
        """;

    private MemoryMutation? Refuse(MemoryScope scope, MemoryDraft draft, MemorySettings settings)
    {
        _ = scope;
        if (settings.Paused)
        {
            return Notice(MemoryCodes.MemoryPaused, "Memory is paused. Entries are kept, and nothing was read or written.");
        }

        var limit = MemoryContentPolicy.LimitSentence(draft.Topic, draft.Content);
        if (limit is not null)
        {
            return Notice(MemoryCodes.MemoryCapReached, limit);
        }

        var decision = MemoryContentPolicy.Evaluate(draft.Topic, draft.Content, draft.Category, settings.IncludeSensitive);
        if (decision.Verdict == MemoryPolicyVerdict.Allowed)
        {
            return null;
        }

        var code = decision.Verdict == MemoryPolicyVerdict.RefusedNeverSave ? MemoryCodes.MemoryRefusedNeverSave : MemoryCodes.MemoryRefusedSensitive;
        return Notice(code, decision.Sentence!);
    }

    private MemoryMutation Notice(string code, string sentence)
    {
        _notices?.Report(new MemoryNotice(code, sentence));
        return new MemoryMutation(false, code, sentence, null);
    }

    private static NpgsqlParameter Account(string accountId) => new("account_id", NpgsqlDbType.Text) { Value = accountId };

    private static NpgsqlParameter NullableText(string name, string? value) =>
        new(name, NpgsqlDbType.Text) { Value = string.IsNullOrEmpty(value) ? DBNull.Value : value };
}
