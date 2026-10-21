using System.Data;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Npgsql;
using OmpOS.Contracts.Serialization;
using OmpOS.Contracts.Sessions;
using OmpOS.Contracts.Streaming;
using OmpOS.Harness.Abstractions;

namespace OmpOS.Sessions.Postgres;

/// <summary>
/// PostgreSQL-backed <see cref="ISessionRepository"/>. Several processes (for example the API and a worker) can share
/// one database: every guarantee the in-memory repository gives within one process also holds across processes.
/// </summary>
/// <remarks>
/// <para>
/// <b>Seq allocation.</b> Every writer of the event log takes the session header row lock before it chooses a Seq:
/// <see cref="AppendAsync"/> with <c>SELECT … FOR UPDATE</c>, and <see cref="SaveAsync"/> with the header upsert. Writers of
/// one session therefore serialize on that row, and Seq is dense and unique under concurrency, from any number of processes.
/// The event table's primary key includes <c>occurred_at</c> (a TimescaleDB hypertable requires it), so the database does
/// not enforce Seq uniqueness itself; this lock is what does.
/// </para>
/// <para>
/// <b>Saves.</b> <see cref="SaveAsync"/> upserts the header, then applies the same prefix rule as the in-memory repository:
/// the stored log is never shrunk, and a log whose Seq prefix diverges from the stored log is rejected with an
/// <see cref="InvalidOperationException"/>, rolling back the header write. Events already stored are never rewritten,
/// because the log is append-only. A longer save also requires each event past the stored log to carry a Seq above the one
/// before it, because the database no longer rejects a repeated Seq.
/// </para>
/// <para>
/// <b>Locks.</b> <see cref="LockSessionAsync"/> takes a session-level PostgreSQL advisory lock on a dedicated unpooled
/// connection, keyed by a stable 64-bit hash of the session id. The lock is exclusive across processes, is not
/// reentrant, and works for a session id that has no header yet.
/// </para>
/// <para>
/// <b>Change notification.</b> This store does not use LISTEN/NOTIFY. A reader that follows a session polls
/// <see cref="ListFromSeqAsync"/> on a short interval. That needs no long-lived listener connection and survives pooled
/// connections and restarts, at the cost of polling latency.
/// </para>
/// </remarks>
public sealed class PostgresSessionRepository : ISessionRepository
{
    private const string InsertEventSql =
        "INSERT INTO session_events (session_id, seq, correlation_id, occurred_at, payload) " +
        "VALUES (@session_id, @seq, @correlation_id, @occurred_at, CAST(@payload AS jsonb))";

    private const string UpsertHeaderSql =
        "INSERT INTO sessions (id, owner_id, created_at, updated_at, status, sandbox_id, messages) " +
        "VALUES (@id, @owner_id, @created_at, @updated_at, @status, @sandbox_id, CAST(@messages AS jsonb)) " +
        "ON CONFLICT (id) DO UPDATE SET owner_id = EXCLUDED.owner_id, created_at = EXCLUDED.created_at, " +
        "updated_at = EXCLUDED.updated_at, status = EXCLUDED.status, sandbox_id = EXCLUDED.sandbox_id, messages = EXCLUDED.messages";

    private const string SelectHeaderSql =
        "SELECT owner_id, created_at, updated_at, status, sandbox_id, CAST(messages AS text) FROM sessions WHERE id = @id";

    private const string SelectEventsSql =
        "SELECT seq, correlation_id, occurred_at, CAST(payload AS text) FROM session_events " +
        "WHERE session_id = @session_id AND seq >= @from_seq ORDER BY seq";

    private const string SelectStoredSeqsSql =
        "SELECT seq FROM session_events WHERE session_id = @session_id ORDER BY seq";

    private const string SessionExistsSql = "SELECT EXISTS (SELECT 1 FROM sessions WHERE id = @id)";

    private const string LockSessionRowSql = "SELECT 1 FROM sessions WHERE id = @id FOR UPDATE";

