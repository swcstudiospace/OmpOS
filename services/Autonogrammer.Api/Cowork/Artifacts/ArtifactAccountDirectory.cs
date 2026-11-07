using System.Collections.Concurrent;
using Autonogrammer.Api.Options;
using Autonogrammer.Contracts.Artifacts;
using Autonogrammer.Cowork.Artifacts;
using Autonogrammer.Cowork.Artifacts.Model;
using Autonogrammer.Cowork.Artifacts.Stores;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Autonogrammer.Api.Cowork.Artifacts;

/// <summary>Opens an artifact store for one account. Another account's identifier reads as absent.</summary>
internal sealed class ArtifactAccountDirectory
{
    private readonly ConcurrentDictionary<string, InMemoryArtifactStore> _memory = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _ids = new(StringComparer.Ordinal);
    private readonly NpgsqlDataSource? _dataSource;
    private readonly bool _postgres;

    /// <summary>Initializes a new instance of the <see cref="ArtifactAccountDirectory"/> class.</summary>
    /// <param name="sessions">The session store selection.</param>
    /// <param name="services">The provider used to open PostgreSQL only when that store is selected.</param>
    public ArtifactAccountDirectory(IOptions<SessionsOptions> sessions, IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(services);
        _postgres = SessionsOptions.IsPostgresStore(sessions.Value.Store);
        _dataSource = _postgres ? services.GetRequiredService<NpgsqlDataSource>() : null;
    }

    /// <summary>Lists headers for one account.</summary>
    /// <param name="accountId">The owner.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The headers.</returns>
    public async Task<IReadOnlyList<ArtifactSummaryDocument>> ListAsync(string accountId, CancellationToken cancellationToken)
    {
        if (_postgres)
        {
            return await ListPostgresAsync(accountId, cancellationToken).ConfigureAwait(false);
        }

        var rows = new List<ArtifactSummaryDocument>();
        if (!_ids.TryGetValue(accountId, out var ids))
        {
            return rows;
        }

        var store = Memory(accountId);
        foreach (var id in ids.Keys)
        {
            var summary = await SummarizeAsync(store, id, cancellationToken).ConfigureAwait(false);
            if (summary is not null)
            {
                rows.Add(summary);
            }
        }

        return rows;
    }

