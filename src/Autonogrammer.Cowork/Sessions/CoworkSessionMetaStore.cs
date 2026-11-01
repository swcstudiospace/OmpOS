using System.Text.Json;

namespace Autonogrammer.Cowork.Sessions;

/// <summary>
/// Metadata the session record has no field for: the session-to-project link, trusted folders, the memory-off flag and pinned artifact ids.
/// The file is <c>&lt;stateRoot&gt;/cowork/&lt;account&gt;/session-meta.json</c>. Writes are atomic and owner-only.
/// Unknown JSON members are kept. A file over the size cap is an error and is not overwritten.
/// Code sessions never gain standing context: this store has no standing-context path.
/// </summary>
public sealed class CoworkSessionMetaStore
{
    /// <summary>The file name inside the account folder.</summary>
    public const string FileName = "session-meta.json";

    /// <summary>Code sessions do not receive standing context. There is no path for it on this store.</summary>
    public const bool CodeSessionsReceiveStandingContext = false;

    /// <summary>The largest file that is read or written, in bytes.</summary>
    public const int MaxFileBytes = 256 * 1024;

    private readonly CoworkJsonFile<CoworkSessionMetaDocument> _file;

    /// <summary>Initializes a new instance of the <see cref="CoworkSessionMetaStore"/> class.</summary>
    /// <param name="stateRoot">The state directory.</param>
    /// <param name="accountFolderKey">The account folder key.</param>
    public CoworkSessionMetaStore(string stateRoot, string accountFolderKey)
    {
        _file = new CoworkJsonFile<CoworkSessionMetaDocument>(
            Path.Combine(CoworkAccountFolder.AccountDirectory(stateRoot, accountFolderKey), FileName),
            MaxFileBytes,
            static () => new CoworkSessionMetaDocument(),
            CoworkSessionMetaJsonContext.Default.CoworkSessionMetaDocument);
    }

    /// <summary>Gets the full path of the file.</summary>
    public string FilePath => _file.FilePath;

    /// <summary>Reads the metadata of one task.</summary>
    /// <param name="sessionId">The task identifier.</param>
    /// <returns>The metadata, or null when the task has none.</returns>
    /// <exception cref="InvalidDataException">The file is unreadable, not valid, or larger than <see cref="MaxFileBytes"/>.</exception>
    public CoworkSessionMeta? Get(string sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        var document = _file.Read();
        return document.Sessions is not null && document.Sessions.TryGetValue(sessionId, out var entry)
            ? ToMeta(entry)
            : null;
    }

    /// <summary>Saves the metadata of one task. Other tasks and unknown members are kept.</summary>
    /// <param name="sessionId">The task identifier.</param>
    /// <param name="meta">The metadata.</param>
    /// <exception cref="InvalidDataException">The file cannot be read or the replacement would exceed <see cref="MaxFileBytes"/>.</exception>
    public void Upsert(string sessionId, CoworkSessionMeta meta)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(meta);
        _file.Update(document =>
        {
            document.Sessions ??= new Dictionary<string, CoworkSessionMetaEntry>(StringComparer.Ordinal);
            document.Sessions[sessionId] = new CoworkSessionMetaEntry
            {
                ProjectId = meta.ProjectId,
                TrustedFolders = [.. meta.TrustedFolders],
                MemoryOff = meta.MemoryOff,
                PinnedArtifactIds = [.. meta.PinnedArtifactIds],
                Extra = document.Sessions.TryGetValue(sessionId, out var existing) ? existing.Extra : null,
            };
        });
    }

    private static CoworkSessionMeta ToMeta(CoworkSessionMetaEntry entry) =>
        new()
        {
            ProjectId = entry.ProjectId,
            TrustedFolders = entry.TrustedFolders ?? [],
            MemoryOff = entry.MemoryOff,
            PinnedArtifactIds = entry.PinnedArtifactIds ?? [],
        };

    /// <summary>The sentence shown when a session grant change applies to the next session.</summary>
    public const string FolderChangeAppliesNextSession = CoworkFolderGrantStore.FolderChangeAppliesNextSession;

    /// <summary>Returns session-scope folder grants. Unknown members stay in the file.</summary>
    /// <param name="sessionId">The task identifier.</param>
    /// <returns>The grants.</returns>
    public IReadOnlyList<CoworkFolderGrant> ListFolderGrants(string sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        var document = _file.Read();
        if (document.Sessions is null || !document.Sessions.TryGetValue(sessionId, out var entry) || entry.Extra is null || !entry.Extra.TryGetValue("folderGrants", out var element) || element.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var grants = new List<CoworkFolderGrant>();
        foreach (var item in element.EnumerateArray())
        {
            var path = item.TryGetProperty("path", out var pathElement) ? pathElement.GetString() : null;
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            var access = item.TryGetProperty("access", out var accessElement) && Enum.TryParse<CoworkFolderAccess>(accessElement.GetString(), out var parsed)
                ? parsed
                : CoworkFolderAccess.ReadOnly;
            grants.Add(new CoworkFolderGrant(path, access, CoworkFolderScope.Session));
        }

        return grants;
    }

    /// <summary>Saves session-scope folder grants. Other members and unknown keys are kept.</summary>
    /// <param name="sessionId">The task identifier.</param>
    /// <param name="grants">The grants.</param>
    /// <returns><see cref="FolderChangeAppliesNextSession"/>.</returns>
    public string SetFolderGrants(string sessionId, IReadOnlyList<CoworkFolderGrant> grants)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(grants);
        _file.Update(document =>
        {
            document.Sessions ??= new Dictionary<string, CoworkSessionMetaEntry>(StringComparer.Ordinal);
            if (!document.Sessions.TryGetValue(sessionId, out var entry))
            {
                entry = new CoworkSessionMetaEntry();
                document.Sessions[sessionId] = entry;
            }

            entry.Extra ??= new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            entry.Extra["folderGrants"] = CoworkFolderGrantJson.Element(grants, CoworkFolderScope.Session);
        });
        return FolderChangeAppliesNextSession;
    }
}
