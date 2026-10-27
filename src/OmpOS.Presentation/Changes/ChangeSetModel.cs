using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using OmpOS.Contracts.Ui;
using OmpOS.Presentation.Editor;
using OmpOS.Presentation.Observable;
using OmpOS.Presentation.State;

namespace OmpOS.Presentation.Changes;

/// <summary>
/// The review of the files an agent changed in one session (ADE-06, ADE-11): Keep and Undo for each file and for all of them, and the
/// review of one file hunk by hunk. The agent's edits are already on disk; keeping leaves them, undoing writes the text from before the
/// agent back. It follows the session's transcript, so a file the agent edits again, or a new file, shows up while the session runs.
/// </summary>
/// <remarks>
/// <para>
/// Every write goes through the workspace file system, and only when the file still holds the text the agent's last edit left. A file
/// that changed since becomes <see cref="ChangeStatus.Stale"/>, nothing is written, and the user is told to review it by hand. A file
/// the agent created is deleted by an undo only under the same condition; a file the agent did not create is never deleted. An expected
/// failure (stale, outside the workspace, unreadable, read-only file system) is a <see cref="ChangeResult"/>, never an exception.
/// </para>
/// <para>
/// A decision holds while the transcript stays as it was: when the agent edits the file again, the file is pending again.
/// </para>
/// </remarks>
public sealed class ChangeSetModel : ObservableModel, IDisposable
{
    private readonly IWorkspaceFileSystem _fileSystem;
    private readonly DiffReviewWriter _writer;
    private readonly ObservableCollection<ChangeEntry> _entries = [];
    private readonly Dictionary<string, Resolution> _resolutions = new(StringComparer.Ordinal);
    private readonly ChangeCommand _keepAllCommand;
    private readonly ChangeCommand _undoAllCommand;
    private IReadOnlyList<UiBlock>? _blocks;
    private SessionViewModel? _session;
    private string _announcement = string.Empty;

    /// <summary>Creates an empty review over a workspace.</summary>
    /// <param name="fileSystem">The workspace the agent edited. Its root is the base of the paths the agent reported.</param>
    /// <exception cref="ArgumentNullException"><paramref name="fileSystem"/> is null.</exception>
    public ChangeSetModel(IWorkspaceFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);

