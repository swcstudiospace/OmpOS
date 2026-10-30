using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Autonogrammer.Contracts.Artifacts;

using Autonogrammer.Cowork.Artifacts.Model;
using ArtifactGrantBinding = Autonogrammer.Cowork.Artifacts.Stores.ArtifactWireContract.Grant;
namespace Autonogrammer.Cowork.Artifacts.Stores;

/// <summary>
/// Authoritative desktop store. Versions are write-once files under a per-account tenant folder. The head is the highest version file.
/// </summary>
public sealed partial class FileArtifactStore : IArtifactStore
{
    private const UnixFileMode OwnerDirectory = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    private const UnixFileMode OwnerFile = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    private readonly string _root;
    private readonly string _accountId;
    private readonly Lock _catalog = new();
    private readonly Dictionary<string, Lock> _artifacts = new(StringComparer.Ordinal);

    /// <summary>Initializes a new instance of the <see cref="FileArtifactStore"/> class.</summary>
    /// <param name="stateRoot">The state directory. Artifacts live in <c>cowork/artifacts</c> beneath it.</param>
    /// <param name="accountId">The account whose tenant folder this store reads and writes.</param>
    public FileArtifactStore(string stateRoot, string accountId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stateRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);
        _root = stateRoot;
        _accountId = accountId;
        EnsureDirectory(TenantDirectory);
        RecoverPartials(TenantDirectory);
    }

    /// <summary>Gets the account this store is scoped to.</summary>
    public string AccountId => _accountId;

    /// <summary>Gets the tenant folder for this account.</summary>
    public string TenantDirectory => Path.Combine(_root, "cowork", "artifacts", TenantName(_accountId));

    /// <summary>Lowercase hex of the first 16 bytes of SHA-256 of the account id.</summary>
    /// <param name="accountId">The account identifier.</param>
    /// <returns>The tenant folder name.</returns>
    public static string TenantName(string accountId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);
        var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(accountId));
        return Convert.ToHexStringLower(hash.AsSpan(0, 16));
    }

    /// <inheritdoc />
    public ValueTask<T> MutateAsync<T>(Func<IArtifactMutation, T> mutation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        cancellationToken.ThrowIfCancellationRequested();
        var session = new Session(this);
        lock (_catalog)
        {
            try
            {
                return ValueTask.FromResult(mutation(session));
            }
            finally
            {
                session.Release();
            }
        }
    }

    /// <inheritdoc />
    public ValueTask<ArtifactRecord?> GetArtifactAsync(string artifactId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(artifactId);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_catalog)
        {
            lock (Gate(artifactId))
            {
                return ValueTask.FromResult(ReadHeader(artifactId, verify: true));
            }
        }
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<ArtifactVersionRecord>> GetVersionsAsync(string artifactId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(artifactId);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_catalog)
        {
            lock (Gate(artifactId))
            {
                return ValueTask.FromResult<IReadOnlyList<ArtifactVersionRecord>>(ReadVersions(artifactId, verify: true));
            }
        }
    }

    /// <summary>Saves a grant beside the artifact. A hard delete removes it with the versions.</summary>
    /// <param name="grant">The grant binding.</param>
    internal void SaveGrant(ArtifactGrantBinding grant)
    {
        ArgumentNullException.ThrowIfNull(grant);
        lock (_catalog)
        {
            lock (Gate(grant.ArtifactId))
            {
                var grants = ReadGrants(grant.ArtifactId).Where(existing => !SameGrant(existing, grant)).Append(grant).ToArray();
                WriteGrants(grant.ArtifactId, grants);
            }
        }
    }

    /// <summary>Lists grants stored for one artifact in this account.</summary>
    /// <param name="artifactId">The artifact identifier.</param>
    /// <returns>The grants.</returns>
    internal IReadOnlyList<ArtifactGrantBinding> Grants(string artifactId)
    {
        ArgumentNullException.ThrowIfNull(artifactId);
        lock (_catalog)
        {
            lock (Gate(artifactId))
            {
                return ReadGrants(artifactId);
            }
        }
    }

    private Lock Gate(string artifactId)
    {
        if (!_artifacts.TryGetValue(artifactId, out var gate))
        {
            gate = new Lock();
            _artifacts.Add(artifactId, gate);
        }

        return gate;
    }

    private string Folder(string artifactId) => Path.Combine(TenantDirectory, artifactId);

    private ArtifactRecord? ReadHeader(string artifactId, bool verify)
    {
        var path = Path.Combine(Folder(artifactId), "artifact.json");
        if (!File.Exists(path))
        {
            return null;
        }

        if (verify)
        {
            _ = ReadVersions(artifactId, verify: true);
        }

        var document = JsonSerializer.Deserialize(File.ReadAllBytes(path), FileJsonContext.Default.HeaderDocument)
            ?? throw new ArtifactIntegrityException();
        if (!Enum.TryParse<ArtifactKind>(document.Kind, out var kind))
        {
            throw new ArtifactIntegrityException();
        }

        ArtifactDerivation? derived = document.DerivedArtifactId is null
            ? null
            : new ArtifactDerivation(document.DerivedArtifactId, document.DerivedVersion ?? 0, document.DerivedHash ?? string.Empty);
        return new ArtifactRecord(document.Id, kind, document.Title, document.MediaType, document.Language, document.SessionId, derived);
    }

    private List<ArtifactVersionRecord> ReadVersions(string artifactId, bool verify)
    {
        var folder = Folder(artifactId);
        if (!Directory.Exists(folder))
        {
            return [];
        }

        RecoverPartials(folder);
        var numbers = Directory.EnumerateFiles(folder, "v*.json")
            .Select(path => ParseNumber(Path.GetFileName(path)))
            .Where(number => number > 0)
            .Order()
            .ToArray();
        var versions = new List<ArtifactVersionRecord>(numbers.Length);
        foreach (var number in numbers)
        {
            versions.Add(ReadVersion(artifactId, number));
        }

        if (verify)
        {
            Verify(versions);
        }

        return versions;
    }

    private ArtifactVersionRecord ReadVersion(string artifactId, int number)
    {
        var stem = "v" + number.ToString("D4", CultureInfo.InvariantCulture);
        var folder = Folder(artifactId);
        var manifestPath = Path.Combine(folder, stem + ".json");
        var contentPath = Path.Combine(folder, stem + ".bin");
        if (!File.Exists(manifestPath) || !File.Exists(contentPath))
        {
            throw new ArtifactIntegrityException();
        }

        var document = JsonSerializer.Deserialize(File.ReadAllBytes(manifestPath), FileJsonContext.Default.ManifestDocument)
            ?? throw new ArtifactIntegrityException();
        if (!Enum.TryParse<ArtifactAuthor>(document.Author, out var author))
        {
            throw new ArtifactIntegrityException();
        }

        var content = File.ReadAllBytes(contentPath);
        return new ArtifactVersionRecord(
            artifactId,
            document.Number,
            content,
            document.ContentSha256,
            document.ParentNumber,
            document.ParentContentSha256,
            author,
            document.CreatedAt,
            document.ChangeNote);
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

    private void WriteHeader(ArtifactRecord artifact, DateTimeOffset createdAt)
    {
        var document = new HeaderDocument
        {
            Id = artifact.Id,
            Kind = artifact.Kind.ToString(),
            Title = artifact.Title,
            MediaType = artifact.MediaType,
            Language = artifact.Language,
            SessionId = artifact.SessionId,
            DerivedArtifactId = artifact.DerivedFrom?.ArtifactId,
            DerivedVersion = artifact.DerivedFrom?.Version,
            DerivedHash = artifact.DerivedFrom?.ContentSha256,
            CreatedAt = createdAt,
        };
        AtomicCreate(Path.Combine(Folder(artifact.Id), "artifact.json"), JsonSerializer.SerializeToUtf8Bytes(document, FileJsonContext.Default.HeaderDocument));
    }

    private void WriteVersion(ArtifactVersionRecord version)
    {
        if (version.Content.Length > ArtifactWireContract.MaxContentBytes)
        {
            throw new ArtifactQuotaException();
        }

        var recomputed = ArtifactWireContract.Hash(version.Content.Span);
        if (!string.Equals(recomputed, version.ContentSha256, StringComparison.Ordinal))
        {
            throw new ArtifactIntegrityException();
        }

        var document = new ManifestDocument
        {
            Number = version.Number,
            ContentSha256 = version.ContentSha256,
            ParentNumber = version.ParentNumber,
            ParentContentSha256 = version.ParentContentSha256,
            Author = version.Author.ToString(),
            CreatedAt = version.CreatedAt,
            ChangeNote = version.ChangeNote,
        };
        var stem = "v" + version.Number.ToString("D4", CultureInfo.InvariantCulture);
        var folder = Folder(version.ArtifactId);
        EnsureDirectory(folder);
        var binFinal = Path.Combine(folder, stem + ".bin");
        var jsonFinal = Path.Combine(folder, stem + ".json");
        if (File.Exists(binFinal) || File.Exists(jsonFinal))
        {
            throw new IOException("A version file already exists.");
        }

        var token = Guid.NewGuid().ToString("N");
        var binTemp = binFinal + "." + token + ".tmp";
        var jsonTemp = jsonFinal + "." + token + ".tmp";
        var movedBin = false;
        try
        {
            WriteNew(binTemp, version.Content.ToArray());
            WriteNew(jsonTemp, JsonSerializer.SerializeToUtf8Bytes(document, FileJsonContext.Default.ManifestDocument));
            File.Move(binTemp, binFinal, overwrite: false);
            movedBin = true;
            File.Move(jsonTemp, jsonFinal, overwrite: false);
        }
        finally
        {
            TryDelete(binTemp);
            TryDelete(jsonTemp);
            if (movedBin && !File.Exists(jsonFinal))
            {
                TryDelete(binFinal);
            }
        }
    }

    private List<ArtifactGrantBinding> ReadGrants(string artifactId)
    {
        var path = Path.Combine(Folder(artifactId), "grants.json");
        if (!File.Exists(path))
        {
            return [];
        }

        return JsonSerializer.Deserialize(File.ReadAllBytes(path), FileJsonContext.Default.ListGrant) ?? [];
    }

    private void WriteGrants(string artifactId, IReadOnlyList<ArtifactGrantBinding> grants)
    {
        EnsureDirectory(Folder(artifactId));
        AtomicCreate(Path.Combine(Folder(artifactId), "grants.json"), JsonSerializer.SerializeToUtf8Bytes(grants.ToList(), FileJsonContext.Default.ListGrant));
    }

    private static bool SameGrant(ArtifactGrantBinding left, ArtifactGrantBinding right) =>
        left.Number == right.Number && string.Equals(left.Scope, right.Scope, StringComparison.Ordinal) && string.Equals(left.Device, right.Device, StringComparison.Ordinal);

    private static void AtomicCreate(string finalPath, byte[] bytes)
    {
        var temporary = finalPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        var replaced = false;
        try
        {
            WriteNew(temporary, bytes);
            File.Move(temporary, finalPath, overwrite: true);
            replaced = true;
        }
        finally
        {
            if (!replaced)
            {
                TryDelete(temporary);
            }
        }
    }

    private static void WriteNew(string path, byte[] bytes)
    {
        var options = new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.Write,
            Share = FileShare.None,
            BufferSize = 4096,
        };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = OwnerFile;
        }

        using var stream = new FileStream(path, options);
        stream.Write(bytes);
        stream.Flush(flushToDisk: true);
    }

    private static void EnsureDirectory(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(path);
            return;
        }

        var existed = Directory.Exists(path);
        Directory.CreateDirectory(path, OwnerDirectory);
        if (existed && new DirectoryInfo(path).LinkTarget is null && File.GetUnixFileMode(path) != OwnerDirectory)
        {
            File.SetUnixFileMode(path, OwnerDirectory);
        }
    }

    private static void RecoverPartials(string folder)
    {
        if (!Directory.Exists(folder))
        {
            return;
        }

        foreach (var path in Directory.EnumerateFiles(folder, "*.tmp"))
        {
            TryDelete(path);
        }

        foreach (var directory in Directory.EnumerateDirectories(folder))
        {
            RecoverPartials(directory);
        }

        foreach (var json in Directory.EnumerateFiles(folder, "v*.json"))
        {
            var bin = Path.ChangeExtension(json, ".bin");
            if (!File.Exists(bin))
            {
                TryDelete(json);
            }
        }

        foreach (var bin in Directory.EnumerateFiles(folder, "v*.bin"))
        {
            var json = Path.ChangeExtension(bin, ".json");
            if (!File.Exists(json))
            {
                TryDelete(bin);
            }
        }
    }

    private static int ParseNumber(string fileName)
    {
        if (fileName.Length < 6 || !fileName.StartsWith('v') || !fileName.EndsWith(".json", StringComparison.Ordinal))
        {
            return 0;
        }

        return int.TryParse(fileName.AsSpan(1, fileName.Length - 6), NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : 0;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    private ArtifactUsage CountUsage()
    {
        if (!Directory.Exists(TenantDirectory))
        {
            return new ArtifactUsage(0, 0);
        }

        var count = 0;
        long bytes = 0;
        foreach (var folder in Directory.EnumerateDirectories(TenantDirectory))
        {
            if (!File.Exists(Path.Combine(folder, "artifact.json")))
            {
                continue;
            }

            count++;
            foreach (var bin in Directory.EnumerateFiles(folder, "v*.bin"))
            {
                bytes += new FileInfo(bin).Length;
            }
        }

        return new ArtifactUsage(count, bytes);
    }

    private sealed class Session : IArtifactMutation
    {
        private readonly FileArtifactStore _store;
        private readonly List<Lock> _held = [];

        internal Session(FileArtifactStore store) => _store = store;

        public ArtifactRecord? Find(string artifactId)
        {
            ArgumentNullException.ThrowIfNull(artifactId);
            Hold(artifactId);
            return _store.ReadHeader(artifactId, verify: true);
        }

        public IReadOnlyList<ArtifactVersionRecord> Versions(string artifactId)
        {
            ArgumentNullException.ThrowIfNull(artifactId);
            Hold(artifactId);
            return _store.ReadVersions(artifactId, verify: true);
        }

        public void Insert(ArtifactRecord artifact, ArtifactVersionRecord version)
        {
            ArgumentNullException.ThrowIfNull(artifact);
            ArgumentNullException.ThrowIfNull(version);
            Hold(artifact.Id);
            EnsureDirectory(_store.Folder(artifact.Id));
            _store.WriteVersion(version);
            _store.WriteHeader(artifact, version.CreatedAt);
            _store.WriteGrants(artifact.Id, []);
        }

        public void Append(ArtifactVersionRecord version)
        {
            ArgumentNullException.ThrowIfNull(version);
            Hold(version.ArtifactId);
            var existing = _store.ReadVersions(version.ArtifactId, verify: true);
            if (existing.Count > 0)
            {
                var parent = existing[^1];
                if (version.ParentNumber != parent.Number || !string.Equals(version.ParentContentSha256, parent.ContentSha256, StringComparison.Ordinal))
                {
                    throw new ArtifactIntegrityException();
                }
            }

            _store.WriteVersion(version);
        }

        public void Delete(string artifactId)
        {
            ArgumentNullException.ThrowIfNull(artifactId);
            Hold(artifactId);
            var folder = _store.Folder(artifactId);
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }

        public ArtifactUsage Usage() => _store.CountUsage();

        internal void Release()
        {
            for (var index = _held.Count - 1; index >= 0; index--)
            {
                _held[index].Exit();
            }

            _held.Clear();
        }

        private void Hold(string artifactId)
        {
            var gate = _store.Gate(artifactId);
            if (_held.Contains(gate))
            {
                return;
            }

            gate.Enter();
            _held.Add(gate);
        }
    }

    private sealed class HeaderDocument
    {
        public required string Id { get; set; }

        public required string Kind { get; set; }

        public required string Title { get; set; }

        public required string MediaType { get; set; }

        public string? Language { get; set; }

        public string? SessionId { get; set; }

        public string? DerivedArtifactId { get; set; }

        public int? DerivedVersion { get; set; }

        public string? DerivedHash { get; set; }

        public required DateTimeOffset CreatedAt { get; set; }
    }

    private sealed class ManifestDocument
    {
        public required int Number { get; set; }

        public required string ContentSha256 { get; set; }

        public int? ParentNumber { get; set; }

        public string? ParentContentSha256 { get; set; }

        public required string Author { get; set; }

        public required DateTimeOffset CreatedAt { get; set; }

        public string? ChangeNote { get; set; }
    }

    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonSerializable(typeof(HeaderDocument))]
    [JsonSerializable(typeof(ManifestDocument))]
    [JsonSerializable(typeof(List<ArtifactWireContract.Grant>))]
    private sealed partial class FileJsonContext : JsonSerializerContext
    {
    }
}
