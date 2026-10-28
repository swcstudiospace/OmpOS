using System.Globalization;
using System.Text.Json;

namespace Autonogrammer.Workspace.Checkpoints;

/// <summary>
/// The file checkpoints of agent sessions. For every turn it keeps, per file the agent changed, the hash of the content before and after the turn and a copy of the
/// content before, in a size-bounded folder that only its owner can use. <see cref="RewindAsync"/> then puts the files back to the state before a turn.
/// </summary>
/// <remarks>
/// <para>
/// A rewind only restores files. It never changes the conversation, and it never touches a path outside the workspace folders given to the store (symbolic links are
/// resolved first). A file that changed after the agent's last edit is stale: without <c>force</c> the rewind writes nothing and says which files are stale. Each file is
/// written through a temporary file, so a reader never sees half a file; when a write fails the files already written are put back. Line endings (CRLF or LF) and a byte
/// order mark of the file as it is now are kept. Binary files and files over the size limit are recorded by hash only, so a rewind skips them and says so.
/// </para>
/// <para>
/// A successful rewind drops the turns it undid from the timeline and remembers their keys, so the recorder does not record them again from the unchanged transcript.
/// Turn numbers are never reused. The store keeps nothing in memory between calls and serialises its calls.
/// </para>
/// </remarks>
public sealed class CheckpointStore : ICheckpointRecorder, IDisposable
{
    private const int DocumentVersion = 1;
    private const int MaxRewoundKeys = 1000;
    private const int MaxLabelChars = 200;