        _fileSystem = fileSystem;
        _writer = new DiffReviewWriter(fileSystem);
        Entries = new ReadOnlyObservableCollection<ChangeEntry>(_entries);
        KeepCommand = new ChangeCommand(path => RunFor(path, static (model, target) => model.Keep(target)));
        UndoCommand = new ChangeCommand(path => RunFor(path, static (model, target) => model.Undo(target)));
        ReviewCommand = new ChangeCommand(path => RunFor(path, static (model, target) => model.Review(target)));
        _keepAllCommand = new ChangeCommand(_ => KeepAll(), () => ActionableCount > 0);
        _undoAllCommand = new ChangeCommand(_ => UndoAll(), () => ActionableCount > 0);
    }

    /// <summary>Occurs when the user asks to review one file. The head shows the review in its Diff review tool.</summary>
    public event EventHandler<ChangeReviewRequestedEventArgs>? ReviewRequested;

    /// <summary>Gets the changed files, in the order the transcript first edited them.</summary>
    public ReadOnlyObservableCollection<ChangeEntry> Entries { get; }

    /// <summary>Gets whether the session changed any file. The Changes bar is hidden while it is false.</summary>
    public bool HasChanges => _entries.Count > 0;

    /// <summary>Gets the number of files that still wait for a decision: pending, or changed on disk since the agent edited them.</summary>
    public int ActionableCount => _entries.Count(static entry => entry.IsActionable);

    /// <summary>Gets the summary line: how many files changed and the lines added and removed in them.</summary>
    public string Summary => ChangeCopy.Summary(_entries.Count, _entries.Sum(static entry => entry.Added), _entries.Sum(static entry => entry.Removed));

    /// <summary>Gets the sentence the last action said. A polite live region reads it out.</summary>
    public string Announcement
    {
        get => _announcement;
        private set => SetProperty(ref _announcement, value);
    }

    /// <summary>Gets the command that keeps one file's changes. Its parameter is the file's workspace-relative path.</summary>
    public ICommand KeepCommand { get; }

    /// <summary>Gets the command that undoes one file's changes. Its parameter is the file's workspace-relative path.</summary>
    public ICommand UndoCommand { get; }

    /// <summary>Gets the command that opens the review of one file. Its parameter is the file's workspace-relative path.</summary>
    public ICommand ReviewCommand { get; }

    /// <summary>Gets the command that keeps every file that waits for a decision.</summary>
    public ICommand KeepAllCommand => _keepAllCommand;

    /// <summary>Gets the command that undoes every file that waits for a decision.</summary>
    public ICommand UndoAllCommand => _undoAllCommand;

    /// <summary>Follows a session: reads its transcript now and again each time the transcript changes.</summary>
    /// <param name="session">The session. A model follows one session at a time.</param>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is null.</exception>
    public void Attach(SessionViewModel session)
    {
        ArgumentNullException.ThrowIfNull(session);

        Detach();
        _session = session;
        session.PropertyChanged += OnSessionChanged;
        Update(session.Tree.Blocks);
    }

    /// <summary>Stops following the session.</summary>
    public void Dispose() => Detach();

    /// <summary>Reads the changes out of a transcript's blocks and brings the entries up to date. Entries whose edits did not change keep their decision.</summary>
    /// <param name="blocks">The transcript's blocks, in order.</param>
    /// <exception cref="ArgumentNullException"><paramref name="blocks"/> is null.</exception>
    public void Update(IReadOnlyList<UiBlock> blocks)
    {
        ArgumentNullException.ThrowIfNull(blocks);

        if (ReferenceEquals(blocks, _blocks))
        {
            return;
        }

        _blocks = blocks;
        var set = SessionChangeSet.Build(blocks, _fileSystem.RootPath);

        var wanted = set.Changes.Select(static change => change.Path).ToHashSet(StringComparer.Ordinal);
        for (var index = _entries.Count - 1; index >= 0; index--)
        {
            if (!wanted.Contains(_entries[index].Path))
            {
                _ = _resolutions.Remove(_entries[index].Path);
                _entries.RemoveAt(index);
            }
        }

        for (var index = 0; index < set.Changes.Count; index++)
        {
            var source = set.Changes[index];
            var (change, status, message) = StateOf(source);
            var existing = FindIndex(source.Path);
            if (existing < 0)
            {
                var created = new ChangeEntry(source, this);
                created.Set(source, change, status, message);
                _entries.Insert(index, created);
                continue;
            }

            if (existing != index)
            {
                _entries.Move(existing, index);
            }

            _entries[index].Set(source, change, status, message);
        }

        Refresh();
    }

    /// <summary>Keeps the agent's changes to one file. Nothing is written: the edits stay on disk.</summary>
    /// <param name="path">The file's workspace-relative path.</param>
    /// <returns>The result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is null.</exception>
    public ChangeResult Keep(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        return Publish(KeepCore(path));
    }

    /// <summary>
    /// Undoes the agent's changes to one file: writes the text from before the agent's first edit back, or deletes the file when the agent
    /// created it, only when the file still holds the text the agent's last edit left.
    /// </summary>
    /// <param name="path">The file's workspace-relative path.</param>
    /// <returns>The result. <see cref="ChangeOutcome.Stale"/> when the file changed since, with nothing written.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is null.</exception>
    public ChangeResult Undo(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        return Publish(UndoCore(path));
    }

    /// <summary>Keeps the changes to every file that waits for a decision.</summary>
    /// <returns>One result per file.</returns>
    public IReadOnlyList<ChangeResult> KeepAll()
    {
        var results = _entries.Where(static entry => entry.IsActionable).ToList().Select(entry => KeepCore(entry.Path)).ToList();
        Announcement = ChangeCopy.KeptAllMessage(results.Count(static result => result.Outcome == ChangeOutcome.Kept));
        return results;
    }

    /// <summary>Undoes the changes to every file that waits for a decision. A file that changed since is left as it is.</summary>
    /// <returns>One result per file.</returns>
    public IReadOnlyList<ChangeResult> UndoAll()
    {
        var results = _entries.Where(static entry => entry.IsActionable).ToList().Select(entry => UndoCore(entry.Path)).ToList();
        var undone = results.Count(static result => result.Outcome == ChangeOutcome.Undone);
        Announcement = ChangeCopy.UndoneAllMessage(undone, results.Count - undone);
        return results;
    }

    /// <summary>
    /// Builds the review of one file's changes: the diff from the text before the agent's first edit to the text the agent left, with every
    /// hunk accepted. Rejecting a hunk reverts it; <see cref="ApplyReview"/> writes the decisions.
    /// </summary>
    /// <param name="path">The file's workspace-relative path.</param>
    /// <returns>The review, or null when no file that waits for a decision has the path.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is null.</exception>
    public DiffReview? CreateReview(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        if (Find(path) is not { IsActionable: true } entry)
        {
            return null;
        }

        var original = entry.Change.OldText ?? string.Empty;
        var review = new DiffReview(original, ChangeDiff.Create(original, entry.Change.NewText));
        for (var index = 0; index < review.Hunks.Count; index++)
        {
            review.Decide(index, HunkDecision.Accept);
        }

        return review;
    }

    /// <summary>Asks for the review of one file: raises <see cref="ReviewRequested"/> with the review <see cref="CreateReview"/> builds.</summary>
    /// <param name="path">The file's workspace-relative path.</param>
    /// <returns>The review that was raised, or null when no file that waits for a decision has the path.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is null.</exception>
    public DiffReview? Review(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        var review = CreateReview(path);
        if (review is null)
        {
            Announcement = ChangeCopy.NotPendingMessage(path);
            return null;
        }

        ReviewRequested?.Invoke(this, new ChangeReviewRequestedEventArgs(path, review));
        return review;
    }

    /// <summary>
    /// Writes the decisions of a review made with <see cref="CreateReview"/>: each accepted hunk stays, each rejected or pending hunk is
    /// reverted, through <see cref="DiffReviewWriter"/>. Nothing is written when the file changed since the agent edited it.
    /// </summary>
    /// <param name="path">The file's workspace-relative path.</param>
    /// <param name="review">The review the user decided.</param>
    /// <returns>The result: kept when every hunk was accepted, undone when none was, partly undone otherwise.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> or <paramref name="review"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="review"/> was not made for this file's changes.</exception>
    public ChangeResult ApplyReview(string path, DiffReview review)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(review);

        return Publish(ApplyReviewCore(path, review));
    }

    // A command's parameter is the file's path; a command asked to run with no path has no file to act on.
    private void RunFor(string? path, Func<ChangeSetModel, string, object?> action)
    {
        if (path is not null)
        {
            _ = action(this, path);
        }
    }

    private ChangeResult ApplyReviewCore(string path, DiffReview review)
    {
        if (Find(path) is not { IsActionable: true } entry)
        {
            return new ChangeResult(path, ChangeOutcome.NotApplicable, ChangeCopy.NotPendingMessage(path));
        }

        var change = entry.Change;
        if (!string.Equals(review.Original, change.OldText ?? string.Empty, StringComparison.Ordinal))
        {
            throw new ArgumentException("The review was not made for this file's changes.", nameof(review));
        }

        var accepted = review.Hunks.Count(static hunk => hunk.Decision == HunkDecision.Accept);
        if (accepted == review.Hunks.Count)
        {
            return KeepCore(path);
        }

        if (accepted == 0)
        {
            // Every hunk is rejected: the whole file goes back, and a file the agent created goes, as with Undo.
            return UndoCore(path);
        }

        DiffWriteStatus written;
        try
        {
            written = _writer.Write(path, review, change.NewText);
        }
        catch (Exception exception) when (IsExpected(exception))
        {
            return Fail(entry, exception);
        }

        if (written == DiffWriteStatus.Stale)
        {
            return Resolve(entry, ChangeStatus.Stale, ChangeOutcome.Stale, ChangeCopy.StaleMessage, change);
        }

        var undone = review.Hunks.Count - accepted;
        return Resolve(
            entry,
            ChangeStatus.Pending,
            ChangeOutcome.PartlyUndone,
            ChangeCopy.PartlyUndoneMessage(path, undone, review.Hunks.Count),
            change.WithNewText(review.Apply()));
    }

    private ChangeResult KeepCore(string path)
    {
        if (Find(path) is not { IsActionable: true } entry)
        {
            return new ChangeResult(path, ChangeOutcome.NotApplicable, ChangeCopy.NotPendingMessage(path));
        }

        return Resolve(entry, ChangeStatus.Kept, ChangeOutcome.Kept, ChangeCopy.KeptMessage(entry.Path), entry.Change);
    }

    private ChangeResult UndoCore(string path)
    {
        if (Find(path) is not { IsActionable: true } entry)
        {
            return new ChangeResult(path, ChangeOutcome.NotApplicable, ChangeCopy.NotPendingMessage(path));
        }

        var change = entry.Change;
        try
        {
            var current = ReadCurrent(entry.Path);
            if (current is null)
            {
                return UndoMissingFile(entry);
            }

            if (!HoldsAgentText(current, change.NewText, out var crlf))
            {
                return Resolve(entry, ChangeStatus.Stale, ChangeOutcome.Stale, ChangeCopy.StaleMessage, change);
            }

            if (change.OldText is null)
            {
                _fileSystem.DeleteFile(entry.Path, current);
                return Resolve(entry, ChangeStatus.Undone, ChangeOutcome.Undone, ChangeCopy.RemovedMessage(entry.Path), change);
            }

            // The write is guarded by the exact text that was read, so a change after the comparison still refuses it.
            _fileSystem.WriteFile(entry.Path, crlf && !change.OldText.Contains('\r', StringComparison.Ordinal) ? change.OldText.Replace("\n", "\r\n", StringComparison.Ordinal) : change.OldText, current);
            return Resolve(entry, ChangeStatus.Undone, ChangeOutcome.Undone, ChangeCopy.UndoneMessage(entry.Path), change);
        }
        catch (StaleWorkspaceFileException)
        {
            return Resolve(entry, ChangeStatus.Stale, ChangeOutcome.Stale, ChangeCopy.StaleMessage, change);
        }
        catch (Exception exception) when (IsExpected(exception))
        {
            return Fail(entry, exception);
        }
    }

    // Whether the file holds the text the agent wrote. The comparison is exact, with one exception: the transcript carries line feeds only, so a file
    // whose every line ends in CRLF holds the agent's text when it equals that text with its line feeds turned into CRLF. Any other difference (lone
    // CR or LF endings, mixed endings, other text) is not the agent's text, and the undo is refused as stale instead of guessing.
    private static bool HoldsAgentText(string current, string agentText, out bool crlf)
    {
        crlf = false;
        if (string.Equals(current, agentText, StringComparison.Ordinal))
        {
            return true;
        }

        if (agentText.Contains('\r', StringComparison.Ordinal) || !current.Contains("\r\n", StringComparison.Ordinal) || current.Replace("\r\n", string.Empty, StringComparison.Ordinal).AsSpan().ContainsAny('\r', '\n'))
        {
            return false;
        }

        crlf = string.Equals(current.Replace("\r\n", "\n", StringComparison.Ordinal), agentText, StringComparison.Ordinal);
        return crlf;
    }

    // The file the agent left is gone. A file it created is already as an undo would leave it. A file it emptied (or deleted: the
    // transcript cannot tell) comes back with its old text. Any other edit left text behind, so the file was removed since: stale.
    private ChangeResult UndoMissingFile(ChangeEntry entry)
    {
        var change = entry.Change;
        if (change.OldText is null)
        {
            return Resolve(entry, ChangeStatus.Undone, ChangeOutcome.Undone, ChangeCopy.AlreadyGoneMessage(entry.Path), change);
        }

        if (change.NewText.Length != 0)
        {
            return Resolve(entry, ChangeStatus.Stale, ChangeOutcome.Stale, ChangeCopy.StaleMessage, change);
        }

        _fileSystem.CreateFile(entry.Path, change.OldText);
        return Resolve(entry, ChangeStatus.Undone, ChangeOutcome.Undone, ChangeCopy.UndoneMessage(entry.Path), change);
    }

    private string? ReadCurrent(string path)
    {
        try
        {
            return _fileSystem.ReadFile(path);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
    }

    // An expected failure of the file system: the path leaves the workspace, the file cannot be read as text or written, or the file
    // system is read-only. Anything else is a bug and propagates.
    private static bool IsExpected(Exception exception) =>
        exception is UnauthorizedAccessException or IOException or InvalidDataException or NotSupportedException or ArgumentException;

    private static ChangeResult Fail(ChangeEntry entry, Exception exception)
    {
        if (exception is UnauthorizedAccessException)
        {
            return new ChangeResult(entry.Path, ChangeOutcome.Refused, ChangeCopy.RefusedMessage(entry.Path));
        }

        return new ChangeResult(entry.Path, ChangeOutcome.Failed, ChangeCopy.FailedMessage(entry.Path, exception.Message));
    }

    // Records a decision on one file: its entry takes the status, and the decision holds until the transcript's edits of the file change.
    private ChangeResult Resolve(ChangeEntry entry, ChangeStatus status, ChangeOutcome outcome, string message, SessionChange effective)
    {
        _resolutions[entry.Path] = new Resolution(status, entry.Source, effective.NewText, message);
        entry.Set(entry.Source, effective, status, message);
        Refresh();
        return new ChangeResult(entry.Path, outcome, message);
    }

    // The state an entry takes for the changes the transcript reports now: a decision made on the same edits holds, new edits start pending.
    private (SessionChange Change, ChangeStatus Status, string Message) StateOf(SessionChange source)
    {
        if (_resolutions.TryGetValue(source.Path, out var resolution))
        {
            if (resolution.IsFor(source))
            {
                return (source.WithNewText(resolution.EffectiveNewText), resolution.Status, resolution.Message);
            }

            _ = _resolutions.Remove(source.Path);
        }

        return (source, ChangeStatus.Pending, string.Empty);
    }

    private ChangeResult Publish(ChangeResult result)
    {
        Announcement = result.Outcome == ChangeOutcome.Stale ? ChangeCopy.StaleAnnouncement(result.Path) : result.Message;
        return result;
    }

    private ChangeEntry? Find(string path)
    {
        var index = FindIndex(path);
        return index < 0 ? null : _entries[index];
    }

    private int FindIndex(string path)
    {
        for (var index = 0; index < _entries.Count; index++)
        {
            if (string.Equals(_entries[index].Path, path, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private void Refresh()
    {
        NotifyPropertyChanged(nameof(HasChanges));
        NotifyPropertyChanged(nameof(ActionableCount));
        NotifyPropertyChanged(nameof(Summary));
        _keepAllCommand.NotifyCanExecuteChanged();
        _undoAllCommand.NotifyCanExecuteChanged();
    }

    private void OnSessionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(SessionViewModel.Tree), StringComparison.Ordinal) && _session is not null)
        {
            Update(_session.Tree.Blocks);
        }
    }

    private void Detach()
    {
        if (_session is not null)
        {
            _session.PropertyChanged -= OnSessionChanged;
            _session = null;
        }
    }

    /// <summary>A decision on a file and the edits it was made on.</summary>
    private sealed record Resolution(ChangeStatus Status, SessionChange Source, string EffectiveNewText, string Message)
    {
        /// <summary>Tells whether the decision was made on exactly these edits of the transcript.</summary>
        public bool IsFor(SessionChange source) =>
            Source.EditCount == source.EditCount
            && string.Equals(Source.LastBlockId, source.LastBlockId, StringComparison.Ordinal)
            && string.Equals(Source.NewText, source.NewText, StringComparison.Ordinal);
    }
}
