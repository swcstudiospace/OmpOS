using System.Collections.Concurrent;
using System.Text.Json;
using Autonogrammer.Sessions.Postgres;
using Npgsql;

namespace Autonogrammer.Api.Cowork.Artifacts;

/// <summary>Audit events for artifact create, update, grant and revoke. Payloads hold hashes and origins only.</summary>
internal sealed class ArtifactAudit
{
    private static readonly Action<ILogger, string, Exception?> Stored = LoggerMessage.Define<string>(LogLevel.Information, new EventId(7100, "ArtifactStored"), "Artifact {ArtifactId} stored.");
    private static readonly Action<ILogger, string, Exception?> Granted = LoggerMessage.Define<string>(LogLevel.Information, new EventId(7101, "ArtifactGrant"), "Artifact {ArtifactId} grant changed.");
    private static readonly Action<ILogger, string, Exception?> Rejected = LoggerMessage.Define<string>(LogLevel.Information, new EventId(7102, "ArtifactRejected"), "Artifact {ArtifactId} rejected.");

    private readonly ConcurrentQueue<ArtifactAuditEvent> _events = new();
    private readonly ILogger<ArtifactAudit> _logger;
    private readonly NpgsqlDataSource? _dataSource;
    private long _sequence;

    /// <summary>Initializes a new instance of the <see cref="ArtifactAudit"/> class.</summary>
    /// <param name="logger">The module logger. It never receives a ticket or a path.</param>
    /// <param name="dataSource">The optional cowork event store.</param>
    public ArtifactAudit(ILogger<ArtifactAudit> logger, NpgsqlDataSource? dataSource = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
        _dataSource = dataSource;
    }

    /// <summary>Gets the events recorded in this process.</summary>
    public IReadOnlyCollection<ArtifactAuditEvent> Events => _events;

    /// <summary>Records one closed-kind event.</summary>
    /// <param name="kind">One of the four artifact audit kinds.</param>
    /// <param name="accountId">The owner. Stored as the row key, never in the payload.</param>
    /// <param name="artifactId">The artifact identifier. Logged, and used as the event session id.</param>
    /// <param name="contentSha256">The version hash.</param>
    /// <param name="origins">The origins, if any.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes when the in-process record is stored.</returns>
    public async ValueTask WriteAsync(string kind, string accountId, string artifactId, string? contentSha256, IReadOnlyList<string> origins, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(kind);
        ArgumentNullException.ThrowIfNull(artifactId);
        var payload = JsonSerializer.Serialize(new AuditPayload(contentSha256, origins), ArtifactsJsonContext.Default.AuditPayload);
        var seq = Interlocked.Increment(ref _sequence);
        _events.Enqueue(new ArtifactAuditEvent(kind, artifactId, payload));
        if (kind is "artifact.grant" or "artifact.revoke")
        {
            Granted(_logger, artifactId, null);
        }
        else if (kind is "artifact.created" or "artifact.updated")
        {
            Stored(_logger, artifactId, null);
        }
        else
        {
            Rejected(_logger, artifactId, null);
        }

        if (_dataSource is null)
        {
            return;
        }
        var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await AccountAdmission.AdmitSharedAsync(connection, transaction, accountId, cancellationToken).ConfigureAwait(false);
            var command = new NpgsqlCommand("INSERT INTO cowork_events (account_id, session_id, seq, occurred_at, kind, payload) VALUES (@account_id, @session_id, @seq, now(), @kind, @payload::jsonb)", connection, transaction);
            try
            {
                command.Parameters.AddWithValue("account_id", accountId);
                command.Parameters.AddWithValue("session_id", artifactId);
                command.Parameters.AddWithValue("seq", seq);
                command.Parameters.AddWithValue("kind", kind);
                command.Parameters.AddWithValue("payload", payload);
                _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                await command.DisposeAsync().ConfigureAwait(false);
            }
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await connection.DisposeAsync().ConfigureAwait(false);
        }
    }
}