    private const string NextSeqSql =
        "SELECT COALESCE(MAX(seq), 0) + 1 FROM session_events WHERE session_id = @session_id";

    private const string InsertApprovalSql =
        "INSERT INTO session_tool_approvals (session_id, request_id, tool_name, granted_tools, requested_at, decision, decided_at) " +
        "VALUES (@session_id, @request_id, @tool_name, CAST(@granted_tools AS jsonb), @requested_at, @decision, @decided_at) " +
        "ON CONFLICT (session_id, request_id) DO NOTHING";

    private const string SelectApprovalSql =
        "SELECT tool_name, CAST(granted_tools AS text), requested_at, decision, decided_at FROM session_tool_approvals " +
        "WHERE session_id = @session_id AND request_id = @request_id";

    private const string DecideApprovalSql =
        "UPDATE session_tool_approvals SET decision = @decision, decided_at = @decided_at " +
        "WHERE session_id = @session_id AND request_id = @request_id AND decision IS NULL";

    private const string SelectSessionIdsByStatusSql =
        "SELECT id FROM sessions WHERE status = @status AND updated_at < @updated_before " +
        "ORDER BY id COLLATE \"C\" LIMIT @max_ids";

    /// <summary>The most session identifiers one <see cref="ListSessionIdsByStatusAsync"/> call returns.</summary>
    private const int MaxSessionIdsPerListing = 200;

    private readonly NpgsqlDataSource _dataSource;
    private readonly PostgresSessionConnection _connection;

