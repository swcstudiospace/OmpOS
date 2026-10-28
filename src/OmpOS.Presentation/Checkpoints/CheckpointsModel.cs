using System.Collections.ObjectModel;
using System.Globalization;
using OmpOS.Presentation.AgentPanel;
using OmpOS.Presentation.Observable;

namespace OmpOS.Presentation.Checkpoints;

/// <summary>
/// The checkpoint timeline of one session: a row per turn in which the agent changed files, a preview of what rewinding to before a turn would do, and a rewind that always asks first.
/// A rewind restores files only; the conversation is not changed, and the words of the model say so. A file that changed after the agent's last edit is stale: the confirmation says so
/// and writes nothing until the user chooses to overwrite. Every failure is a sentence in <see cref="StatusText"/>, never an exception.
/// </summary>
/// <remarks>Not thread-safe: it runs on the UI context that owns the presentation models.</remarks>
public sealed class CheckpointsModel : ObservableModel
{
    private readonly string _sessionId;
    private readonly ICheckpointBackend _backend;
    private readonly TimeProvider _timeProvider;
    private readonly ObservableCollection<CheckpointRow> _rows = [];
    private readonly AgentPanelCommand _refreshCommand;
    private readonly AgentPanelCommand _previewCommand;
    private readonly AgentPanelCommand _rewindCommand;
    private readonly AgentPanelCommand _confirmCommand;
    private readonly AgentPanelCommand _forceCommand;
    private readonly AgentPanelCommand _cancelCommand;
    private readonly AgentPanelCommand _undoLastCommand;
    private CheckpointRewindPreview? _preview;
    private int? _pendingTurnId;
    private bool _isBusy;
    private bool _isOpen;
    private bool _hasError;
    private string _statusText = string.Empty;
    private string _confirmationText = string.Empty;

    /// <summary>Creates the timeline of one session.</summary>
    /// <param name="sessionId">The session.</param>
    /// <param name="backend">Keeps and restores the checkpoints.</param>
    /// <param name="timeProvider">The clock of the relative times.</param>
    /// <exception cref="ArgumentException"><paramref name="sessionId"/> is null, empty or white space.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="backend"/> or <paramref name="timeProvider"/> is null.</exception>
    public CheckpointsModel(string sessionId, ICheckpointBackend backend, TimeProvider timeProvider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(backend);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _sessionId = sessionId;
        _backend = backend;
        _timeProvider = timeProvider;
        Rows = new ReadOnlyObservableCollection<CheckpointRow>(_rows);
        _refreshCommand = new AgentPanelCommand(_ => RefreshAsync(CancellationToken.None), Fail, _ => !_isBusy);
        _previewCommand = new AgentPanelCommand(parameter => PreviewAsync(TurnOf(parameter), CancellationToken.None), Fail, parameter => !_isBusy && TurnOf(parameter) > 0);
        _rewindCommand = new AgentPanelCommand(parameter => RequestRewindAsync(TurnOf(parameter), CancellationToken.None), Fail, parameter => !_isBusy && TurnOf(parameter) > 0);
        _confirmCommand = new AgentPanelCommand(_ => ConfirmAsync(force: false, CancellationToken.None), Fail, _ => !_isBusy && IsConfirming);
        _forceCommand = new AgentPanelCommand(_ => ConfirmAsync(force: true, CancellationToken.None), Fail, _ => !_isBusy && IsConfirming && CanForce);
        _cancelCommand = new AgentPanelCommand(_ => DismissAsync(), Fail, _ => IsConfirming);
        _undoLastCommand = new AgentPanelCommand(_ => RequestUndoLastAsync(CancellationToken.None), Fail, _ => !_isBusy);
    }

    /// <summary>Occurs after a rewind wrote files.</summary>
    public event EventHandler<CheckpointRewoundEventArgs>? Rewound;

    /// <summary>Gets the turns that changed files, oldest first.</summary>
    public ReadOnlyObservableCollection<CheckpointRow> Rows { get; }

    /// <summary>Gets whether the session has a checkpoint.</summary>
    public bool HasCheckpoints => _rows.Count > 0;

    /// <summary>Gets the sentence shown while there is none: <see cref="CheckpointsCopy.Empty"/>, or an empty string when there is a checkpoint.</summary>
    public string EmptyText => HasCheckpoints ? string.Empty : CheckpointsCopy.Empty;

    /// <summary>Gets the number of checkpoints in words.</summary>
    public string CountText => CheckpointsCopy.Count(_rows.Count);

    /// <summary>Gets whether the timeline is shown. The head binds the panel's visibility to it; <c>/rewind</c> opens it.</summary>
    public bool IsOpen => _isOpen;

    /// <summary>Gets whether a read or a rewind is running. Every command waits while it is true.</summary>
    public bool IsBusy => _isBusy;

    /// <summary>Gets the last sentence for the status line and assistive technology.</summary>
    public string StatusText => _statusText;

    /// <summary>Gets whether <see cref="StatusText"/> reports a failure.</summary>
    public bool HasError => _hasError;

