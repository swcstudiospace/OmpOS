using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OmpOS.Contracts.Agents;

namespace OmpOS.Harness.AgentLoop.Sessions;

/// <summary>
/// Reads and writes the registry's state file. A write goes to a temporary file in the same directory (created with mode 0600),
/// is flushed to disk and then renamed over the state file, so a reader sees the old document or the new one and never a
/// partial one. A file that is empty, not valid JSON, too large, null-listed or of an unknown schema version is renamed to
/// <c>&lt;file&gt;.quarantined-&lt;utc yyyyMMddTHHmmssZ&gt;</c> and the load yields an empty list. A load never throws.
/// </summary>
internal sealed class AgentSessionStateFile
{
    /// <summary>The schema version this build reads and writes.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>The largest state file that is read, in bytes.</summary>
    public const int MaxFileBytes = 1024 * 1024;

    private static readonly Action<ILogger, string, Exception?> Quarantined = LoggerMessage.Define<string>(
        LogLevel.Warning,
        new EventId(1, "AgentSessionStateQuarantined"),
        "The session state file {FileName} is not valid and was set aside; the registry starts empty.");

    private static readonly Action<ILogger, string, Exception?> QuarantineFailed = LoggerMessage.Define<string>(
        LogLevel.Warning,
        new EventId(2, "AgentSessionStateQuarantineFailed"),
        "The session state file {FileName} is not valid and could not be set aside; the registry starts empty.");

    private static readonly Action<ILogger, string, int, Exception?> EntriesSkipped = LoggerMessage.Define<string, int>(
        LogLevel.Warning,
        new EventId(3, "AgentSessionStateEntriesSkipped"),
        "The session state file {FileName} held {Count} unusable session entries; they were dropped.");

    private static readonly Action<ILogger, string, Exception?> WriteFailed = LoggerMessage.Define<string>(
        LogLevel.Warning,
        new EventId(4, "AgentSessionStateWriteFailed"),
        "The session state file {FileName} could not be written; the in-memory registry is unchanged and the next change retries.");

    private readonly string _path;
    private readonly string _stateRoot;
    private readonly TimeProvider _clock;
    private readonly ILogger _logger;

    /// <summary>Initializes a new instance of the <see cref="AgentSessionStateFile"/> class.</summary>
    /// <param name="path">The full path of the state file.</param>
    /// <param name="stateRoot">The directory under which each session's state directory lives.</param>
    /// <param name="clock">The clock that stamps quarantined files and fills missing times.</param>
    /// <param name="logger">Receives warnings that carry the file name only.</param>
    public AgentSessionStateFile(string path, string stateRoot, TimeProvider clock, ILogger logger)
    {
        _path = path;
        _stateRoot = stateRoot;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>Returns the state directory of a session.</summary>
    /// <param name="id">A valid session identifier.</param>
    /// <returns>The directory path.</returns>
    public string StateDirectoryFor(string id) => Path.Combine(_stateRoot, "sessions", id);

    /// <summary>Loads the records synchronously. A missing file is an empty registry; an invalid file is quarantined first.</summary>
    /// <returns>The records.</returns>
    public List<AgentSessionRecord> Load()
    {
        byte[]? bytes;
        try
        {
            bytes = File.Exists(_path) ? ReadCapped() : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Quarantine();
        }

        return Interpret(bytes);
    }

    /// <summary>Loads the records asynchronously. A missing file is an empty registry; an invalid file is quarantined first.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The records.</returns>
    public async Task<List<AgentSessionRecord>> LoadAsync(CancellationToken cancellationToken)
    {
        byte[]? bytes;
        try
        {
            bytes = File.Exists(_path) ? await ReadCappedAsync(cancellationToken).ConfigureAwait(false) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Quarantine();
        }

        return Interpret(bytes);
    }

    /// <summary>Writes the records atomically. A failed write is logged and reported by the return value, never thrown.</summary>
    /// <param name="records">Every record to persist.</param>
    /// <returns>True when the new document replaced the old one.</returns>
    public bool Write(IEnumerable<AgentSessionRecord> records)
    {
        var temporary = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        var replaced = false;
        try
        {
            EnsureDirectory();
            var document = new AgentSessionDocument
            {
                SchemaVersion = CurrentSchemaVersion,
                Sessions = [.. records.Select(ToEntry)],
            };
            var bytes = JsonSerializer.SerializeToUtf8Bytes(document, AgentSessionJsonContext.Default.AgentSessionDocument);
            var options = new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
                BufferSize = 4096,
            };
            if (!OperatingSystem.IsWindows())
            {
                // The file holds resume references, so only its owner may read or write it.
                options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            }

            using (var stream = new FileStream(temporary, options))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporary, _path, overwrite: true);
            replaced = true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            WriteFailed(_logger, Path.GetFileName(_path), null);
        }
        finally
        {
            if (!replaced)
            {
                DeleteTemporary(temporary);
            }
        }