    private readonly string _directory;
    private readonly CheckpointRoots _roots;
    private readonly CheckpointOptions _options;
    private readonly TimeProvider _clock;
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>Creates a store.</summary>
    /// <param name="stateDirectory">The Autonogrammer state folder. The store keeps its files in <c>checkpoints</c> inside it and creates the folder, owner-only, on the first record.</param>
    /// <param name="workspaceRoots">The workspace folders, the primary first. A relative path the recorder reports is relative to the primary.</param>
    /// <param name="options">The size limits, or <see langword="null"/> for the defaults.</param>
    /// <param name="clock">Stamps turns; the system clock when <see langword="null"/>.</param>
    /// <exception cref="ArgumentException">A path is blank, or there is no workspace folder.</exception>
    /// <exception cref="DirectoryNotFoundException">A workspace folder does not exist.</exception>
    public CheckpointStore(string stateDirectory, IEnumerable<string> workspaceRoots, CheckpointOptions? options = null, TimeProvider? clock = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stateDirectory);
        _directory = Path.Combine(Path.GetFullPath(stateDirectory), "checkpoints");
        _roots = new CheckpointRoots(workspaceRoots);
        _options = options ?? new CheckpointOptions();
        _clock = clock ?? TimeProvider.System;
        if (_options.MaxFileBytes < 1 || _options.MaxSessionBytes < 1L || _options.MaxTurns < 1 || _options.MaxFilesPerTurn < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options, "Every checkpoint size limit must be at least 1.");
        }
    }

    /// <summary>Releases the lock that serialises calls.</summary>
    public void Dispose() => _gate.Dispose();

    /// <inheritdoc />
    public async Task<CheckpointTurn?> RecordTurnAsync(string sessionId, string turnKey, string? label, IReadOnlyList<CheckpointFileChange> changes, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(turnKey);
        ArgumentNullException.ThrowIfNull(changes);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var document = await LoadAsync(sessionId, cancellationToken).ConfigureAwait(false);
            if (document.RewoundKeys.Contains(turnKey, StringComparer.Ordinal))
            {
                return null;
            }

            var blobs = new CheckpointBlobs(SessionDirectory(sessionId));
            var files = await BuildFilesAsync(changes, blobs, cancellationToken).ConfigureAwait(false);
            var existing = document.Turns.FindIndex(turn => string.Equals(turn.TurnKey, turnKey, StringComparison.Ordinal));
            if (files.Count == 0)
            {
                if (existing >= 0)
                {
                    document.Turns.RemoveAt(existing);
                    await SaveAsync(sessionId, document, cancellationToken).ConfigureAwait(false);
                }

                return null;
            }

            var recorded = Upsert(document, existing, turnKey, label, files);
            Trim(document);
            await SaveAsync(sessionId, document, cancellationToken).ConfigureAwait(false);
            return document.Turns.Contains(recorded) ? ToPublic(sessionId, recorded) : null;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Lists the turns of a session that changed files.</summary>
    /// <param name="sessionId">The session.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The turns, oldest first; empty for a session that has none.</returns>
    public async Task<IReadOnlyList<CheckpointTurn>> ListAsync(string sessionId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var document = await LoadAsync(sessionId, cancellationToken).ConfigureAwait(false);
            return [.. document.Turns.OrderBy(static turn => turn.TurnId).Select(turn => ToPublic(sessionId, turn))];
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Works out what a rewind to before a turn would do, without writing anything.</summary>
    /// <param name="sessionId">The session.</param>
    /// <param name="turnId">The turn.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The plan; its file list is empty when the session has no such turn.</returns>
    public async Task<RewindPlan> PlanRewindAsync(string sessionId, int turnId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var document = await LoadAsync(sessionId, cancellationToken).ConfigureAwait(false);
            if (!document.Turns.Exists(turn => turn.TurnId == turnId))
            {
                return new RewindPlan(sessionId, turnId, []);
            }

            var (plan, _) = await RewindPlanner.PlanAsync(document, turnId, _roots, new CheckpointBlobs(SessionDirectory(sessionId)), cancellationToken).ConfigureAwait(false);
            return plan;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Puts the files back to the state before a turn: every file that this turn or a later turn changed.</summary>
    /// <param name="sessionId">The session.</param>
    /// <param name="turnId">The turn whose changes, and those of every later turn, are undone.</param>
    /// <param name="force"><see langword="true"/> overwrites files that changed after the agent's last edit; <see langword="false"/> writes nothing while any file is stale.</param>
    /// <param name="cancellationToken">Cancels the rewind before it writes; a write that has begun is finished or undone.</param>
    /// <returns>How it ended. Nothing is thrown for a stale, refused or unrestorable file.</returns>
    public async Task<RewindResult> RewindAsync(string sessionId, int turnId, bool force, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var document = await LoadAsync(sessionId, cancellationToken).ConfigureAwait(false);
            if (!document.Turns.Exists(turn => turn.TurnId == turnId))
            {
                return new RewindResult(RewindStatus.UnknownTurn, new RewindPlan(sessionId, turnId, []), [], "That checkpoint no longer exists.");
            }

            var blobs = new CheckpointBlobs(SessionDirectory(sessionId));
            var (plan, writes) = await RewindPlanner.PlanAsync(document, turnId, _roots, blobs, cancellationToken).ConfigureAwait(false);
            if (plan.IsBlocked && !force)
            {
                return new RewindResult(RewindStatus.Blocked, plan, [], CheckpointCopy.Blocked(plan.StaleCount));
            }

            var toWrite = writes.Where(write => write.File.Action != RewindFileAction.Stale || force).ToList();
            if (toWrite.Count == 0)
            {
                return new RewindResult(RewindStatus.NothingToDo, plan, [], CheckpointCopy.NothingToDo);
            }

            var failure = await ApplyAsync(toWrite, cancellationToken).ConfigureAwait(false);
            if (failure is not null)
            {
                return new RewindResult(failure.Value.Blocked ? RewindStatus.Blocked : RewindStatus.Failed, plan, [], failure.Value.Message);
            }

            Consume(document, turnId);
            await SaveAsync(sessionId, document, cancellationToken).ConfigureAwait(false);
            var written = toWrite.Select(static write => write.File.Path).ToList();
            return new RewindResult(RewindStatus.Completed, plan, written, CheckpointCopy.Completed(written.Count, plan.SkippedCount));
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Deletes everything the store kept for a session.</summary>
    /// <param name="sessionId">The session.</param>
    /// <param name="cancellationToken">Cancels the wait for the store.</param>
    public async Task DeleteSessionAsync(string sessionId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var directory = SessionDirectory(sessionId);
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private static async Task<(bool Blocked, string Message)?> ApplyAsync(IReadOnlyList<PlannedWrite> writes, CancellationToken cancellationToken)
    {
        var done = new List<(PlannedWrite Write, byte[]? Previous)>();
        try
        {
            foreach (var write in writes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var previous = File.Exists(write.AbsolutePath) ? await File.ReadAllBytesAsync(write.AbsolutePath, cancellationToken).ConfigureAwait(false) : null;
                var hash = previous is null ? null : CheckpointHash.Of(previous);
                if (!string.Equals(hash, write.CurrentHash, StringComparison.Ordinal))
                {
                    await RollBackAsync(done).ConfigureAwait(false);
                    return (true, CheckpointCopy.ChangedWhileRewinding(write.File.Path));
                }

                if (write.File.Deletes)
                {
                    File.Delete(write.AbsolutePath);
                }
                else
                {
                    await CheckpointFiles.WriteWorkspaceFileAsync(write.AbsolutePath, write.Content!, cancellationToken).ConfigureAwait(false);
                }

                done.Add((write, previous));
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or OperationCanceledException)
        {
            await RollBackAsync(done).ConfigureAwait(false);
            if (exception is OperationCanceledException)
            {
                throw;
            }

            return (false, CheckpointCopy.WriteFailed(done.Count, exception.Message));
        }

        return null;
    }

    private static async Task RollBackAsync(List<(PlannedWrite Write, byte[]? Previous)> done)
    {
        for (var index = done.Count - 1; index >= 0; index--)
        {
            var (write, previous) = done[index];
            try
            {
                if (previous is null)
                {
                    File.Delete(write.AbsolutePath);
                }
                else
                {
                    await CheckpointFiles.WriteWorkspaceFileAsync(write.AbsolutePath, previous, CancellationToken.None).ConfigureAwait(false);
                }
            }
            catch (IOException)
            {
                // The file stays as the failed rewind left it; the failure message already tells the user the rewind did not finish.
            }
            catch (UnauthorizedAccessException)
            {
                // The file stays as the failed rewind left it; the failure message already tells the user the rewind did not finish.
            }
        }
    }

    private async Task<List<CheckpointFileDocument>> BuildFilesAsync(IReadOnlyList<CheckpointFileChange> changes, CheckpointBlobs blobs, CancellationToken cancellationToken)
    {
        var files = new List<CheckpointFileDocument>();
        var index = new Dictionary<string, int>(StringComparer.Ordinal);
        var merged = new List<(string Root, string Path, ReadOnlyMemory<byte>? Before, ReadOnlyMemory<byte>? After)>();
        foreach (var change in changes)
        {
            if (!_roots.TryLocate(change.Path, out var root, out var relative))
            {
                continue;
            }

            var key = root + "\0" + relative;
            if (index.TryGetValue(key, out var position))
            {
                merged[position] = (root, relative, merged[position].Before, change.After);
            }
            else if (merged.Count < _options.MaxFilesPerTurn)
            {
                index[key] = merged.Count;
                merged.Add((root, relative, change.Before, change.After));
            }
        }

        var stored = 0L;
        foreach (var (root, relative, before, after) in merged)
        {
            var file = Describe(root, relative, before, after, ref stored);
            if (file is null)
            {
                continue;
            }

            if (file.BeforeBlob is not null)
            {
                await blobs.WriteAsync(file.BeforeBlob, before!.Value, cancellationToken).ConfigureAwait(false);
            }

            files.Add(file);
        }

        return files;
    }

    private CheckpointFileDocument? Describe(string root, string relative, ReadOnlyMemory<byte>? before, ReadOnlyMemory<byte>? after, ref long stored)
    {
        var beforeHash = before is { } b ? CheckpointHash.Of(b.Span) : null;
        var afterHash = after is { } a ? CheckpointHash.Of(a.Span) : null;
        if (string.Equals(beforeHash, afterHash, StringComparison.Ordinal))
        {
            return null;
        }

        var file = new CheckpointFileDocument
        {
            Root = root,
            Path = relative,
            BeforeHash = beforeHash,
            BeforeSize = before?.Length ?? 0,
            AfterHash = afterHash,
            AfterSize = after?.Length ?? 0,
        };
        if (before is not { } content)
        {
            return file;
        }

        if (CheckpointHash.IsBinary(content.Span) || (after is { } changed && CheckpointHash.IsBinary(changed.Span)))
        {
            file.Note = "Binary file: only its hash is kept.";
        }
        else if (content.Length > _options.MaxFileBytes || stored + content.Length > _options.MaxSessionBytes / 2)
        {
            file.Note = string.Create(CultureInfo.InvariantCulture, $"Larger than the {_options.MaxFileBytes} byte limit for one file or for one turn: only its hash is kept.");
        }
        else
        {
            file.BeforeBlob = CheckpointHash.Raw(content.Span);
            stored += content.Length;
        }

        return file;
    }

    private CheckpointTurnDocument Upsert(CheckpointDocument document, int existing, string turnKey, string? label, List<CheckpointFileDocument> files)
    {
        var text = Shorten(label);
        if (existing >= 0)
        {
            var turn = document.Turns[existing];
            turn.Files = files;
            if (text.Length > 0)
            {
                turn.Label = text;
            }

            return turn;
        }

        var created = new CheckpointTurnDocument
        {
            TurnId = document.NextTurnId++,
            TurnKey = turnKey,
            Label = text,
            RecordedAt = _clock.GetUtcNow(),
            Files = files,
        };
        document.Turns.Add(created);
        return created;
    }

    private void Trim(CheckpointDocument document)
    {
        while (document.Turns.Count > 1 && (document.Turns.Count > _options.MaxTurns || StoredBytes(document) > _options.MaxSessionBytes))
        {
            document.Turns.RemoveAt(0);
        }
    }

    private static long StoredBytes(CheckpointDocument document) =>
        document.Turns.SelectMany(static turn => turn.Files).Where(static file => file.BeforeBlob is not null).DistinctBy(static file => file.BeforeBlob, StringComparer.Ordinal).Sum(static file => file.BeforeSize);

    private static void Consume(CheckpointDocument document, int turnId)
    {
        foreach (var turn in document.Turns.Where(turn => turn.TurnId >= turnId))
        {
            document.RewoundKeys.Add(turn.TurnKey);
        }

        document.Turns.RemoveAll(turn => turn.TurnId >= turnId);
        if (document.RewoundKeys.Count > MaxRewoundKeys)
        {
            document.RewoundKeys.RemoveRange(0, document.RewoundKeys.Count - MaxRewoundKeys);
        }
    }

    private static string Shorten(string? label)
    {
        var text = (label ?? string.Empty).ReplaceLineEndings(" ").Trim();
        return text.Length <= MaxLabelChars ? text : text[..MaxLabelChars];
    }

    private static CheckpointTurn ToPublic(string sessionId, CheckpointTurnDocument turn) =>
        new(
            sessionId,
            turn.TurnId,
            turn.TurnKey,
            turn.Label,
            turn.RecordedAt,
            [.. turn.Files.Select(static file => new CheckpointFileEntry(
                file.Root,
                file.Path,
                file.BeforeHash,
                file.AfterHash,
                file.BeforeSize,
                file.AfterSize,
                file.BeforeHash is null || file.BeforeBlob is not null,
                file.Note))]);

    private string SessionDirectory(string sessionId) => Path.Combine(_directory, CheckpointHash.SessionFolder(sessionId));

    private async Task<CheckpointDocument> LoadAsync(string sessionId, CancellationToken cancellationToken)
    {
        var path = Path.Combine(SessionDirectory(sessionId), "index.json");
        try
        {
            if (File.Exists(path))
            {
                var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
                var document = JsonSerializer.Deserialize(bytes, CheckpointJsonContext.Default.CheckpointDocument);
                if (document is { Version: DocumentVersion } && string.Equals(document.SessionId, sessionId, StringComparison.Ordinal))
                {
                    return document;
                }
            }
        }
        catch (JsonException)
        {
            QuarantineDamaged(path);
        }
        catch (IOException)
        {
            // An unreadable timeline is an empty one: recording starts it again and a rewind finds nothing to undo.
        }
        catch (UnauthorizedAccessException)
        {
            // An unreadable timeline is an empty one: recording starts it again and a rewind finds nothing to undo.
        }

        return new CheckpointDocument { Version = DocumentVersion, SessionId = sessionId };
    }

    private static void QuarantineDamaged(string path)
    {
        try
        {
            File.Move(path, path + ".damaged", overwrite: true);
        }
        catch (IOException)
        {
            // The next save replaces the damaged file anyway.
        }
        catch (UnauthorizedAccessException)
        {
            // The next save replaces the damaged file anyway.
        }
    }

    private async Task SaveAsync(string sessionId, CheckpointDocument document, CancellationToken cancellationToken)
    {
        var directory = SessionDirectory(sessionId);
        CheckpointFiles.CreateOwnerDirectory(directory);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(document, CheckpointJsonContext.Default.CheckpointDocument);
        await CheckpointFiles.WriteOwnerFileAsync(Path.Combine(directory, "index.json"), bytes, cancellationToken).ConfigureAwait(false);
        var referenced = document.Turns.SelectMany(static turn => turn.Files).Select(static file => file.BeforeBlob).OfType<string>().ToHashSet(StringComparer.Ordinal);
        new CheckpointBlobs(directory).Prune(referenced);
    }
}