    /// <summary>Gets what the last preview or confirmation would do, or <see langword="null"/> before any.</summary>
    public CheckpointRewindPreview? Preview => _preview;

    /// <summary>Gets the files of <see cref="Preview"/>, each as <c>path: detail</c>.</summary>
    public IReadOnlyList<string> PreviewLines
    {
        get
        {
            if (_preview is null)
            {
                return [];
            }

            return [.. _preview.Files.Select(static file => file.Path + ": " + file.Detail)];
        }
    }

    /// <summary>Gets whether a rewind waits for the user's confirmation.</summary>
    public bool IsConfirming => _pendingTurnId is not null;

    /// <summary>Gets the turn a confirmation waits for, or <see langword="null"/>.</summary>
    public int? PendingTurnId => _pendingTurnId;

    /// <summary>Gets the question a confirmation asks, or an empty string.</summary>
    public string ConfirmationText => _confirmationText;

    /// <summary>Gets whether the pending rewind may overwrite files that changed since the agent's edit.</summary>
    public bool CanForce => IsConfirming && _preview is { StaleCount: > 0 };

    /// <summary>Gets the command that reads the timeline again.</summary>
    public AgentPanelCommand RefreshCommand => _refreshCommand;

    /// <summary>Gets the command that shows what a rewind would do. Its parameter is the turn number.</summary>
    public AgentPanelCommand PreviewCommand => _previewCommand;

    /// <summary>Gets the command that asks to rewind. Its parameter is the turn number; it opens the confirmation and writes nothing.</summary>
    public AgentPanelCommand RewindCommand => _rewindCommand;

    /// <summary>Gets the command that confirms the pending rewind. It never overwrites a stale file.</summary>
    public AgentPanelCommand ConfirmCommand => _confirmCommand;

    /// <summary>Gets the command that confirms the pending rewind and overwrites files that changed since the agent's edit.</summary>
    public AgentPanelCommand ForceCommand => _forceCommand;

    /// <summary>Gets the command that dismisses the confirmation.</summary>
    public AgentPanelCommand CancelCommand => _cancelCommand;

    /// <summary>Gets the command that asks to rewind the latest checkpoint.</summary>
    public AgentPanelCommand UndoLastCommand => _undoLastCommand;

    /// <summary>Shows the timeline.</summary>
    public void Open() => SetProperty(ref _isOpen, true, nameof(IsOpen));

    /// <summary>Hides the timeline and dismisses a pending confirmation.</summary>
    public void Close()
    {
        CancelConfirmation();
        SetProperty(ref _isOpen, false, nameof(IsOpen));
    }

