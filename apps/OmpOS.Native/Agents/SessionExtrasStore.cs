using System.Text.Json;

namespace OmpOS.Native.Agents;

/// <summary>
/// The small file beside the session registry that remembers which ssh environment a session runs on and the other roots of its workspace, so a session reopened after a restart is
/// composed the way it was created. A file that is missing, unreadable or not valid reads as empty (every session local and single-root) and is replaced by the next write, never
/// the cause of a failure.
/// </summary>
internal sealed class SessionExtrasStore
{
    /// <summary>The name of the file in the state root.</summary>
    public const string FileName = "session-extras.json";

    private const int MaxFileBytes = 1024 * 1024;

    private readonly string _path;
    private readonly Lock _gate = new();
    private SessionExtrasDocument? _document;

    /// <summary>Creates the store.</summary>
    /// <param name="stateRoot">The state root the file lives in.</param>
    /// <exception cref="ArgumentException"><paramref name="stateRoot"/> is blank.</exception>
    public SessionExtrasStore(string stateRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stateRoot);
        _path = Path.Combine(Path.GetFullPath(stateRoot), FileName);
    }

    /// <summary>Returns what is remembered about a session.</summary>
    /// <param name="sessionId">The session.</param>
    /// <returns>The extras, or <see cref="SessionExtras.Local"/> when nothing is remembered.</returns>
    public SessionExtras Get(string sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        lock (_gate)
        {
            var document = Load();
            return document.Sessions.TryGetValue(sessionId, out var entry)
                ? new SessionExtras(entry.EnvironmentId, [.. entry.AdditionalDirectories ?? []])
                : SessionExtras.Local;
        }
    }

    /// <summary>Remembers the extras of a session. A plain local single-root session removes its entry. A file that cannot be written is ignored: the extras then last until the application ends.</summary>
    /// <param name="sessionId">The session.</param>
    /// <param name="extras">The extras.</param>
    public void Set(string sessionId, SessionExtras extras)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(extras);
        lock (_gate)
        {
            var document = Load();
            if (extras.EnvironmentId is null && extras.AdditionalDirectories.Count == 0)
            {
                _ = document.Sessions.Remove(sessionId);
            }
            else
            {
                document.Sessions[sessionId] = new SessionExtrasEntry
                {
                    EnvironmentId = extras.EnvironmentId,
                    AdditionalDirectories = extras.AdditionalDirectories.Count == 0 ? null : [.. extras.AdditionalDirectories],
                };
            }

            Save(document);
        }
    }

    private SessionExtrasDocument Load()
    {
        if (_document is not null)
        {
            return _document;
        }

        try
        {
            var info = new FileInfo(_path);
            if (info.Exists && info.Length <= MaxFileBytes)
            {
                var text = File.ReadAllText(_path);
                _document = JsonSerializer.Deserialize(text, SessionExtrasJsonContext.Default.SessionExtrasDocument);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            _document = null;
        }

        _document ??= new SessionExtrasDocument();
        _document.Sessions ??= new Dictionary<string, SessionExtrasEntry>(StringComparer.Ordinal);
        return _document;
    }

    private void Save(SessionExtrasDocument document)
    {
        try
        {
            var directory = Path.GetDirectoryName(_path)!;
            SessionEngineSet.EnsurePrivateDirectory(directory);
            var temporary = _path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(document, SessionExtrasJsonContext.Default.SessionExtrasDocument));
            File.Move(temporary, _path, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The extras stay in memory for this run; the next write tries the file again.
        }
    }
}