    /// <summary>Finds one header, or null when it is absent for this account.</summary>
    /// <param name="accountId">The owner.</param>
    /// <param name="artifactId">The artifact identifier.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The header, or null.</returns>
    public async Task<ArtifactSummaryDocument?> FindAsync(string accountId, string artifactId, CancellationToken cancellationToken)
    {
        var store = Open(accountId);
        try
        {
            return await SummarizeAsync(store, artifactId, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            DisposeIfPostgres(store);
        }
    }

    /// <summary>Reads one version header.</summary>
    /// <param name="accountId">The owner.</param>
    /// <param name="artifactId">The artifact identifier.</param>
    /// <param name="number">The version number.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The header, or null.</returns>
    public async Task<ArtifactSummaryDocument?> VersionAsync(string accountId, string artifactId, int number, CancellationToken cancellationToken)
    {
        var store = Open(accountId);
        try
        {
            var artifact = await store.GetArtifactAsync(artifactId, cancellationToken).ConfigureAwait(false);
            if (artifact is null)
            {
                return null;
            }

            var versions = await store.GetVersionsAsync(artifactId, cancellationToken).ConfigureAwait(false);
            var version = versions.FirstOrDefault(row => row.Number == number);
            return version is null ? null : new ArtifactSummaryDocument(artifact.Id, artifact.Title, artifact.Kind.ToString(), version.Number, version.ContentSha256);
        }
        finally
        {
            DisposeIfPostgres(store);
        }
    }

    /// <summary>Copies version bytes.</summary>
    /// <param name="accountId">The owner.</param>
    /// <param name="artifactId">The artifact identifier.</param>
    /// <param name="number">The version number.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The bytes, or null.</returns>
    public async Task<byte[]?> ContentAsync(string accountId, string artifactId, int number, CancellationToken cancellationToken)
    {
        var store = Open(accountId);
        try
        {
            var versions = await store.GetVersionsAsync(artifactId, cancellationToken).ConfigureAwait(false);
            var version = versions.FirstOrDefault(row => row.Number == number);
            return version?.Content.ToArray();
        }
        finally
        {
            DisposeIfPostgres(store);
        }
    }

    /// <summary>Stores a version idempotently by hash.</summary>
    /// <param name="accountId">The owner.</param>
    /// <param name="artifactId">The caller-supplied identifier.</param>
    /// <param name="number">The version number.</param>
    /// <param name="kind">The kind.</param>
    /// <param name="content">The bytes. Empty for the metadata-only form.</param>
    /// <param name="hash">The lowercase hex hash.</param>
    /// <param name="limits">The service limits.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The outcome.</returns>
    public async Task<ArtifactPutOutcome> PutAsync(string accountId, string artifactId, int number, ArtifactKind kind, byte[] content, string hash, ArtifactOptions limits, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(limits);
        _ = accountId;

        var store = Open(accountId);
        try
        {
            var outcome = await store.MutateAsync(mutation => Put(mutation, artifactId, number, kind, content, hash, limits), cancellationToken).ConfigureAwait(false);
            if (outcome is ArtifactPutOutcome.Created or ArtifactPutOutcome.SameHash && store is InMemoryArtifactStore)
            {
                _ids.GetOrAdd(accountId, static _ => new ConcurrentDictionary<string, byte>(StringComparer.Ordinal))[artifactId] = 0;
            }

            return outcome;
        }
        finally
        {
            DisposeIfPostgres(store);
        }
    }

    /// <summary>Deletes an artifact the account owns.</summary>
    /// <param name="accountId">The owner.</param>
    /// <param name="artifactId">The artifact identifier.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>True when a row was deleted.</returns>
    public async Task<bool> DeleteAsync(string accountId, string artifactId, CancellationToken cancellationToken)
    {
        var store = Open(accountId);
        try
        {
            var existed = await store.GetArtifactAsync(artifactId, cancellationToken).ConfigureAwait(false) is not null;
            if (!existed)
            {
                return false;
            }

            await store.MutateAsync(
                mutation =>
                {
                    mutation.Delete(artifactId);
                    return true;
                },
                cancellationToken).ConfigureAwait(false);
            if (_ids.TryGetValue(accountId, out var ids))
            {
                _ = ids.TryRemove(artifactId, out _);
            }

            return true;
        }
        finally
        {
            DisposeIfPostgres(store);
        }
    }

    private static ArtifactPutOutcome Put(IArtifactMutation mutation, string artifactId, int number, ArtifactKind kind, byte[] content, string hash, ArtifactOptions limits)
    {
        var existing = mutation.Find(artifactId);
        var versions = mutation.Versions(artifactId);
        var current = versions.FirstOrDefault(row => row.Number == number);
        if (current is not null)
        {
            return string.Equals(current.ContentSha256, hash, StringComparison.Ordinal) ? ArtifactPutOutcome.SameHash : ArtifactPutOutcome.Conflict;
        }

        if (existing is null && number != 1)
        {
            return ArtifactPutOutcome.Missing;
        }

        var usage = mutation.Usage();
        if (existing is null && usage.ArtifactCount >= limits.MaxArtifacts)
        {
            return ArtifactPutOutcome.Quota;
        }

        if (usage.TotalBytes + content.Length > limits.MaxAccountBytes)
        {
            return ArtifactPutOutcome.Quota;
        }

        var createdAt = DateTimeOffset.UtcNow;
        if (existing is null)
        {
            var record = new ArtifactRecord(artifactId, kind, "synced", Media(kind), null, null, null);
            mutation.Insert(record, new ArtifactVersionRecord(artifactId, number, content, hash, null, null, ArtifactAuthor.Agent, createdAt, null));
            return ArtifactPutOutcome.Created;
        }

        var parent = versions.Count == 0 ? null : versions[^1];
        mutation.Append(new ArtifactVersionRecord(artifactId, number, content, hash, parent?.Number, parent?.ContentSha256, ArtifactAuthor.Agent, createdAt, null));
        return ArtifactPutOutcome.Created;
    }

    private async Task<IReadOnlyList<ArtifactSummaryDocument>> ListPostgresAsync(string accountId, CancellationToken cancellationToken)
    {
        var connection = await _dataSource!.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var command = new NpgsqlCommand("SELECT id, title, kind, latest_number FROM artifacts WHERE account_id = @account_id ORDER BY updated_at DESC", connection);
            try
            {
                command.Parameters.AddWithValue("account_id", accountId);
                var rows = new List<ArtifactSummaryDocument>();
                var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        rows.Add(new ArtifactSummaryDocument(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetInt32(3), string.Empty));
                    }
                }
                finally
                {
                    await reader.DisposeAsync().ConfigureAwait(false);
                }

                return rows;
            }
            finally
            {
                await command.DisposeAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            await connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static async Task<ArtifactSummaryDocument?> SummarizeAsync(IArtifactStore store, string artifactId, CancellationToken cancellationToken)
    {
        var artifact = await store.GetArtifactAsync(artifactId, cancellationToken).ConfigureAwait(false);
        if (artifact is null)
        {
            return null;
        }

        var versions = await store.GetVersionsAsync(artifactId, cancellationToken).ConfigureAwait(false);
        var latest = versions.Count == 0 ? null : versions[^1];
        return new ArtifactSummaryDocument(artifact.Id, artifact.Title, artifact.Kind.ToString(), latest?.Number ?? 0, latest?.ContentSha256 ?? string.Empty);
    }

    private IArtifactStore Open(string accountId) => _postgres ? new PostgresArtifactStore(_dataSource!, accountId) : Memory(accountId);

    private InMemoryArtifactStore Memory(string accountId) => _memory.GetOrAdd(accountId, static _ => new InMemoryArtifactStore());

    private void DisposeIfPostgres(IArtifactStore store)
    {
        if (_postgres && store is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private static int Cap(ArtifactOptions limits, ArtifactKind kind) => kind switch
    {
        ArtifactKind.Image => limits.MaxImageBytes,
        ArtifactKind.Svg => limits.MaxSvgBytes,
        ArtifactKind.Mermaid => limits.MaxMermaidBytes,
        _ => limits.MaxTextBytes,
    };

    private static string Media(ArtifactKind kind) => kind switch
    {
        ArtifactKind.Markdown => "text/markdown",
        ArtifactKind.Code => "text/plain",
        ArtifactKind.Html => "text/html",
        ArtifactKind.Svg => "image/svg+xml",
        ArtifactKind.Mermaid => "text/vnd.mermaid",
        ArtifactKind.Csv => "text/csv",
        ArtifactKind.Image => "image/png",
        _ => "application/octet-stream",
    };
}