    /// <summary>Reads the timeline from the backend.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the rows are current.</returns>
    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        await RunAsync(
            async () =>
            {
                var turns = await _backend.ListAsync(_sessionId, cancellationToken).ConfigureAwait(true);
                var now = _timeProvider.GetUtcNow();
                _rows.Clear();
                foreach (var turn in turns.OrderBy(static turn => turn.TurnId))
                {
                    _rows.Add(new CheckpointRow(turn, now));
                }

                NotifyRows();
                SetStatus(CheckpointsCopy.Count(_rows.Count) + ".", isError: false);
            }).ConfigureAwait(true);
    }

    /// <summary>Updates the relative time of every row.</summary>
    public void RefreshTimes()
    {
        var now = _timeProvider.GetUtcNow();
        foreach (var row in _rows)
        {
            row.RefreshTime(now);
        }
    }

    /// <summary>Shows what a rewind to before a turn would do. Nothing is written.</summary>
    /// <param name="turnId">The turn.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>A task that completes when <see cref="Preview"/> is set.</returns>
    public async Task PreviewAsync(int turnId, CancellationToken cancellationToken)
    {
        await RunAsync(
            async () =>
            {
                var preview = await _backend.PreviewAsync(_sessionId, turnId, cancellationToken).ConfigureAwait(true);
                SetPreview(preview);
                SetStatus(CheckpointsCopy.PreviewSummary(turnId, preview), isError: false);
            }).ConfigureAwait(true);
    }

    /// <summary>Asks to rewind to before a turn: shows what it would do and waits for <see cref="ConfirmAsync"/>. Nothing is written yet.</summary>
    /// <param name="turnId">The turn.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>A task that completes when the confirmation is shown, or the status says why there is none.</returns>
    public async Task RequestRewindAsync(int turnId, CancellationToken cancellationToken)
    {
        await RunAsync(
            async () =>
            {
                var preview = await _backend.PreviewAsync(_sessionId, turnId, cancellationToken).ConfigureAwait(true);
                SetPreview(preview);
                if (preview.Files.Count == 0)
                {
                    SetPending(null, string.Empty);
                    SetStatus(CheckpointsCopy.Gone, isError: false);
                    return;
                }

                Open();
                SetPending(turnId, CheckpointsCopy.Confirmation(turnId, preview));
                SetStatus(_confirmationText, isError: false);
            }).ConfigureAwait(true);
    }

    /// <summary>Asks to rewind the latest checkpoint, like <c>/undo</c>.</summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>A task that completes when the confirmation is shown, or the status says why there is none.</returns>
    public async Task RequestUndoLastAsync(CancellationToken cancellationToken)
    {
        if (_rows.Count == 0)
        {
            await RefreshAsync(cancellationToken).ConfigureAwait(true);
        }

        if (_rows.Count == 0)
        {
            SetStatus(CheckpointsCopy.Empty, isError: false);
            return;
        }

        await RequestRewindAsync(_rows[^1].TurnId, cancellationToken).ConfigureAwait(true);
    }

    /// <summary>Does the pending rewind.</summary>
    /// <param name="force"><see langword="true"/> overwrites files that changed since the agent's edit. It has an effect only when <see cref="CanForce"/> is true.</param>
    /// <param name="cancellationToken">Cancels the rewind before it writes.</param>
    /// <returns>How the rewind ended, or <see langword="null"/> when no rewind waited for confirmation.</returns>
    public async Task<CheckpointRewindOutcome?> ConfirmAsync(bool force, CancellationToken cancellationToken)
    {
        if (_pendingTurnId is not { } turnId)
        {
            return null;
        }

        var outcome = await RunValueAsync(
            async () =>
            {
                var result = await _backend.RewindAsync(_sessionId, turnId, force && CanForce, cancellationToken).ConfigureAwait(true);
                SetPreview(result.Preview);
                var failed = result.Status is CheckpointRewindStatus.Failed or CheckpointRewindStatus.Blocked;
                SetStatus(result.Message, failed);
                SetPending(result.Status == CheckpointRewindStatus.Blocked ? turnId : null, result.Status == CheckpointRewindStatus.Blocked ? CheckpointsCopy.Confirmation(turnId, result.Preview) : string.Empty);
                return result;
            }).ConfigureAwait(true);

        if (outcome is { Status: CheckpointRewindStatus.Completed })
        {
            await RefreshAsync(cancellationToken).ConfigureAwait(true);
            SetStatus(outcome.Message + " " + CheckpointsCopy.ConversationUnchanged, isError: false);
            Rewound?.Invoke(this, new CheckpointRewoundEventArgs(outcome));
        }

        return outcome;
    }

    /// <summary>Dismisses the pending confirmation. Nothing was written.</summary>
    public void CancelConfirmation()
    {
        if (!IsConfirming)
        {
            return;
        }

        SetPending(null, string.Empty);
        SetStatus(CheckpointsCopy.Cancelled, isError: false);
    }

    private static int TurnOf(object? parameter) => parameter switch
    {
        int number => number,
        string text when int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) => parsed,
        _ => 0,
    };

    private Task DismissAsync()
    {
        CancelConfirmation();
        return Task.CompletedTask;
    }

    private async Task<T?> RunValueAsync<T>(Func<Task<T>> work)
        where T : class
    {
        SetBusy(true);
        try
        {
            return await work().ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is not OperationCanceledException and not OutOfMemoryException)
        {
            Fail(exception);
            return null;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task RunAsync(Func<Task> work)
    {
        SetBusy(true);
        try
        {
            await work().ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is not OperationCanceledException and not OutOfMemoryException)
        {
            Fail(exception);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void Fail(Exception exception) => SetStatus(CheckpointsCopy.Failed(exception.Message), isError: true);

    private void SetBusy(bool busy)
    {
        if (SetProperty(ref _isBusy, busy, nameof(IsBusy)))
        {
            NotifyCommands();
        }
    }

    private void SetStatus(string text, bool isError)
    {
        SetProperty(ref _statusText, text, nameof(StatusText));
        SetProperty(ref _hasError, isError, nameof(HasError));
    }

    private void SetPreview(CheckpointRewindPreview preview)
    {
        _preview = preview;
        NotifyPropertyChanged(nameof(Preview));
        NotifyPropertyChanged(nameof(PreviewLines));
        NotifyPropertyChanged(nameof(CanForce));
        NotifyCommands();
    }

    private void SetPending(int? turnId, string text)
    {
        _pendingTurnId = turnId;
        SetProperty(ref _confirmationText, text, nameof(ConfirmationText));
        NotifyPropertyChanged(nameof(PendingTurnId));
        NotifyPropertyChanged(nameof(IsConfirming));
        NotifyPropertyChanged(nameof(CanForce));
        NotifyCommands();
    }

    private void NotifyRows()
    {
        NotifyPropertyChanged(nameof(HasCheckpoints));
        NotifyPropertyChanged(nameof(EmptyText));
        NotifyPropertyChanged(nameof(CountText));
        NotifyCommands();
    }

    private void NotifyCommands()
    {
        _refreshCommand.NotifyCanExecuteChanged();
        _previewCommand.NotifyCanExecuteChanged();
        _rewindCommand.NotifyCanExecuteChanged();
        _confirmCommand.NotifyCanExecuteChanged();
        _forceCommand.NotifyCanExecuteChanged();
        _cancelCommand.NotifyCanExecuteChanged();
        _undoLastCommand.NotifyCanExecuteChanged();
    }
}