        return replaced;
    }

    private static AgentSessionEntry ToEntry(AgentSessionRecord record) => new()
    {
        Id = record.Id,
        Title = record.Title,
        Lifecycle = record.Lifecycle.ToString(),
        CreatedAt = record.CreatedAt,
        LastActivity = record.LastActivity,
        Heartbeat = record.Heartbeat,
        GraphId = record.GraphId,
        TaskId = record.TaskId,
        WorkspaceRoot = record.WorkspaceRoot,
        AcpSessionId = record.AcpSessionId,
        ResumeReference = record.ResumeReference,
        Kind = record.Kind.ToString(),
    };

    // A temporary file that cannot be deleted is left behind; the registry reads only its own path, so the file is never used.
    private static void DeleteTemporary(string temporary)
    {
        try
        {
            File.Delete(temporary);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static SessionLifecycle ParseLifecycle(string? text) =>
        Enum.TryParse<SessionLifecycle>(text, ignoreCase: true, out var lifecycle)
        && Enum.IsDefined(lifecycle)
        && !int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
            ? lifecycle
            : SessionLifecycle.Lost;

    // A file written before sessions had a kind, or one with a name this build does not know, describes a chat session.
    private static SessionKind ParseKind(string? text) =>
        Enum.TryParse<SessionKind>(text, ignoreCase: true, out var kind)
        && Enum.IsDefined(kind)
        && !int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
            ? kind
            : SessionKind.Chat;

    private byte[] ReadCapped()
    {
        using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, bufferSize: 4096);
        var buffer = new byte[(int)Math.Min(stream.Length, MaxFileBytes) + 1];
        var total = 0;
        while (total < buffer.Length)
        {
            var read = stream.Read(buffer, total, buffer.Length - total);
            if (read == 0)
            {
                break;
            }

            total += read;
        }

        return buffer.AsSpan(0, total).ToArray();
    }

    private async Task<byte[]> ReadCappedAsync(CancellationToken cancellationToken)
    {
        var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, bufferSize: 4096, FileOptions.Asynchronous);
        await using (stream.ConfigureAwait(false))
        {
            var buffer = new byte[(int)Math.Min(stream.Length, MaxFileBytes) + 1];
            var total = 0;
            while (total < buffer.Length)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(total), cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                total += read;
            }

            return buffer.AsSpan(0, total).ToArray();
        }
    }

    private List<AgentSessionRecord> Interpret(byte[]? bytes)
    {
        if (bytes is null)
        {
            return [];
        }

        AgentSessionDocument? document = null;
        if (bytes.Length is > 0 and <= MaxFileBytes)
        {
            try
            {
                document = JsonSerializer.Deserialize(bytes, AgentSessionJsonContext.Default.AgentSessionDocument);
            }
            catch (JsonException)
            {
                document = null;
            }
        }

        if (document is not { SchemaVersion: CurrentSchemaVersion, Sessions: { } entries })
        {
            return Quarantine();
        }

        var records = new List<AgentSessionRecord>(Math.Min(entries.Count, AgentSessionRegistry.MaxSessions));
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var skipped = 0;
        foreach (var entry in entries)
        {
            if (entry is null
                || !AgentSessionIds.IsValid(entry.Id)
                || records.Count >= AgentSessionRegistry.MaxSessions
                || !seen.Add(entry.Id))
            {
                skipped++;
                continue;
            }

            records.Add(ToRecord(entry, entry.Id));
        }

        if (skipped > 0)
        {
            EntriesSkipped(_logger, Path.GetFileName(_path), skipped, null);
        }

        return records;
    }

    private AgentSessionRecord ToRecord(AgentSessionEntry entry, string id)
    {
        var now = _clock.GetUtcNow();
        var createdAt = entry.CreatedAt ?? now;
        var lastActivity = entry.LastActivity ?? createdAt;
        return new AgentSessionRecord
        {
            Id = id,
            Title = string.IsNullOrWhiteSpace(entry.Title) ? AgentSessionRegistry.DefaultTitle : entry.Title,
            Lifecycle = ParseLifecycle(entry.Lifecycle),
            CreatedAt = createdAt,
            LastActivity = lastActivity,
            GraphId = entry.GraphId,
            TaskId = entry.TaskId,
            WorkspaceRoot = entry.WorkspaceRoot,
            StateDirectory = StateDirectoryFor(id),
            AcpSessionId = entry.AcpSessionId,
            ResumeReference = entry.ResumeReference,
            Heartbeat = entry.Heartbeat ?? lastActivity,
            Kind = ParseKind(entry.Kind),
        };
    }

    private List<AgentSessionRecord> Quarantine()
    {
        var fileName = Path.GetFileName(_path);
        var stamp = _clock.GetUtcNow().UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        var target = _path + ".quarantined-" + stamp;
        for (var attempt = 1; File.Exists(target) || Directory.Exists(target); attempt++)
        {
            target = _path + ".quarantined-" + stamp + "-" + attempt.ToString(CultureInfo.InvariantCulture);
        }

        try
        {
            File.Move(_path, target);
            Quarantined(_logger, fileName, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            QuarantineFailed(_logger, fileName, null);
        }

        return [];
    }

    private void EnsureDirectory()
    {
        var directory = Path.GetDirectoryName(_path);
        if (string.IsNullOrEmpty(directory) || Directory.Exists(directory))
        {
            return;
        }

        // Only the leaf directory created by this call is restricted to its owner. Missing ancestors keep the default mode.
        Directory.CreateDirectory(directory);
        if (!OperatingSystem.IsWindows() && new DirectoryInfo(directory).LinkTarget is null)
        {
            File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }
}
