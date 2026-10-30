
namespace Autonogrammer.Cowork.Sessions;

/// <summary>
/// Metadata the session record has no field for: project, trusted folders, the memory-off flag and pinned artifact ids.
/// The file is <c>&lt;stateRoot&gt;/cowork/&lt;account&gt;/session-meta.json</c>. Writes are atomic and owner-only.
/// Unknown JSON members are kept. A file over the size cap is an error and is not overwritten.
/// </summary>
public sealed class CoworkSessionMetaStore
{
    /// <summary>The file name inside the account folder.</summary>
    public const string FileName = "session-meta.json";

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
}

