using System.Windows.Input;
using OmpOS.Presentation.Observable;

namespace OmpOS.Presentation.Changes;

/// <summary>
/// One changed file in the Changes bar: its path, what the agent's edits did, the lines they added and removed, and where its review
/// stands. The entry changes only through its <see cref="ChangeSetModel"/>; its commands act on the file through the model.
/// </summary>
public sealed class ChangeEntry : ObservableModel
{
    private static readonly string[] AllProperties =
    [
        nameof(Change),
        nameof(Status),
        nameof(Kind),
        nameof(Added),
        nameof(Removed),
        nameof(AddedText),
        nameof(RemovedText),
        nameof(StatusText),
        nameof(KindText),
        nameof(Message),
        nameof(IsActionable),
        nameof(RowName),
    ];

    private SessionChange _change;
    private ChangeStatus _status = ChangeStatus.Pending;
    private string _message = string.Empty;

    /// <summary>Creates the entry of one changed file, pending. Its commands act through the model that owns it.</summary>
    /// <param name="source">The change as the transcript reports it.</param>
    /// <param name="owner">The model whose Keep, Undo and Review the entry's commands run.</param>
    internal ChangeEntry(SessionChange source, ChangeSetModel owner)
    {
        Source = source;
        _change = source;
        var path = source.Path;
        KeepCommand = new ChangeCommand(_ => owner.Keep(path), () => IsActionable);
        UndoCommand = new ChangeCommand(_ => owner.Undo(path), () => IsActionable);
        ReviewCommand = new ChangeCommand(_ => owner.Review(path), () => IsActionable);
    }

    /// <summary>Gets the workspace-relative path of the file.</summary>
    public string Path => Source.Path;

    /// <summary>Gets the change as the transcript reports it, whatever was undone since.</summary>
    internal SessionChange Source { get; private set; }

    /// <summary>Gets the change as it stands now: the text before the agent's first edit and the text the file holds after the last edit, or after the hunks undone since.</summary>
    public SessionChange Change => _change;

    /// <summary>Gets where the file's review stands.</summary>
    public ChangeStatus Status => _status;

    /// <summary>Gets what the agent's edits did to the file.</summary>
    public ChangeKind Kind => _change.Kind;

    /// <summary>Gets the number of lines the edits added.</summary>
    public int Added => _change.Added;

    /// <summary>Gets the number of lines the edits removed.</summary>
    public int Removed => _change.Removed;

    /// <summary>Gets the added-lines count as text, such as <c>+3</c>.</summary>
    public string AddedText => ChangeCopy.AddedText(_change.Added);

    /// <summary>Gets the removed-lines count as text, such as <c>-1</c>.</summary>
    public string RemovedText => ChangeCopy.RemovedText(_change.Removed);

    /// <summary>Gets the status as text.</summary>
    public string StatusText => ChangeCopy.StatusText(_status);

    /// <summary>Gets the word for what the edits did to the file.</summary>
    public string KindText => ChangeCopy.KindText(_change.Kind);

    /// <summary>Gets the sentence the last action on the file said, or an empty string before any.</summary>
    public string Message => _message;

    /// <summary>Gets whether the file still waits for a decision: its edits are pending or changed on disk since. Keep, Undo and Review work only then.</summary>
    public bool IsActionable => _status is ChangeStatus.Pending or ChangeStatus.Stale;

    /// <summary>Gets the accessible name of the Keep control.</summary>
    public string KeepName => ChangeCopy.KeepName(Path);

    /// <summary>Gets the accessible name of the Undo control.</summary>
    public string UndoName => ChangeCopy.UndoName(Path);

    /// <summary>Gets the accessible name of the Review control.</summary>
    public string ReviewName => ChangeCopy.ReviewName(Path);

    /// <summary>Gets the accessible name of the row: the file, what happened to it and where the review stands.</summary>
    public string RowName => ChangeCopy.RowName(Path, Kind, _status, Added, Removed);

    /// <summary>Gets the command that keeps the file's changes.</summary>
    public ICommand KeepCommand { get; }

    /// <summary>Gets the command that undoes the file's changes.</summary>
    public ICommand UndoCommand { get; }

    /// <summary>Gets the command that opens the file's review.</summary>
    public ICommand ReviewCommand { get; }

    /// <summary>Takes a new state and tells every binding.</summary>
    internal void Set(SessionChange source, SessionChange change, ChangeStatus status, string message)
    {
        if (source == Source && change == _change && status == _status && string.Equals(message, _message, StringComparison.Ordinal))
        {
            return;
        }

        Source = source;
        _change = change;
        _status = status;
        _message = message;
        foreach (var name in AllProperties)
        {
            NotifyPropertyChanged(name);
        }

        ((ChangeCommand)KeepCommand).NotifyCanExecuteChanged();
        ((ChangeCommand)UndoCommand).NotifyCanExecuteChanged();
        ((ChangeCommand)ReviewCommand).NotifyCanExecuteChanged();
    }
}