    /// <summary>Creates a repository over an existing data source. The schema must already exist; see <see cref="PostgresSessionSchema"/>.</summary>
    /// <param name="dataSource">The data source for the session database. Its lifetime is owned by the caller.</param>
    /// <param name="connection">The connection string the store was configured with. Session locks connect with it directly.</param>
    public PostgresSessionRepository(NpgsqlDataSource dataSource, PostgresSessionConnection connection)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        ArgumentNullException.ThrowIfNull(connection);
        _dataSource = dataSource;
        _connection = connection;
    }

    /// <inheritdoc />
    public async Task<Session?> GetAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);

        var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            // Header and events are read in one snapshot, so the pair is consistent even while appends run.
            var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken).ConfigureAwait(false);
            await using (transaction.ConfigureAwait(false))
            {
                var header = await ReadHeaderAsync(connection, transaction, id, cancellationToken).ConfigureAwait(false);
                if (header is null)
                {
                    return null;
                }

                var events = await ReadEventsAsync(connection, transaction, id, 0, cancellationToken).ConfigureAwait(false);
                return header with { Events = events };
            }
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// The upsert takes the session header row lock before the stored log is read, so a save and an append of one session
    /// serialize on that lock, the same lock that orders <see cref="AppendAsync"/>.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// The incoming <see cref="Session.Events"/> neither extend nor agree with the stored log: an event within the common
    /// prefix carries a different <see cref="StreamEvent.Seq"/> than the stored event at the same position, or an event past
    /// the stored log does not carry a Seq above the one before it. The message names the session and the first conflicting
    /// position. The header write is rolled back.
    /// </exception>
    public async Task SaveAsync(Session session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);

        var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await using (transaction.ConfigureAwait(false))
            {
                await UpsertHeaderAsync(connection, transaction, session, cancellationToken).ConfigureAwait(false);
                var stored = await ReadStoredSeqsAsync(connection, transaction, session.Id, cancellationToken).ConfigureAwait(false);
                ThrowOnConflict(session, stored);

                for (var index = stored.Count; index < session.Events.Count; index++)
                {
                    await InsertEventAsync(connection, transaction, session.Id, session.Events[index], cancellationToken).ConfigureAwait(false);
                }

                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// <b>Seq is allocated under the session row lock.</b> The transaction first locks the header row with
    /// <c>SELECT … FOR UPDATE</c>, then reads <c>max(seq) + 1</c> and inserts. The lock is granted only after any concurrent
    /// append or save of the session commits, and the read runs in a statement that starts after the grant, so it sees every
    /// committed Seq. Two events of one session therefore never receive the same Seq. This holds without a unique constraint
    /// on Seq, which a hypertable primary key cannot express. Any new writer of <c>session_events</c> must take the same lock
    /// before it chooses a Seq.
    /// </remarks>
    /// <exception cref="KeyNotFoundException">No session has the given identifier.</exception>
    public async Task AppendAsync(string sessionId, StreamEvent evt, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        ArgumentNullException.ThrowIfNull(evt);

        var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await using (transaction.ConfigureAwait(false))
            {
                await LockSessionRowAsync(connection, transaction, sessionId, cancellationToken).ConfigureAwait(false);
                var seq = await NextSeqAsync(connection, transaction, sessionId, cancellationToken).ConfigureAwait(false);
                await InsertEventAsync(connection, transaction, sessionId, evt with { Seq = seq }, cancellationToken).ConfigureAwait(false);

                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <inheritdoc />
    /// <exception cref="KeyNotFoundException">No session has the given identifier (raised on first enumeration).</exception>
    public async IAsyncEnumerable<StreamEvent> ListFromSeqAsync(
        string sessionId,
        long fromSeq,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        cancellationToken.ThrowIfCancellationRequested();

        // The rows are read before the first yield, so the pooled connection is released before the consumer runs.
        var events = await ReadFromSeqAsync(sessionId, fromSeq, cancellationToken).ConfigureAwait(false);
        foreach (var evt in events)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return evt;
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// The lock is taken on a dedicated unpooled connection and held until the returned value is disposed. A second
    /// caller, in this process or another, waits until then. Do not nest calls for the same session: the lock is not
    /// reentrant and a nested call on the same task would wait forever.
    /// </remarks>
    public async Task<IAsyncDisposable> LockSessionAsync(string sessionId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sessionId);

        var key = PostgresSql.AdvisoryKey(sessionId);
        var connection = await OpenLockConnectionAsync(_connection, cancellationToken).ConfigureAwait(false);
        try
        {
            var command = PostgresSql.Create(connection, null);
            command.CommandText = "SELECT pg_advisory_lock(@key)";
            await using (command.ConfigureAwait(false))
            {
                command.Parameters.AddWithValue("key", key);
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch
        {
            // A cancelled or failed acquisition can race with the grant. Closing the unpooled connection ends its backend, which releases any grant.
            await PostgresSessionLock.CloseConnectionAsync(connection).ConfigureAwait(false);
            throw;
        }

        return new PostgresSessionLock(connection, key);
    }

    /// <summary>
    /// Opens the connection that holds a session lock. It is built from the connection string the store was configured with,
    /// with pooling off, and opened directly, not through the data source, so the password and the authentication settings are
    /// the originals. Closing it ends its server backend, and the server then releases the lock, whether or not an unlock ran.
    /// </summary>
    /// <param name="connection">The configured connection string. It holds credentials and is never logged.</param>
    /// <param name="cancellationToken">Cancels the open.</param>
    /// <returns>The open, unpooled connection. The caller owns it.</returns>
    internal static async Task<NpgsqlConnection> OpenLockConnectionAsync(PostgresSessionConnection connection, CancellationToken cancellationToken)
    {
        var unpooled = new NpgsqlConnectionStringBuilder(connection.ConnectionString) { Pooling = false }.ConnectionString;
        var opened = new NpgsqlConnection(unpooled);
        try
        {
            await opened.OpenAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await opened.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        return opened;
    }

    /// <inheritdoc />
    /// <exception cref="KeyNotFoundException">No session has the given identifier.</exception>
    public async Task<bool> TryAddToolApprovalAsync(string sessionId, ToolApprovalRecord approval, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        ArgumentNullException.ThrowIfNull(approval);

        var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            if (!await SessionExistsAsync(connection, sessionId, cancellationToken).ConfigureAwait(false))
            {
                throw new KeyNotFoundException($"Session '{sessionId}' does not exist.");
            }

            var command = PostgresSql.Create(connection, null);
            command.CommandText = InsertApprovalSql;
            await using (command.ConfigureAwait(false))
            {
                command.Parameters.AddWithValue("session_id", sessionId);
                command.Parameters.AddWithValue("request_id", approval.RequestId);
                command.Parameters.AddWithValue("tool_name", approval.ToolName);
                command.Parameters.AddWithValue("granted_tools", JsonSerializer.Serialize(approval.GrantableTools.ToArray(), PostgresJsonContext.Default.StringArray));
                command.Parameters.AddWithValue("requested_at", approval.RequestedAt);
                command.Parameters.AddWithValue("decision", (object?)approval.Decision?.ToString() ?? DBNull.Value);
                command.Parameters.AddWithValue("decided_at", approval.DecidedAt.HasValue ? approval.DecidedAt.Value : DBNull.Value);

                // ON CONFLICT DO NOTHING leaves an existing request untouched: zero rows means the request id is already taken.
                return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1;
            }
        }
    }

    /// <inheritdoc />
    public async Task<ToolApprovalRecord?> GetToolApprovalAsync(string sessionId, string requestId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        ArgumentNullException.ThrowIfNull(requestId);

        var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            var command = PostgresSql.Create(connection, null);
            command.CommandText = SelectApprovalSql;
            await using (command.ConfigureAwait(false))
            {
                command.Parameters.AddWithValue("session_id", sessionId);
                command.Parameters.AddWithValue("request_id", requestId);

                var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                await using (reader.ConfigureAwait(false))
                {
                    if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        return null;
                    }

                    return new ToolApprovalRecord
                    {
                        SessionId = sessionId,
                        RequestId = requestId,
                        ToolName = reader.GetString(0),
                        GrantableTools = JsonSerializer.Deserialize(reader.GetString(1), PostgresJsonContext.Default.StringArray) ?? [],
                        RequestedAt = await reader.GetFieldValueAsync<DateTimeOffset>(2, cancellationToken).ConfigureAwait(false),
                        Decision = await reader.IsDBNullAsync(3, cancellationToken).ConfigureAwait(false) ? (PermissionDecision?)null : Enum.Parse<PermissionDecision>(reader.GetString(3)),
                        DecidedAt = await reader.IsDBNullAsync(4, cancellationToken).ConfigureAwait(false) ? (DateTimeOffset?)null : await reader.GetFieldValueAsync<DateTimeOffset>(4, cancellationToken).ConfigureAwait(false),
                    };
                }
            }
        }
    }

    /// <inheritdoc />
    public async Task<bool> TryDecideToolApprovalAsync(
        string sessionId,
        string requestId,
        PermissionDecision decision,
        DateTimeOffset decidedAt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        ArgumentNullException.ThrowIfNull(requestId);

        var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            var command = PostgresSql.Create(connection, null);
            command.CommandText = DecideApprovalSql;
            await using (command.ConfigureAwait(false))
            {
                command.Parameters.AddWithValue("session_id", sessionId);
                command.Parameters.AddWithValue("request_id", requestId);
                command.Parameters.AddWithValue("decision", decision.ToString());
                command.Parameters.AddWithValue("decided_at", decidedAt);

                // The conditional UPDATE is the decision: exactly one caller can change a pending row.
                return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1;
            }
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Ids are ordered with the <c>C</c> collation, which is byte order, the same order as ordinal comparison in the
    /// in-memory repository, whatever the database collation.
    /// </remarks>
    public async Task<IReadOnlyList<string>> ListSessionIdsByStatusAsync(SessionStatus status, DateTimeOffset updatedBefore, CancellationToken cancellationToken)
    {
        var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            var command = PostgresSql.Create(connection, null);
            command.CommandText = SelectSessionIdsByStatusSql;
            await using (command.ConfigureAwait(false))
            {
                command.Parameters.AddWithValue("status", status.ToString());
                command.Parameters.AddWithValue("updated_before", updatedBefore);
                command.Parameters.AddWithValue("max_ids", (long)MaxSessionIdsPerListing);

                var ids = new List<string>();
                var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                await using (reader.ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        ids.Add(reader.GetString(0));
                    }
                }

                return ids;
            }
        }
    }

    private static async Task<Session?> ReadHeaderAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string id, CancellationToken cancellationToken)
    {
        var command = PostgresSql.Create(connection, transaction);
        command.CommandText = SelectHeaderSql;
        await using (command.ConfigureAwait(false))
        {
            command.Parameters.AddWithValue("id", id);

            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    return null;
                }

                return new Session
                {
                    Id = id,
                    OwnerId = reader.GetString(0),
                    CreatedAt = await reader.GetFieldValueAsync<DateTimeOffset>(1, cancellationToken).ConfigureAwait(false),
                    UpdatedAt = await reader.GetFieldValueAsync<DateTimeOffset>(2, cancellationToken).ConfigureAwait(false),
                    Status = Enum.Parse<SessionStatus>(reader.GetString(3)),
                    SandboxId = await reader.IsDBNullAsync(4, cancellationToken).ConfigureAwait(false) ? null : reader.GetString(4),
                    Messages = JsonSerializer.Deserialize(reader.GetString(5), PostgresJsonContext.Default.MessageArray) ?? [],
                    Events = [],
                };
            }
        }
    }

    private static async Task<List<StreamEvent>> ReadEventsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string sessionId,
        long fromSeq,
        CancellationToken cancellationToken)
    {
        var events = new List<StreamEvent>();
        var command = PostgresSql.Create(connection, transaction);
        command.CommandText = SelectEventsSql;
        await using (command.ConfigureAwait(false))
        {
            command.Parameters.AddWithValue("session_id", sessionId);
            command.Parameters.AddWithValue("from_seq", fromSeq);

            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    events.Add(ReadEvent(sessionId, reader));
                }
            }
        }

        return events;
    }

    private async Task<List<StreamEvent>> ReadFromSeqAsync(string sessionId, long fromSeq, CancellationToken cancellationToken)
    {
        var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            if (!await SessionExistsAsync(connection, sessionId, cancellationToken).ConfigureAwait(false))
            {
                throw new KeyNotFoundException($"Session '{sessionId}' does not exist.");
            }

            return await ReadEventsAsync(connection, null, sessionId, fromSeq, cancellationToken).ConfigureAwait(false);
        }
    }

    private static StreamEvent ReadEvent(string sessionId, NpgsqlDataReader reader) => new()
    {
        Seq = reader.GetInt64(0),
        SessionId = sessionId,
        CorrelationId = reader.GetString(1),
        Timestamp = reader.GetFieldValue<DateTimeOffset>(2),
        Payload = JsonSerializer.Deserialize(reader.GetString(3), OmpOsJsonContext.Default.StreamEventPayload)
            ?? throw new InvalidOperationException($"Session '{sessionId}' has an event with a null payload."),
    };

    private static async Task UpsertHeaderAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Session session, CancellationToken cancellationToken)
    {
        var command = PostgresSql.Create(connection, transaction);
        command.CommandText = UpsertHeaderSql;
        await using (command.ConfigureAwait(false))
        {
            command.Parameters.AddWithValue("id", session.Id);
            command.Parameters.AddWithValue("owner_id", session.OwnerId);
            command.Parameters.AddWithValue("created_at", session.CreatedAt);
            command.Parameters.AddWithValue("updated_at", session.UpdatedAt);
            command.Parameters.AddWithValue("status", session.Status.ToString());
            command.Parameters.AddWithValue("sandbox_id", (object?)session.SandboxId ?? DBNull.Value);
            command.Parameters.AddWithValue("messages", JsonSerializer.Serialize(session.Messages.ToArray(), PostgresJsonContext.Default.MessageArray));

            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<List<long>> ReadStoredSeqsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string sessionId, CancellationToken cancellationToken)
    {
        var seqs = new List<long>();
        var command = PostgresSql.Create(connection, transaction);
        command.CommandText = SelectStoredSeqsSql;
        await using (command.ConfigureAwait(false))
        {
            command.Parameters.AddWithValue("session_id", sessionId);

            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    seqs.Add(reader.GetInt64(0));
                }
            }
        }

        return seqs;
    }

    /// <summary>
    /// Throws when the incoming log, at least as long as the stored one, disagrees with the stored Seq prefix, or when an
    /// event past the stored log does not carry a Seq above the one before it.
    /// </summary>
    /// <param name="session">The session being saved.</param>
    /// <param name="stored">The Seq values of the stored log, in order.</param>
    /// <exception cref="InvalidOperationException">The incoming log conflicts with the stored log.</exception>
    private static void ThrowOnConflict(Session session, List<long> stored)
    {
        if (session.Events.Count < stored.Count)
        {
            return;
        }

        for (var index = 0; index < stored.Count; index++)
        {
            if (session.Events[index].Seq != stored[index])
            {
                throw new InvalidOperationException(
                    $"Session '{session.Id}' has a conflicting event log: incoming event at index {index} " +
                    $"has Seq {session.Events[index].Seq} but the stored log has Seq {stored[index]}.");
            }
        }

        // The primary key includes occurred_at, so the database accepts a repeated Seq. This check, made under the session
        // header row lock taken by the upsert, keeps the stored log unique in Seq.
        var previous = stored.Count == 0 ? long.MinValue : stored[^1];
        for (var index = stored.Count; index < session.Events.Count; index++)
        {
            var seq = session.Events[index].Seq;
            if (seq <= previous)
            {
                throw new InvalidOperationException(
                    $"Session '{session.Id}' has a conflicting event log: incoming event at index {index} has Seq {seq}, " +
                    $"which is not above the Seq {previous} before it.");
            }

            previous = seq;
        }
    }

    private static async Task InsertEventAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string sessionId, StreamEvent evt, CancellationToken cancellationToken)
    {
        var command = PostgresSql.Create(connection, transaction);
        command.CommandText = InsertEventSql;
        await using (command.ConfigureAwait(false))
        {
            command.Parameters.AddWithValue("session_id", sessionId);
            command.Parameters.AddWithValue("seq", evt.Seq);
            command.Parameters.AddWithValue("correlation_id", evt.CorrelationId);
            command.Parameters.AddWithValue("occurred_at", evt.Timestamp);
            command.Parameters.AddWithValue("payload", JsonSerializer.Serialize(evt.Payload, OmpOsJsonContext.Default.StreamEventPayload));

            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Takes the session row lock that serializes appends and saves of one session.</summary>
    /// <exception cref="KeyNotFoundException">No session row exists.</exception>
    private static async Task LockSessionRowAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string sessionId, CancellationToken cancellationToken)
    {
        var command = PostgresSql.Create(connection, transaction);
        command.CommandText = LockSessionRowSql;
        await using (command.ConfigureAwait(false))
        {
            command.Parameters.AddWithValue("id", sessionId);

            if (await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is null)
            {
                throw new KeyNotFoundException($"Session '{sessionId}' does not exist.");
            }
        }
    }

    private static async Task<long> NextSeqAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string sessionId, CancellationToken cancellationToken)
    {
        var command = PostgresSql.Create(connection, transaction);
        command.CommandText = NextSeqSql;
        await using (command.ConfigureAwait(false))
        {
            command.Parameters.AddWithValue("session_id", sessionId);

            var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    private static async Task<bool> SessionExistsAsync(NpgsqlConnection connection, string sessionId, CancellationToken cancellationToken)
    {
        var command = PostgresSql.Create(connection, null);
        command.CommandText = SessionExistsSql;
        await using (command.ConfigureAwait(false))
        {
            command.Parameters.AddWithValue("id", sessionId);

            return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is true;
        }
    }
}
