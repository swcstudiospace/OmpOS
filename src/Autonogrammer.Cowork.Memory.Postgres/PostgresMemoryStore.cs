using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Autonogrammer.Contracts.Cowork.Memory;
using Npgsql;
using NpgsqlTypes;

namespace Autonogrammer.Cowork.Memory.Postgres;

/// <summary>
/// Account memory over the M0009 tables. The host supplies the <c>autonogrammer_app</c> data source. This type writes no migration,
/// runs DML only, and filters every query on <c>account_id</c>. There is no row-level security.
/// </summary>
public sealed class PostgresMemoryStore : IMemoryStore, IMemoryIdempotentStore
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

    // Test seam: simulate loss of acknowledgement after the database has committed.
    internal Func<bool>? SuppressCommitAcknowledgement { get; set; }
    // Test seam: coordinate another database connection after the database-clock sample.
    internal Func<DateTimeOffset, CancellationToken, Task>? AfterExpiryTimeSample { get; set; }

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
        var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var connectionDisposal = connection.ConfigureAwait(false);
        var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using var transactionDisposal = transaction.ConfigureAwait(false);
        await LockAccountAsync(accountId, connection, transaction, cancellationToken).ConfigureAwait(false);
        var command = new NpgsqlCommand(
            """
            INSERT INTO cowork_memory_settings (account_id, enabled, paused, include_sensitive)
            VALUES (@account_id, @enabled, @paused, @include_sensitive)
            ON CONFLICT (account_id) DO UPDATE
            SET enabled = EXCLUDED.enabled, paused = EXCLUDED.paused, include_sensitive = EXCLUDED.include_sensitive
            """, connection, transaction);
        await using var commandDisposal = command.ConfigureAwait(false);
        command.Parameters.Add(Account(accountId));
        command.Parameters.Add(new NpgsqlParameter("enabled", NpgsqlDbType.Boolean) { Value = settings.Enabled });
        command.Parameters.Add(new NpgsqlParameter("paused", NpgsqlDbType.Boolean) { Value = settings.Paused });
        command.Parameters.Add(new NpgsqlParameter("include_sensitive", NpgsqlDbType.Boolean) { Value = settings.IncludeSensitive });
        _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
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

        return await InsertAsync(scope, draft, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<MemoryIdempotentResult> SaveIdempotentAsync(MemoryScope scope, MemoryDraft draft, string key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentException.ThrowIfNullOrWhiteSpace(scope.AccountId, nameof(scope));
        ArgumentNullException.ThrowIfNull(key);
        if (key.Length is < 1 or > 128 || key.Any(static c => c < '!' || c > '~'))
        {
            throw new ArgumentException("The key must contain 1 to 128 visible ASCII characters.", nameof(key));
        }

        try
        {
            var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            await using var connectionDisposal = connection.ConfigureAwait(false);
            var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await using var transactionDisposal = transaction.ConfigureAwait(false);
            return await SaveIdempotentInTransactionAsync(scope, draft, key, Fingerprint(scope, draft), connection, transaction, cancellationToken).ConfigureAwait(false);
        }
        catch (NpgsqlException)
        {
            // A failed commit may have committed. Never retry as an unkeyed write.
            return new(MemoryIdempotentStatus.Unavailable, null);
        }
    }

    private async Task<MemoryIdempotentResult> SaveIdempotentInTransactionAsync(
        MemoryScope scope, MemoryDraft draft, string key, byte[] fingerprint,
        NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        await LockAccountAsync(scope.AccountId, connection, transaction, cancellationToken).ConfigureAwait(false);
        var settings = await ReadSettingsAsync(scope.AccountId, connection, transaction, cancellationToken).ConfigureAwait(false);
        if (settings.Paused)
        {
            return new(MemoryIdempotentStatus.Refused, Notice(MemoryCodes.MemoryPaused, "Memory is paused. Entries are kept, and nothing was read or written."));
        }
        var asOf = await SampleExpiryTimeAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
        if (AfterExpiryTimeSample is { } afterSample)
        {
            await afterSample(asOf, cancellationToken).ConfigureAwait(false);
        }
        await PruneExpiredClaimsAsync(scope.AccountId, asOf, connection, transaction, cancellationToken).ConfigureAwait(false);

        var prior = await FindClaimAsync(scope.AccountId, key, fingerprint, asOf, connection, transaction, cancellationToken).ConfigureAwait(false);
        if (prior is not null)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return prior;
        }

        var refusal = Refuse(scope, draft, settings)
            ?? await CheckWriteScopeAsync(scope, connection, transaction, cancellationToken).ConfigureAwait(false);
        if (refusal is not null)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new(MemoryIdempotentStatus.Refused, refusal);
        }

        var entry = await InsertClaimedEntryAsync(scope, draft, key, fingerprint, connection, transaction, cancellationToken).ConfigureAwait(false);
        try
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Cancellation during COMMIT cannot prove whether the server committed.
            return new(MemoryIdempotentStatus.Unavailable, null);
        }

        return SuppressCommitAcknowledgement?.Invoke() == true
            ? new(MemoryIdempotentStatus.Unavailable, null)
            : new(MemoryIdempotentStatus.Created, new MemoryMutation(true, MemoryCodes.Ok, null, entry));
    }

    private static async Task<DateTimeOffset> SampleExpiryTimeAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        var command = new NpgsqlCommand("SELECT clock_timestamp()", connection, transaction);
        await using var commandDisposal = command.ConfigureAwait(false);
        var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        await using var readerDisposal = reader.ConfigureAwait(false);
        await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        return await reader.GetFieldValueAsync<DateTimeOffset>(0, cancellationToken).ConfigureAwait(false);
    }

    private static async Task PruneExpiredClaimsAsync(
        string accountId, DateTimeOffset asOf, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        var command = new NpgsqlCommand(
            """
            DELETE FROM cowork_memory_idempotency AS claims
            USING (
                SELECT account_id, operation, key
                FROM cowork_memory_idempotency
                WHERE account_id = @account_id AND expires_at <= @as_of
                ORDER BY expires_at
                LIMIT 100
                FOR UPDATE SKIP LOCKED
            ) AS expired
            WHERE claims.account_id = expired.account_id AND claims.operation = expired.operation AND claims.key = expired.key
            """, connection, transaction);
        await using var commandDisposal = command.ConfigureAwait(false);
        command.Parameters.Add(Account(accountId));
        command.Parameters.AddWithValue("as_of", asOf);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<MemoryIdempotentResult?> FindClaimAsync(
        string accountId, string key, byte[] fingerprint, DateTimeOffset asOf, NpgsqlConnection connection,
        NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        var expired = new NpgsqlCommand(
            "DELETE FROM cowork_memory_idempotency WHERE account_id = @account_id AND operation = 'create' AND key = @key AND expires_at <= @as_of", connection, transaction);
        await using (expired.ConfigureAwait(false))
        {
            expired.Parameters.Add(Account(accountId));
            expired.Parameters.AddWithValue("key", key);
            expired.Parameters.AddWithValue("as_of", asOf);
            await expired.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        var claim = new NpgsqlCommand(
            "SELECT fingerprint_version, fingerprint, entry_id FROM cowork_memory_idempotency WHERE account_id = @account_id AND operation = 'create' AND key = @key", connection, transaction);
        string? entryId;
        await using (claim.ConfigureAwait(false))
        {
            claim.Parameters.Add(Account(accountId));
            claim.Parameters.AddWithValue("key", key);
            var reader = await claim.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    return null;
                }

                var digest = await reader.GetFieldValueAsync<byte[]>(1, cancellationToken).ConfigureAwait(false);
                if (reader.GetInt32(0) != 1 || !CryptographicOperations.FixedTimeEquals(digest, fingerprint))
                {
                    return new(MemoryIdempotentStatus.Conflict, null);
                }

                entryId = reader.GetString(2);
            }
        }

        var existing = await ReadEntryAsync(accountId, entryId, connection, transaction, cancellationToken).ConfigureAwait(false);
        return existing is null
            ? new(MemoryIdempotentStatus.Gone, null)
            : new(MemoryIdempotentStatus.Replayed, new MemoryMutation(true, MemoryCodes.Ok, null, existing));
    }

    private async Task<MemoryEntry?> InsertClaimedEntryAsync(
        MemoryScope scope, MemoryDraft draft, string key, byte[] fingerprint,
        NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        var entryId = Guid.NewGuid().ToString("n");
        var insert = new NpgsqlCommand(
            """
            INSERT INTO cowork_memory_entries (id, account_id, project_id, topic, content, category, source_session, created_at, updated_at)
            VALUES (@id, @account_id, @project_id, @topic, @content, @category, @session_id, @now, @now)
            """, connection, transaction);
        await using (insert.ConfigureAwait(false))
        {
            insert.Parameters.AddWithValue("id", entryId);
            insert.Parameters.Add(Account(scope.AccountId));
            insert.Parameters.Add(NullableText("project_id", scope.ProjectId));
            insert.Parameters.Add(NullableText("topic", draft.Topic));
            insert.Parameters.AddWithValue("content", draft.Content);
            insert.Parameters.Add(NullableText("category", draft.Category));
            insert.Parameters.AddWithValue("session_id", scope.SessionId);
            insert.Parameters.Add(new NpgsqlParameter("now", NpgsqlDbType.TimestampTz) { Value = _timeProvider.GetUtcNow() });
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        var entry = await ReadEntryAsync(scope.AccountId, entryId, connection, transaction, cancellationToken).ConfigureAwait(false);
        var complete = new NpgsqlCommand(
            """
            INSERT INTO cowork_memory_idempotency (account_id, operation, key, fingerprint_version, fingerprint, entry_id, created_at, expires_at)
            SELECT @account_id, 'create', @key, 1, @fingerprint, @entry_id, stamp, stamp + interval '24 hours'
            FROM (SELECT clock_timestamp() AS stamp) AS sampled
            """, connection, transaction);
        await using (complete.ConfigureAwait(false))
        {
            complete.Parameters.Add(Account(scope.AccountId));
            complete.Parameters.AddWithValue("key", key);
            complete.Parameters.AddWithValue("fingerprint", fingerprint);
            complete.Parameters.AddWithValue("entry_id", entryId);
            await complete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        return entry;
    }

    // Version 1: UTF-8 strings prefixed by signed big-endian 32-bit byte lengths; -1 is null.
    // Field order: topic, content, category, project, account-memory flag ("1"/"0"), session.
    private static byte[] Fingerprint(MemoryScope scope, MemoryDraft draft)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> length = stackalloc byte[4];
        foreach (var value in new[] { draft.Topic, draft.Content, draft.Category, scope.ProjectId, scope.UseAccountMemory ? "1" : "0", scope.SessionId })
        {
            var bytes = value is null ? null : Encoding.UTF8.GetBytes(value);
            BinaryPrimitives.WriteInt32BigEndian(length, bytes?.Length ?? -1);
            hash.AppendData(length);
            if (bytes is not null) hash.AppendData(bytes);
        }

        return hash.GetHashAndReset();
    }

    private static async Task<MemorySettings> ReadSettingsAsync(string accountId, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        var command = new NpgsqlCommand("SELECT enabled, paused, include_sensitive FROM cowork_memory_settings WHERE account_id = @account_id", connection, transaction);
        await using var commandDisposal = command.ConfigureAwait(false);
        command.Parameters.Add(Account(accountId));
        var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        await using var readerDisposal = reader.ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? new MemorySettings(reader.GetBoolean(0), reader.GetBoolean(1), reader.GetBoolean(2))
            : new MemorySettings(false, false, false);
    }

    private static async Task<MemoryEntry?> ReadEntryAsync(string accountId, string id, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        var command = new NpgsqlCommand(
            "SELECT id, account_id, project_id, topic, content, category, source_session, created_at, updated_at FROM cowork_memory_entries WHERE account_id = @account_id AND id = @id FOR KEY SHARE",
            connection, transaction);
        await using var commandDisposal = command.ConfigureAwait(false);
        command.Parameters.Add(Account(accountId));
        command.Parameters.AddWithValue("id", id);
        var entries = await ReadEntriesAsync(command, cancellationToken).ConfigureAwait(false);
        return entries.Count == 0 ? null : entries[0];
    }

    private static async Task LockAccountAsync(string accountId, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        var gate = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtextextended('cowork-memory:' || @account_id, 0))", connection, transaction);
        await using var gateDisposal = gate.ConfigureAwait(false);
        gate.Parameters.Add(Account(accountId));
        await gate.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
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

    private async Task<MemoryMutation?> CapAsync(MemoryScope scope, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        var command = new NpgsqlCommand(
            """
            SELECT
                (SELECT count(*) FROM cowork_memory_entries WHERE account_id = @account_id),
                (SELECT count(*) FROM cowork_memory_entries WHERE account_id = @account_id AND source_session = @session_id AND created_at >= @since)
            """, connection, transaction);
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
        var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var connectionDisposal = connection.ConfigureAwait(false);
        var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using var transactionDisposal = transaction.ConfigureAwait(false);
        var refused = await CheckWriteScopeAsync(scope, connection, transaction, cancellationToken).ConfigureAwait(false);
        if (refused is not null)
        {
            return refused;
        }

        var command = new NpgsqlCommand(
            """
            INSERT INTO cowork_memory_entries (id, account_id, project_id, topic, content, category, source_session, created_at, updated_at)
            VALUES (@id, @account_id, @project_id, @topic, @content, @category, @session_id, @now, @now)
            """, connection, transaction);
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
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
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

    private async Task<MemoryMutation?> CheckWriteScopeAsync(
        MemoryScope scope,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await LockAccountAsync(scope.AccountId, connection, transaction, cancellationToken).ConfigureAwait(false);

        var capped = await CapAsync(scope, connection, transaction, cancellationToken).ConfigureAwait(false);
        if (capped is not null)
        {
            return capped;
        }

        if (string.IsNullOrEmpty(scope.ProjectId))
        {
            return null;
        }

        var ownership = new NpgsqlCommand(
            "SELECT 1 FROM cowork_projects WHERE id = @id AND account_id = @account_id FOR KEY SHARE", connection, transaction);
        await using var ownershipDisposal = ownership.ConfigureAwait(false);
        ownership.Parameters.AddWithValue("id", scope.ProjectId);
        ownership.Parameters.Add(Account(scope.AccountId));
        return await ownership.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is null
            ? new MemoryMutation(false, MemoryCodes.MemoryNotFound, "That project was not found.", null)
            : null;
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
