using System.Globalization;
using System.Text;
using System.Text.Json;
using Autonogrammer.Contracts.Artifacts;

using Autonogrammer.Cowork.Artifacts.Model;
using Npgsql;
using NpgsqlTypes;
using ArtifactWireJsonContext = Autonogrammer.Cowork.Artifacts.Stores.ArtifactWireContract.JsonContext;
namespace Autonogrammer.Cowork.Artifacts.Stores;

/// <summary>
/// Account store over the M0006 tables. It takes a data source from the host, writes no migration, and filters every query by account id.
/// </summary>
public sealed class PostgresArtifactStore : IArtifactStore, IArtifactGrantStore, IDisposable
{
    private const string HeaderSql = "SELECT a.id, a.title, a.kind, a.derived_from, v.session_id, v.media_type, v.permissions::text FROM artifacts a LEFT JOIN artifact_versions v ON v.artifact_id = a.id AND v.version = 1 AND v.account_id = a.account_id WHERE a.id = @id AND a.account_id = @account_id";
    private const string VersionsSql = "SELECT artifact_id, version, content_sha256, parent_number, parent_content_sha256, author, change_note, created_at, content, permissions::text FROM artifact_versions WHERE artifact_id = @artifact_id AND account_id = @account_id ORDER BY version";
    private readonly NpgsqlDataSource _dataSource;
    private readonly string _accountId;
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>Initializes a new instance of the <see cref="PostgresArtifactStore"/> class.</summary>
    /// <param name="dataSource">The host data source. Its search path selects the schema.</param>
    /// <param name="accountId">The account every query filters on.</param>
    public PostgresArtifactStore(NpgsqlDataSource dataSource, string accountId)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);
        _dataSource = dataSource;
        _accountId = accountId;
    }

    /// <summary>Gets the account this store filters on.</summary>
    public string AccountId => _accountId;

    /// <summary>Releases the mutation gate.</summary>
    public void Dispose() => _gate.Dispose();

    /// <inheritdoc />
    public async ValueTask<T> MutateAsync<T>(Func<IArtifactMutation, T> mutation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        cancellationToken.ThrowIfCancellationRequested();
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    // One transaction-scoped lock per account serializes every writer across store instances and API requests, so the usage read below stays true until commit.
                    using (var gate = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtextextended('cowork-artifacts:' || @account_id, 0))", connection, transaction))
                    {
                        gate.Parameters.AddWithValue("account_id", _accountId);
                        _ = await gate.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                    }

                    var usage = await ReadUsageAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
                    var session = new Session(_accountId, connection, usage);
                    var result = mutation(session);

                    foreach (var change in session.Changes)
                    {
                        await change(connection, transaction, cancellationToken).ConfigureAwait(false);
                    }

                    await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                    return result;
                }
                finally
                {
                    await transaction.DisposeAsync().ConfigureAwait(false);
                }
            }
            finally
            {
                await connection.DisposeAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            _gate.Release();
        }
    }
    /// <inheritdoc />
    public async ValueTask<ArtifactRecord?> GetArtifactAsync(string artifactId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(artifactId);
        cancellationToken.ThrowIfCancellationRequested();
        _ = await GetVersionsAsync(artifactId, cancellationToken).ConfigureAwait(false);
        var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await ReadHeaderAsync(connection, artifactId, _accountId, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<ArtifactVersionRecord>> GetVersionsAsync(string artifactId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(artifactId);
        cancellationToken.ThrowIfCancellationRequested();
        var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var versions = await ReadVersionsAsync(connection, artifactId, cancellationToken).ConfigureAwait(false);
            Verify(versions);
            return versions;
        }
        finally
        {
            await connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<ArtifactGrantBinding>> ListAsync(string artifactId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(artifactId);
        cancellationToken.ThrowIfCancellationRequested();
        var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var command = new NpgsqlCommand("SELECT artifact_id, version, content_sha256, origins::text, scope, device, granted_at FROM artifact_grants WHERE artifact_id = @artifact_id AND account_id = @account_id ORDER BY version", connection);
            command.Parameters.AddWithValue("artifact_id", artifactId);
            command.Parameters.AddWithValue("account_id", _accountId);
            var grants = new List<ArtifactGrantBinding>();
            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    grants.Add(ReadGrant(reader));
                }
            }
            finally
            {
                await reader.DisposeAsync().ConfigureAwait(false);
            }

            return grants;
        }
        finally
        {
            await connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async ValueTask UpsertAsync(ArtifactGrantBinding grant, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(grant);
        cancellationToken.ThrowIfCancellationRequested();
        var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var command = new NpgsqlCommand("INSERT INTO artifact_grants (artifact_id, version, account_id, content_sha256, origins, scope, device, granted_at) VALUES (@artifact_id, @version, @account_id, @content_sha256, @origins::jsonb, @scope, @device, @granted_at) ON CONFLICT (artifact_id, version, scope, device) DO UPDATE SET content_sha256 = EXCLUDED.content_sha256, origins = EXCLUDED.origins, granted_at = EXCLUDED.granted_at WHERE artifact_grants.account_id = @account_id", connection);
            AddGrant(command, grant, _accountId);
            _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async ValueTask DeleteAsync(string artifactId, int number, string scope, string device, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(artifactId);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(device);
        cancellationToken.ThrowIfCancellationRequested();
        var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var command = new NpgsqlCommand("DELETE FROM artifact_grants WHERE artifact_id = @artifact_id AND version = @version AND scope = @scope AND device = @device AND account_id = @account_id", connection);
            command.Parameters.AddWithValue("artifact_id", artifactId);
            command.Parameters.AddWithValue("version", number);
            command.Parameters.AddWithValue("scope", scope);
            command.Parameters.AddWithValue("device", device);
            command.Parameters.AddWithValue("account_id", _accountId);
            _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task<ArtifactUsage> ReadUsageAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        using var command = new NpgsqlCommand("SELECT (SELECT count(*) FROM artifacts WHERE account_id = @account_id)::int, (SELECT coalesce(sum(size_bytes), 0) FROM artifact_versions WHERE account_id = @account_id)::bigint", connection, transaction);
        command.Parameters.AddWithValue("account_id", _accountId);
        var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _ = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            return new ArtifactUsage(reader.GetInt32(0), reader.GetInt64(1));
        }
        finally
        {
            await reader.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static ArtifactRecord? ReadHeader(NpgsqlConnection connection, string artifactId, string accountId)
    {
        using var command = new NpgsqlCommand(HeaderSql, connection);
        command.Parameters.AddWithValue("id", artifactId);
        command.Parameters.AddWithValue("account_id", accountId);
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapHeader(reader) : null;
    }

    private static List<ArtifactVersionRecord> ReadVersions(NpgsqlConnection connection, string artifactId, string accountId)
    {
        using var command = new NpgsqlCommand(VersionsSql, connection);
        command.Parameters.AddWithValue("artifact_id", artifactId);
        command.Parameters.AddWithValue("account_id", accountId);
        var versions = new List<ArtifactVersionRecord>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            versions.Add(MapVersion(reader));
        }

        return versions;
    }

    private static async Task<ArtifactRecord?> ReadHeaderAsync(NpgsqlConnection connection, string artifactId, string accountId, CancellationToken cancellationToken)
    {
        using var command = new NpgsqlCommand(HeaderSql, connection);
        command.Parameters.AddWithValue("id", artifactId);
        command.Parameters.AddWithValue("account_id", accountId);
        var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? MapHeader(reader) : null;
        }
        finally
        {
            await reader.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task<List<ArtifactVersionRecord>> ReadVersionsAsync(NpgsqlConnection connection, string artifactId, CancellationToken cancellationToken)
    {
        using var command = new NpgsqlCommand(VersionsSql, connection);
        command.Parameters.AddWithValue("artifact_id", artifactId);
        command.Parameters.AddWithValue("account_id", _accountId);
        var versions = new List<ArtifactVersionRecord>();
        var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                versions.Add(MapVersion(reader));
            }
        }
        finally
        {
            await reader.DisposeAsync().ConfigureAwait(false);
        }

        return versions;
    }

    private static void Verify(List<ArtifactVersionRecord> versions)
    {
        string? previous = null;
        for (var index = 0; index < versions.Count; index++)
        {
            var version = versions[index];
            if (!string.Equals(ArtifactWireContract.Hash(version.Content.Span), version.ContentSha256, StringComparison.Ordinal))
            {
                throw new ArtifactIntegrityException();
            }

            if (index == 0)
            {
                if (version.ParentNumber is not null || version.ParentContentSha256 is not null)
                {
                    throw new ArtifactIntegrityException();
                }
            }
            else if (version.ParentNumber != versions[index - 1].Number || !string.Equals(version.ParentContentSha256, previous, StringComparison.Ordinal))
            {
                throw new ArtifactIntegrityException();
            }

            previous = version.ContentSha256;
        }
    }

    private static ArtifactRecord MapHeader(NpgsqlDataReader reader)
    {
        if (!Enum.TryParse<ArtifactKind>(reader.GetString(2), out var kind))
        {
            throw new ArtifactIntegrityException();
        }

        var session = reader.IsDBNull(4) ? null : reader.GetString(4);
        return new ArtifactRecord(reader.GetString(0), kind, reader.GetString(1), reader.IsDBNull(5) ? string.Empty : reader.GetString(5), ParseLanguage(reader.IsDBNull(6) ? "[]" : reader.GetString(6)), string.IsNullOrEmpty(session) ? null : session, ParseDerived(reader.IsDBNull(3) ? null : reader.GetString(3)));
    }

    private static ArtifactVersionRecord MapVersion(NpgsqlDataReader reader)
    {
        var content = reader.IsDBNull(8) ? [] : reader.GetFieldValue<byte[]>(8);
        var requests = ParseRequests(reader.IsDBNull(9) ? "[]" : reader.GetString(9));
        return new ArtifactVersionRecord(reader.GetString(0), reader.GetInt32(1), content, reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetInt32(3), reader.IsDBNull(4) ? null : reader.GetString(4), Enum.Parse<ArtifactAuthor>(reader.GetString(5)), reader.GetFieldValue<DateTimeOffset>(7), reader.IsDBNull(6) ? null : reader.GetString(6), requests);
    }

    private static ArtifactGrantBinding ReadGrant(NpgsqlDataReader reader) =>
        new(reader.GetString(0), reader.GetInt32(1), reader.GetString(2), JsonSerializer.Deserialize(reader.GetString(3), ArtifactWireJsonContext.Default.ListString) ?? [], reader.GetString(4), reader.GetString(5), reader.GetFieldValue<DateTimeOffset>(6));

    private static void AddGrant(NpgsqlCommand command, ArtifactGrantBinding grant, string accountId)
    {
        command.Parameters.AddWithValue("artifact_id", grant.ArtifactId);
        command.Parameters.AddWithValue("version", grant.Number);
        command.Parameters.AddWithValue("account_id", accountId);
        command.Parameters.AddWithValue("content_sha256", grant.ContentSha256);
        command.Parameters.AddWithValue("origins", JsonSerializer.Serialize(grant.Origins.ToList(), ArtifactWireJsonContext.Default.ListString));
        command.Parameters.AddWithValue("scope", grant.Scope);
        command.Parameters.AddWithValue("device", grant.Device);
        command.Parameters.AddWithValue("granted_at", grant.GrantedAt);
    }

    private static string? ParseLanguage(string permissions)
    {
        if (permissions.Length == 0 || permissions[0] != '{')
        {
            return null;
        }

        using var document = JsonDocument.Parse(permissions);
        return document.RootElement.TryGetProperty("language", out var language) ? language.GetString() : null;
    }

    private static ArtifactDerivation? ParseDerived(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        return new ArtifactDerivation(root.GetProperty("artifactId").GetString() ?? string.Empty, root.GetProperty("version").GetInt32(), root.GetProperty("contentSha256").GetString() ?? string.Empty);
    }

    private static string? DerivedJson(ArtifactDerivation? derived) =>
        derived is null ? null : "{\"artifactId\":\"" + derived.ArtifactId + "\",\"version\":" + derived.Version.ToString(CultureInfo.InvariantCulture) + ",\"contentSha256\":\"" + derived.ContentSha256 + "\"}";

    private static string PermissionsJson(string? language, IReadOnlyList<ArtifactOriginRequest> requests)
    {
        if (string.IsNullOrEmpty(language) && requests.Count == 0)
        {
            return "[]";
        }

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            if (!string.IsNullOrEmpty(language))
            {
                writer.WriteString("language", language);
            }

            if (requests.Count > 0)
            {
                writer.WriteStartArray("requests");
                foreach (var request in requests)
                {
                    writer.WriteStartObject();
                    writer.WriteString("origin", request.Origin);
                    writer.WriteString("reason", request.Reason);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static List<ArtifactOriginRequest> ParseRequests(string permissions)
    {
        if (permissions.Length == 0 || permissions[0] != '{')
        {
            return [];
        }

        using var document = JsonDocument.Parse(permissions);
        if (!document.RootElement.TryGetProperty("requests", out var stored) || stored.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var requests = new List<ArtifactOriginRequest>(stored.GetArrayLength());
        foreach (var entry in stored.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object || !entry.TryGetProperty("origin", out var origin) || origin.ValueKind != JsonValueKind.String)
            {
                throw new ArtifactIntegrityException();
            }

            var reason = entry.TryGetProperty("reason", out var storedReason) && storedReason.ValueKind == JsonValueKind.String ? storedReason.GetString() ?? string.Empty : string.Empty;
            try
            {
                requests.Add(new ArtifactOriginRequest(origin.GetString()!, reason));
            }
            catch (ArgumentException)
            {
                throw new ArtifactIntegrityException();
            }
        }

        return requests;
    }

    private static async Task InsertRowsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string accountId, ArtifactRecord artifact, ArtifactVersionRecord version, CancellationToken cancellationToken)
    {
        using (var header = new NpgsqlCommand("INSERT INTO artifacts (id, account_id, title, kind, derived_from, latest_number, created_at, updated_at) VALUES (@id, @account_id, @title, @kind, @derived_from, @latest_number, @created_at, @updated_at)", connection, transaction))
        {
            header.Parameters.AddWithValue("id", artifact.Id);
            header.Parameters.AddWithValue("account_id", accountId);
            header.Parameters.AddWithValue("title", artifact.Title);
            header.Parameters.AddWithValue("kind", artifact.Kind.ToString());
            header.Parameters.AddWithValue("derived_from", (object?)DerivedJson(artifact.DerivedFrom) ?? DBNull.Value);
            header.Parameters.AddWithValue("latest_number", version.Number);
            header.Parameters.AddWithValue("created_at", version.CreatedAt);
            header.Parameters.AddWithValue("updated_at", version.CreatedAt);
            _ = await header.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await InsertVersionAsync(connection, transaction, accountId, artifact.SessionId, artifact.MediaType, artifact.Language, version, cancellationToken).ConfigureAwait(false);
    }

    private static async Task InsertVersionAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string accountId, string? sessionId, string mediaType, string? language, ArtifactVersionRecord version, CancellationToken cancellationToken)
    {
        using var command = new NpgsqlCommand("INSERT INTO artifact_versions (artifact_id, version, session_id, account_id, content_sha256, size_bytes, media_type, parent_number, parent_content_sha256, author, change_note, permissions, created_at, content) VALUES (@artifact_id, @version, @session_id, @account_id, @content_sha256, @size_bytes, @media_type, @parent_number, @parent_content_sha256, @author, @change_note, @permissions::jsonb, @created_at, @content)", connection, transaction);
        command.Parameters.AddWithValue("artifact_id", version.ArtifactId);
        command.Parameters.AddWithValue("version", version.Number);
        command.Parameters.AddWithValue("session_id", sessionId ?? string.Empty);
        command.Parameters.AddWithValue("account_id", accountId);
        command.Parameters.AddWithValue("content_sha256", version.ContentSha256);
        command.Parameters.AddWithValue("size_bytes", version.Content.Length);
        command.Parameters.AddWithValue("media_type", mediaType);
        command.Parameters.AddWithValue("parent_number", version.ParentNumber.HasValue ? version.ParentNumber.Value : DBNull.Value);
        command.Parameters.AddWithValue("parent_content_sha256", (object?)version.ParentContentSha256 ?? DBNull.Value);
        command.Parameters.AddWithValue("author", version.Author.ToString());
        command.Parameters.AddWithValue("change_note", (object?)version.ChangeNote ?? DBNull.Value);
        command.Parameters.AddWithValue("permissions", PermissionsJson(language, version.Requests));
        command.Parameters.AddWithValue("created_at", version.CreatedAt);
        _ = command.Parameters.Add(new NpgsqlParameter("content", NpgsqlDbType.Bytea) { Value = version.Content.ToArray() });
        _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private sealed class Session : IArtifactMutation
    {
        private readonly string _accountId;
        private readonly NpgsqlConnection _connection;
        private int _count;

        internal Session(string accountId, NpgsqlConnection connection, ArtifactUsage usage)
        {
            _accountId = accountId;
            _connection = connection;
            _count = usage.ArtifactCount;
            Bytes = usage.TotalBytes;
        }

        /// <summary>Gets the artifacts this mutation has touched. An absent artifact maps to null, so it is read once.</summary>
        internal Dictionary<string, ArtifactRecord?> Artifacts { get; } = new(StringComparer.Ordinal);

        internal Dictionary<string, List<ArtifactVersionRecord>> Rows { get; } = new(StringComparer.Ordinal);

        internal long Bytes { get; set; }

        internal List<Func<NpgsqlConnection, NpgsqlTransaction, CancellationToken, Task>> Changes { get; } = [];

        public ArtifactRecord? Find(string artifactId)
        {
            ArgumentNullException.ThrowIfNull(artifactId);
            _ = Versions(artifactId);
            return Artifacts.TryGetValue(artifactId, out var artifact) ? artifact : null;
        }

        public IReadOnlyList<ArtifactVersionRecord> Versions(string artifactId)
        {
            ArgumentNullException.ThrowIfNull(artifactId);
            Load(artifactId);
            if (!Rows.TryGetValue(artifactId, out var versions))
            {
                return [];
            }

            Verify(versions);
            return versions.ToArray();
        }

        public void Insert(ArtifactRecord artifact, ArtifactVersionRecord version)
        {
            ArgumentNullException.ThrowIfNull(artifact);
            ArgumentNullException.ThrowIfNull(version);
            EnsureWritable(version);
            if (!Artifacts.TryGetValue(artifact.Id, out var previous) || previous is null)
            {
                _count++;
            }

            Artifacts[artifact.Id] = artifact;
            Rows[artifact.Id] = [version];
            Bytes += version.Content.Length;
            var accountId = _accountId;
            Changes.Add((connection, transaction, cancellationToken) => InsertRowsAsync(connection, transaction, accountId, artifact, version, cancellationToken));
        }

        public void Append(ArtifactVersionRecord version)
        {
            ArgumentNullException.ThrowIfNull(version);
            EnsureWritable(version);
            var existing = Versions(version.ArtifactId);
            if (existing.Count == 0 || version.Number != existing[^1].Number + 1 || version.ParentNumber != existing[^1].Number || !string.Equals(version.ParentContentSha256, existing[^1].ContentSha256, StringComparison.Ordinal))
            {
                throw new ArtifactIntegrityException();
            }

            Rows[version.ArtifactId].Add(version);
            Bytes += version.Content.Length;
            var artifact = Artifacts[version.ArtifactId] ?? throw new ArtifactIntegrityException();
            var accountId = _accountId;
            Changes.Add(async (connection, transaction, cancellationToken) =>
            {
                await InsertVersionAsync(connection, transaction, accountId, artifact.SessionId, artifact.MediaType, artifact.Language, version, cancellationToken).ConfigureAwait(false);
                using var update = new NpgsqlCommand("UPDATE artifacts SET latest_number = @latest_number, updated_at = @updated_at WHERE id = @id AND account_id = @account_id", connection, transaction);
                update.Parameters.AddWithValue("id", version.ArtifactId);
                update.Parameters.AddWithValue("account_id", accountId);
                update.Parameters.AddWithValue("latest_number", version.Number);
                update.Parameters.AddWithValue("updated_at", version.CreatedAt);
                if (await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) != 1)
                {
                    throw new ArtifactIntegrityException();
                }
            });
        }

        public void Delete(string artifactId)
        {
            ArgumentNullException.ThrowIfNull(artifactId);
            Load(artifactId);

            if (Rows.Remove(artifactId, out var versions))
            {
                Bytes -= versions.Sum(version => version.Content.Length);
            }

            if (Artifacts.TryGetValue(artifactId, out var existing) && existing is not null)
            {
                _count--;
            }

            Artifacts[artifactId] = null;
            var accountId = _accountId;
            Changes.Add(async (connection, transaction, cancellationToken) =>
            {
                using var command = new NpgsqlCommand("DELETE FROM artifacts WHERE id = @id AND account_id = @account_id", connection, transaction);
                command.Parameters.AddWithValue("id", artifactId);
                command.Parameters.AddWithValue("account_id", accountId);
                _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            });
        }

        public ArtifactUsage Usage() => new(_count, Bytes);

        /// <summary>Reads one artifact and its versions the first time a mutation touches it. Nothing else of the account is read.</summary>
        /// <param name="artifactId">The artifact identifier.</param>
        private void Load(string artifactId)
        {
            if (Artifacts.ContainsKey(artifactId))
            {
                return;
            }

            var header = ReadHeader(_connection, artifactId, _accountId);
            Artifacts[artifactId] = header;
            if (header is not null)
            {
                Rows[artifactId] = ReadVersions(_connection, artifactId, _accountId);
            }
        }

        private static void EnsureWritable(ArtifactVersionRecord version)
        {
            if (version.Content.Length > ArtifactWireContract.MaxContentBytes)
            {
                throw new ArtifactQuotaException();
            }

            if (!string.Equals(ArtifactWireContract.Hash(version.Content.Span), version.ContentSha256, StringComparison.Ordinal))
            {
                throw new ArtifactIntegrityException();
            }
        }
    }
}
