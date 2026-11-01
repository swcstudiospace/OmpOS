using Npgsql;

namespace Autonogrammer.Api.Audit;

/// <summary>An in-process outbox for a host that has no PostgreSQL data source, and for tests.</summary>
internal sealed class MemoryLedgerOutboxStore : ILedgerOutboxStore
{
    private readonly Lock _gate = new();
    private readonly List<Row> _rows = [];
    private long _nextId = 1;

    /// <inheritdoc />
    public long Depth
    {
        get
        {
            lock (_gate)
            {
                return _rows.Count(static row => row.SentAt is null);
            }
        }
    }

    /// <inheritdoc />
    public ValueTask EnqueueAsync(AuditLedgerEvent audit, NpgsqlTransaction? transaction, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(audit);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            _rows.Add(new Row(_nextId++, 0, DateTimeOffset.MinValue, null, audit));
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<LedgerOutboxItem>> ClaimDueAsync(int limit, DateTimeOffset now, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            var due = _rows
                .Where(row => row.SentAt is null && row.NextAttemptAt <= now)
                .OrderBy(static row => row.Id)
                .Take(limit)
                .Select(static row => new LedgerOutboxItem(row.Id, row.Attempts, row.Audit))
                .ToArray();
            return ValueTask.FromResult<IReadOnlyList<LedgerOutboxItem>>(due);
        }
    }

    /// <inheritdoc />
    public ValueTask MarkSentAsync(long id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            Replace(id, row => row with { SentAt = DateTimeOffset.UnixEpoch });
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask ScheduleAsync(long id, int attempts, DateTimeOffset nextAttemptAt, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            Replace(id, row => row with { Attempts = attempts, NextAttemptAt = nextAttemptAt });
        }

        return ValueTask.CompletedTask;
    }

    private void Replace(long id, Func<Row, Row> update)
    {
        var index = _rows.FindIndex(row => row.Id == id);
        if (index >= 0)
        {
            _rows[index] = update(_rows[index]);
        }
    }

    private sealed record Row(long Id, int Attempts, DateTimeOffset NextAttemptAt, DateTimeOffset? SentAt, AuditLedgerEvent Audit);
}

