using System.Globalization;
using Npgsql;

namespace Autonogrammer.Api.Audit;

/// <summary>
/// The PostgreSQL <c>ledger_outbox</c>. The audit row and the outbox row are inserted in the caller's transaction, after a savepoint, so a ledger-table
/// failure rolls back only the savepoint. The payload is not stored; the row keeps the audit id and the digest. The event body is remembered in this
/// process so the pump can emit the closed field set. After a restart the pump rebuilds that set from <c>dispatch_audit</c> (hash, outcome, item in <c>corr</c>).
/// </summary>
internal sealed class PostgresLedgerOutboxStore : ILedgerOutboxStore
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly Lock _gate = new();
    private readonly Dictionary<long, AuditLedgerEvent> _pending = [];
    private long _depth;

    /// <summary>Initializes a new instance of the <see cref="PostgresLedgerOutboxStore"/> class.</summary>
    /// <param name="dataSource">The runtime data source.</param>
    public PostgresLedgerOutboxStore(NpgsqlDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        _dataSource = dataSource;
    }

    /// <inheritdoc />
    public long Depth => Volatile.Read(ref _depth);

    /// <inheritdoc />
    public async ValueTask EnqueueAsync(AuditLedgerEvent audit, NpgsqlTransaction? transaction, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(audit);
        if (transaction is null)
        {
            var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            await using (connection.ConfigureAwait(false))
            {
                var own = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
                await using (own.ConfigureAwait(false))
                {
                    if (await InsertAsync(own, audit, cancellationToken).ConfigureAwait(false))
                    {
                        await own.CommitAsync(cancellationToken).ConfigureAwait(false);
                    }
                }
            }

            return;
        }

        _ = await InsertAsync(transaction, audit, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<LedgerOutboxItem>> ClaimDueAsync(int limit, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            var command = connection.CreateCommand();
            await using (command.ConfigureAwait(false))
            {
                command.CommandText = """
                    SELECT o.id, o.attempts, o.payload_digest, a.account_id_hash, a.outcome, a.corr
                    FROM ledger_outbox o
                    JOIN dispatch_audit a ON a.id = o.audit_id
                    WHERE o.sent_at IS NULL AND o.next_attempt_at <= @now
                    ORDER BY o.id
                    LIMIT @limit
                    """;
                command.Parameters.AddWithValue("now", now);
                command.Parameters.AddWithValue("limit", limit);
                var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                await using (reader.ConfigureAwait(false))
                {
                    var items = new List<LedgerOutboxItem>();
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        var id = reader.GetInt64(0);
                        var missingItem = await reader.IsDBNullAsync(5, cancellationToken).ConfigureAwait(false);
                        var audit = Remembered(id) ?? new AuditLedgerEvent
                        {
                            Item = missingItem ? id.ToString(CultureInfo.InvariantCulture) : reader.GetString(5),
                            Outcome = reader.GetString(4),
                            Summary = "audit",
                            TenantHash = reader.GetString(3),
                        };
                        if (!string.Equals(SubstrateAuditLedger.Digest(audit), reader.GetString(2), StringComparison.Ordinal) && Remembered(id) is null)
                        {
                            continue;
                        }

                        items.Add(new LedgerOutboxItem(id, reader.GetInt32(1), audit));
                    }

                    Volatile.Write(ref _depth, items.Count);
                    return items;
                }
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask MarkSentAsync(long id, CancellationToken cancellationToken)
    {
        await MarkSentSqlAsync(id, cancellationToken).ConfigureAwait(false);
        lock (_gate)
        {
            _ = _pending.Remove(id);
        }

        if (_depth > 0)
        {
            Interlocked.Decrement(ref _depth);
        }
    }

    /// <inheritdoc />
    public ValueTask ScheduleAsync(long id, int attempts, DateTimeOffset nextAttemptAt, CancellationToken cancellationToken) =>
        new(ScheduleSqlAsync(id, attempts, nextAttemptAt, cancellationToken));

    private async Task<bool> InsertAsync(NpgsqlTransaction transaction, AuditLedgerEvent audit, CancellationToken cancellationToken)
    {
        var connection = transaction.Connection ?? throw new InvalidOperationException("The audit transaction has no connection.");
        var command = connection.CreateCommand();
        await using (command.ConfigureAwait(false))
        {
            command.Transaction = transaction;
            command.CommandText = """
                SAVEPOINT ledger_outbox_enqueue;
                WITH audit AS (
                    INSERT INTO dispatch_audit (account_id_hash, occurred_at, op, outcome, corr)
                    VALUES (@hash, now(), 'autonogrammer.audit.v1', @outcome, @item)
                    RETURNING id
                )
                INSERT INTO ledger_outbox (audit_id, payload_digest, next_attempt_at)
                SELECT id, @digest, now() FROM audit
                RETURNING id
                """;
            command.Parameters.AddWithValue("hash", audit.TenantHash);
            command.Parameters.AddWithValue("outcome", audit.Outcome);
            command.Parameters.AddWithValue("item", audit.Item);
            command.Parameters.AddWithValue("digest", SubstrateAuditLedger.Digest(audit));
            try
            {
                var id = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                if (id is long rowId)
                {
                    lock (_gate)
                    {
                        _pending[rowId] = audit;
                    }

                    Interlocked.Increment(ref _depth);
                }
            }
            catch (Exception exception) when (exception is PostgresException or NpgsqlException)
            {
                var rollback = connection.CreateCommand();
                await using (rollback.ConfigureAwait(false))
                {
                    rollback.Transaction = transaction;
                    rollback.CommandText = "ROLLBACK TO SAVEPOINT ledger_outbox_enqueue";
                    _ = await rollback.ExecuteNonQueryAsync(CancellationToken.None).ConfigureAwait(false);
                }

                return false;
            }
        }

        var release = connection.CreateCommand();
        await using (release.ConfigureAwait(false))
        {
            release.Transaction = transaction;
            release.CommandText = "RELEASE SAVEPOINT ledger_outbox_enqueue";
            _ = await release.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        return true;
    }

    private async Task MarkSentSqlAsync(long id, CancellationToken cancellationToken)
    {
        var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            var command = connection.CreateCommand();
            await using (command.ConfigureAwait(false))
            {
                command.CommandText = "UPDATE ledger_outbox SET sent_at = now() WHERE id = @id";
                command.Parameters.AddWithValue("id", id);
                _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task ScheduleSqlAsync(long id, int attempts, DateTimeOffset nextAttemptAt, CancellationToken cancellationToken)
    {
        var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            var command = connection.CreateCommand();
            await using (command.ConfigureAwait(false))
            {
                command.CommandText = "UPDATE ledger_outbox SET attempts = @attempts, next_attempt_at = @next WHERE id = @id";
                command.Parameters.AddWithValue("id", id);
                command.Parameters.AddWithValue("attempts", attempts);
                command.Parameters.AddWithValue("next", nextAttemptAt);
                _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private AuditLedgerEvent? Remembered(long id)
    {
        lock (_gate)
        {
            return _pending.TryGetValue(id, out var audit) ? audit : null;
        }
    }
}
