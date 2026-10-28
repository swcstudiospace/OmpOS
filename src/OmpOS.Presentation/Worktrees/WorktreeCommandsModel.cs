using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using OmpOS.Presentation.AgentPanel;
using OmpOS.Presentation.Observable;

namespace OmpOS.Presentation.Worktrees;

/// <summary>
/// The worktree commands of one repository: <c>/worktree</c> starts a run in its own checkout, <c>/apply-worktree</c> applies a run's changes back as a reviewable patch and reports the files that
/// conflict, <c>/delete-worktree</c> deletes a run after a confirmation, and <c>/best-of-n</c> makes n runs of the same prompt and lists them for comparison. The model keeps no file and starts no
/// process: <see cref="IWorktreeBackend"/> does the git work and <see cref="IBestOfNHost"/> runs the agents. Every failure is a sentence in <see cref="StatusText"/>, never an exception.
/// </summary>
/// <remarks>Not thread-safe: it runs on the UI context that owns the presentation models.</remarks>
public sealed class WorktreeCommandsModel : ObservableModel
{
    private readonly IWorktreeBackend _backend;
    private readonly IBestOfNHost? _bestOfHost;
    private readonly TimeProvider _timeProvider;
    private readonly ObservableCollection<WorktreeRunRow> _rows = [];
    private readonly ObservableCollection<BestOfNCandidate> _candidates = [];
    private readonly AgentPanelCommand _refreshCommand;
    private readonly AgentPanelCommand _startCommand;
    private readonly AgentPanelCommand _previewCommand;
    private readonly AgentPanelCommand _applyCommand;
    private readonly AgentPanelCommand _applyThreeWayCommand;
    private readonly AgentPanelCommand _requestDeleteCommand;
    private readonly AgentPanelCommand _confirmDeleteCommand;
    private readonly AgentPanelCommand _cancelDeleteCommand;
    private List<WorktreeRunInfo> _pendingDelete = [];
    private WorktreePatchPreview? _preview;
    private WorktreeApplyOutcome? _lastApply;
    private bool _isBusy;
    private bool _isOpen;
    private bool _hasError;
    private string _statusText = string.Empty;
    private string _deleteConfirmationText = string.Empty;

    /// <summary>Creates the commands of one repository.</summary>
    /// <param name="backend">Does the git work.</param>
    /// <param name="timeProvider">The clock of generated run ids.</param>
    /// <param name="bestOfHost">Runs the agents of <c>/best-of-n</c>, or <see langword="null"/> when this window cannot.</param>
    /// <exception cref="ArgumentNullException"><paramref name="backend"/> or <paramref name="timeProvider"/> is null.</exception>
    public WorktreeCommandsModel(IWorktreeBackend backend, TimeProvider timeProvider, IBestOfNHost? bestOfHost = null)
    {
        ArgumentNullException.ThrowIfNull(backend);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _backend = backend;
        _timeProvider = timeProvider;
        _bestOfHost = bestOfHost;
        Rows = new ReadOnlyObservableCollection<WorktreeRunRow>(_rows);
        Candidates = new ReadOnlyObservableCollection<BestOfNCandidate>(_candidates);
        _refreshCommand = new AgentPanelCommand(_ => RefreshAsync(CancellationToken.None), Fail, _ => !_isBusy);
        _startCommand = new AgentPanelCommand(parameter => StartAsync(parameter as string, CancellationToken.None), Fail, _ => !_isBusy);
        _previewCommand = new AgentPanelCommand(parameter => PreviewAsync(Id(parameter), CancellationToken.None), Fail, parameter => !_isBusy && Id(parameter).Length > 0);
        _applyCommand = new AgentPanelCommand(parameter => ApplyAsync(Id(parameter), WorktreeApplyMethod.Clean, CancellationToken.None), Fail, parameter => !_isBusy && Id(parameter).Length > 0);
        _applyThreeWayCommand = new AgentPanelCommand(parameter => ApplyAsync(Id(parameter), WorktreeApplyMethod.ThreeWay, CancellationToken.None), Fail, parameter => !_isBusy && Id(parameter).Length > 0);
        _requestDeleteCommand = new AgentPanelCommand(parameter => RequestDeleteAsync(Id(parameter), CancellationToken.None), Fail, parameter => !_isBusy && Id(parameter).Length > 0);
        _confirmDeleteCommand = new AgentPanelCommand(_ => ConfirmDeleteAsync(CancellationToken.None), Fail, _ => !_isBusy && IsConfirmingDelete);
        _cancelDeleteCommand = new AgentPanelCommand(_ => DismissAsync(), Fail, _ => IsConfirmingDelete);
    }

    /// <summary>Gets the run worktrees of the repository.</summary>
    public ReadOnlyObservableCollection<WorktreeRunRow> Rows { get; }

    /// <summary>Gets the candidates of the last <c>/best-of-n</c>, for comparison.</summary>
    public ReadOnlyObservableCollection<BestOfNCandidate> Candidates { get; }

    /// <summary>Gets whether the repository has a run worktree.</summary>
    public bool HasRuns => _rows.Count > 0;

    /// <summary>Gets the sentence shown while there is none, or an empty string.</summary>
    public string EmptyText => HasRuns ? string.Empty : WorktreeCommandsCopy.Empty;

    /// <summary>Gets whether the worktree panel is shown. The head binds its visibility to it.</summary>
    public bool IsOpen => _isOpen;

    /// <summary>Gets whether a command is running. Every command waits while it is true.</summary>
    public bool IsBusy => _isBusy;

    /// <summary>Gets the last sentence for the status line and assistive technology.</summary>
    public string StatusText => _statusText;

    /// <summary>Gets whether <see cref="StatusText"/> reports a failure or a conflict.</summary>
    public bool HasError => _hasError;

    /// <summary>Gets the patch of the run last reviewed, or <see langword="null"/>.</summary>
    public WorktreePatchPreview? Preview => _preview;

    /// <summary>Gets how the last apply ended, or <see langword="null"/>. Its conflict paths are the files to resolve.</summary>
    public WorktreeApplyOutcome? LastApply => _lastApply;

    /// <summary>Gets whether a deletion waits for the user's confirmation.</summary>
    public bool IsConfirmingDelete => _pendingDelete.Count > 0;

    /// <summary>Gets the runs a confirmation would delete.</summary>
    public IReadOnlyList<WorktreeRunInfo> PendingDelete => _pendingDelete;

    /// <summary>Gets the question a deletion asks, or an empty string.</summary>
    public string DeleteConfirmationText => _deleteConfirmationText;

    /// <summary>Gets the command that reads the worktrees again.</summary>
    public AgentPanelCommand RefreshCommand => _refreshCommand;

    /// <summary>Gets the command that starts a run worktree. Its parameter is an optional name.</summary>
    public AgentPanelCommand StartCommand => _startCommand;

    /// <summary>Gets the command that shows a run's patch. Its parameter is the run id.</summary>
    public AgentPanelCommand PreviewCommand => _previewCommand;

    /// <summary>Gets the command that applies a run's changes only when they fit. Its parameter is the run id.</summary>
    public AgentPanelCommand ApplyCommand => _applyCommand;

    /// <summary>Gets the command that merges a run's changes three ways. Its parameter is the run id.</summary>
    public AgentPanelCommand ApplyThreeWayCommand => _applyThreeWayCommand;

    /// <summary>Gets the command that asks to delete a run. Its parameter is the run id; it opens the confirmation and deletes nothing.</summary>
    public AgentPanelCommand RequestDeleteCommand => _requestDeleteCommand;

    /// <summary>Gets the command that confirms the pending deletion.</summary>
    public AgentPanelCommand ConfirmDeleteCommand => _confirmDeleteCommand;

    /// <summary>Gets the command that dismisses the pending deletion.</summary>
    public AgentPanelCommand CancelDeleteCommand => _cancelDeleteCommand;

    /// <summary>Shows the worktree panel.</summary>
    public void Open() => SetProperty(ref _isOpen, true, nameof(IsOpen));

    /// <summary>Hides the worktree panel and dismisses a pending deletion.</summary>
    public void Close()
    {
        CancelDelete();
        SetProperty(ref _isOpen, false, nameof(IsOpen));
    }

    /// <summary>Reads the run worktrees from the backend.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the rows are current.</returns>
    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        await RunAsync(async () => await ReloadAsync(cancellationToken).ConfigureAwait(true)).ConfigureAwait(true);
    }

    /// <summary>Starts a run in a worktree, like <c>/worktree</c>.</summary>
    /// <param name="name">The name to give the run, or blank for a generated one. Characters other than letters, digits and hyphens become hyphens.</param>
    /// <param name="cancellationToken">Cancels the creation.</param>
    /// <returns>The new worktree, or <see langword="null"/> when it could not be made (the status says why).</returns>
    public async Task<WorktreeRunInfo?> StartAsync(string? name, CancellationToken cancellationToken)
    {
        var result = await RunValueAsync(
            async () =>
            {
                await ReloadAsync(cancellationToken).ConfigureAwait(true);
                var started = await _backend.StartAsync(UniqueRunId(name), cancellationToken).ConfigureAwait(true);
                await ReloadAsync(cancellationToken).ConfigureAwait(true);
                Open();
                SetStatus(WorktreeCommandsCopy.Started(started.Run, started.SetupMessage), isError: false);
                return started;
            }).ConfigureAwait(true);
        return result?.Run;
    }

    /// <summary>Shows a run's changes as a patch to review. Nothing is applied.</summary>
    /// <param name="runId">The run.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when <see cref="Preview"/> is set.</returns>
    public async Task PreviewAsync(string runId, CancellationToken cancellationToken)
    {
        await RunAsync(
            async () =>
            {
                var preview = await _backend.PreviewAsync(runId, cancellationToken).ConfigureAwait(true);
                SetProperty(ref _preview, preview, nameof(Preview));
                SetStatus(WorktreeCommandsCopy.PatchSummary(preview), isError: false);
            }).ConfigureAwait(true);
    }

    /// <summary>Applies a run's changes to the open checkout, like <c>/apply-worktree</c>. The changes stay uncommitted.</summary>
    /// <param name="runId">The run, or blank when the repository has exactly one run.</param>
    /// <param name="method">What to do when the patch does not fit.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>How it ended, or <see langword="null"/> when the run could not be chosen or git failed hard (the status says why). A patch that does not fit is a result that names the files.</returns>
    public async Task<WorktreeApplyOutcome?> ApplyAsync(string runId, WorktreeApplyMethod method, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(runId);
        return await RunValueAsync(
            async () =>
            {
                await ReloadAsync(cancellationToken).ConfigureAwait(true);
                var id = ResolveRunId(runId);
                if (id is null)
                {
                    return null;
                }

                var outcome = await _backend.ApplyAsync(id, method, cancellationToken).ConfigureAwait(true);
                SetProperty(ref _lastApply, outcome, nameof(LastApply));
                SetStatus(outcome.Message, outcome.State is WorktreeApplyState.Conflicts or WorktreeApplyState.Failed);
                return outcome;
            }).ConfigureAwait(true);
    }

    /// <summary>Asks to delete a run, like <c>/delete-worktree</c>: it shows what would be lost and waits for <see cref="ConfirmDeleteAsync"/>. Nothing is deleted yet.</summary>
    /// <param name="runId">The run, or blank when the repository has exactly one run.</param>
    /// <param name="cancellationToken">Cancels the read of the worktrees.</param>
    /// <returns>A task that completes when the confirmation is shown, or the status says why there is none.</returns>
    public async Task RequestDeleteAsync(string runId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(runId);
        await RunAsync(
            async () =>
            {
                await ReloadAsync(cancellationToken).ConfigureAwait(true);
                var id = ResolveRunId(runId);
                if (id is null)
                {
                    return;
                }

                SetPendingDelete([_rows.First(row => string.Equals(row.RunId, id, StringComparison.Ordinal)).Run]);
            }).ConfigureAwait(true);
    }

    /// <summary>Deletes the runs the confirmation names.</summary>
    /// <param name="cancellationToken">Cancels the deletion between runs.</param>
    /// <returns><see langword="true"/> when every run was deleted; <see langword="false"/> when none waited, or one failed (the status says which).</returns>
    public async Task<bool> ConfirmDeleteAsync(CancellationToken cancellationToken)
    {
        if (!IsConfirmingDelete)
        {
            return false;
        }

        var pending = _pendingDelete;
        var deleted = 0;
        var done = await RunValueAsync(
            async () =>
            {
                foreach (var run in pending)
                {
                    await _backend.DeleteAsync(run.RunId, cancellationToken).ConfigureAwait(true);
                    deleted++;
                }

                return "done";
            }).ConfigureAwait(true);

        ForgetCandidates(pending.Take(deleted).Select(static run => run.RunId));
        SetPendingDelete([]);
        await RunAsync(async () => await ReloadAsync(cancellationToken).ConfigureAwait(true)).ConfigureAwait(true);
        if (done is not null)
        {
            SetStatus(WorktreeCommandsCopy.Deleted(deleted), isError: false);
        }

        return done is not null;
    }

    /// <summary>Dismisses the pending deletion. Nothing was deleted.</summary>
    public void CancelDelete()
    {
        if (!IsConfirmingDelete)
        {
            return;
        }

        SetPendingDelete([]);
        SetStatus(WorktreeCommandsCopy.DeleteCancelled, isError: false);
    }

    /// <summary>Makes n worktrees, runs the same prompt in each through <see cref="IBestOfNHost"/> and lists the results in <see cref="Candidates"/> for comparison, like <c>/best-of-n</c>.</summary>
    /// <param name="prompt">The task every candidate gets.</param>
    /// <param name="count">The number of candidates, 1 to <see cref="WorktreeCommandsCopy.MaxBestOf"/>.</param>
    /// <param name="cancellationToken">Cancels the runs.</param>
    /// <returns>A task that completes when every candidate ended.</returns>
    public async Task BestOfNAsync(string prompt, int count, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        if (_bestOfHost is null)
        {
            SetStatus(WorktreeCommandsCopy.NoBestOfHost(), isError: true);
            return;
        }

        if (count is < 1 or > WorktreeCommandsCopy.MaxBestOf)
        {
            SetStatus(WorktreeCommandsCopy.CountOutOfRange(), isError: true);
            return;
        }

        if (string.IsNullOrWhiteSpace(prompt))
        {
            SetStatus(WorktreeCommandsCopy.NeedsPrompt(), isError: true);
            return;
        }

        await RunAsync(
            async () =>
            {
                _candidates.Clear();
                var stamp = _timeProvider.GetUtcNow().ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
                for (var index = 1; index <= count; index++)
                {
                    var candidate = new BestOfNCandidate(index, string.Create(CultureInfo.InvariantCulture, $"bon-{stamp}-{index}"));
                    _candidates.Add(candidate);
                    await StartCandidateAsync(candidate, cancellationToken).ConfigureAwait(true);
                }

                var host = _bestOfHost;
                var task = prompt.Trim();
                await Task.WhenAll(_candidates.Where(static candidate => candidate.Run is not null).Select(candidate => RunCandidateAsync(host, task, count, candidate, cancellationToken))).ConfigureAwait(true);
                await ReloadAsync(cancellationToken).ConfigureAwait(true);
                CountChanges();
                Open();
                SetStatus(WorktreeCommandsCopy.BestOfDone(_candidates.Count(static candidate => candidate.State == BestOfNState.Finished), count), isError: false);
            }).ConfigureAwait(true);
    }

    /// <summary>Asks to delete every candidate except the chosen one, after the chosen one was applied. Nothing is deleted until <see cref="ConfirmDeleteAsync"/>.</summary>
    /// <param name="chosenIndex">The number of the candidate to keep.</param>
    /// <returns><see langword="false"/> when there is no other candidate with a worktree.</returns>
    public bool RequestDiscardOthers(int chosenIndex)
    {
        var others = _candidates.Where(candidate => candidate.Index != chosenIndex && candidate.Run is not null).Select(static candidate => candidate.Run!).ToList();
        if (others.Count == 0)
        {
            return false;
        }

        SetPendingDelete(others);
        return true;
    }

    private static string Id(object? parameter) => parameter as string ?? string.Empty;

    private static string Sanitize(string name)
    {
        var builder = new StringBuilder();
        foreach (var character in name.Trim())
        {
            builder.Append(character is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') ? character : '-');
        }

        var text = builder.ToString().Trim('-');
        return text.Length <= 56 ? text : text[..56].TrimEnd('-');
    }

    private string UniqueRunId(string? name)
    {
        var baseId = string.IsNullOrWhiteSpace(name) ? string.Empty : Sanitize(name);
        if (baseId.Length == 0)
        {
            baseId = "wt-" + _timeProvider.GetUtcNow().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        }

        var id = baseId;
        for (var suffix = 2; _rows.Any(row => string.Equals(row.RunId, id, StringComparison.OrdinalIgnoreCase)); suffix++)
        {
            id = string.Create(CultureInfo.InvariantCulture, $"{baseId}-{suffix}");
        }

        return id;
    }

    private string? ResolveRunId(string requested)
    {
        var text = requested.Trim();
        if (text.Length > 0)
        {
            if (_rows.Any(row => string.Equals(row.RunId, text, StringComparison.Ordinal)))
            {
                return text;
            }

            SetStatus(WorktreeCommandsCopy.Failed("there is no worktree named " + text), isError: true);
            return null;
        }

        if (_rows.Count == 1)
        {
            return _rows[0].RunId;
        }

        SetStatus(
            _rows.Count == 0
                ? WorktreeCommandsCopy.Empty
                : "Name the worktree: " + string.Join(", ", _rows.Select(static row => row.RunId)) + ".",
            isError: _rows.Count > 0);
        return null;
    }

    private async Task StartCandidateAsync(BestOfNCandidate candidate, CancellationToken cancellationToken)
    {
        try
        {
            var started = await _backend.StartAsync(candidate.RunId, cancellationToken).ConfigureAwait(true);
            candidate.SetRun(started.Run);
        }
        catch (Exception exception) when (exception is not OperationCanceledException and not OutOfMemoryException)
        {
            candidate.Update(BestOfNState.Failed, exception.Message, 0);
        }
    }

    private static async Task RunCandidateAsync(IBestOfNHost? host, string prompt, int count, BestOfNCandidate candidate, CancellationToken cancellationToken)
    {
        candidate.Update(BestOfNState.Running, string.Empty, 0);
        try
        {
            var result = await host!.RunAsync(new BestOfNRunRequest(prompt, candidate.Index, count, candidate.Run!), cancellationToken).ConfigureAwait(true);
            candidate.Update(result.Succeeded ? BestOfNState.Finished : BestOfNState.Failed, result.Summary, 0);
        }
        catch (Exception exception) when (exception is not OperationCanceledException and not OutOfMemoryException)
        {
            candidate.Update(BestOfNState.Failed, exception.Message, 0);
        }
    }

    private void CountChanges()
    {
        foreach (var candidate in _candidates)
        {
            var row = _rows.FirstOrDefault(row => string.Equals(row.RunId, candidate.RunId, StringComparison.Ordinal));
            if (row is not null)
            {
                candidate.Update(candidate.State, candidate.Summary, row.Run.ChangedFiles);
            }
        }
    }

    private void ForgetCandidates(IEnumerable<string> runIds)
    {
        var gone = runIds.ToHashSet(StringComparer.Ordinal);
        foreach (var candidate in _candidates.Where(candidate => gone.Contains(candidate.RunId)).ToList())
        {
            _candidates.Remove(candidate);
        }
    }

    private async Task ReloadAsync(CancellationToken cancellationToken)
    {
        var runs = await _backend.ListAsync(cancellationToken).ConfigureAwait(true);
        _rows.Clear();
        foreach (var run in runs)
        {
            _rows.Add(new WorktreeRunRow(run));
        }

        NotifyPropertyChanged(nameof(HasRuns));
        NotifyPropertyChanged(nameof(EmptyText));
    }

    private Task DismissAsync()
    {
        CancelDelete();
        return Task.CompletedTask;
    }

    private async Task<T?> RunValueAsync<T>(Func<Task<T?>> work)
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

    private void Fail(Exception exception) => SetStatus(WorktreeCommandsCopy.Failed(exception.Message), isError: true);

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

    private void SetPendingDelete(List<WorktreeRunInfo> runs)
    {
        _pendingDelete = runs;
        SetProperty(ref _deleteConfirmationText, runs.Count == 0 ? string.Empty : WorktreeCommandsCopy.DeleteConfirmation(runs), nameof(DeleteConfirmationText));
        NotifyPropertyChanged(nameof(PendingDelete));
        NotifyPropertyChanged(nameof(IsConfirmingDelete));
        if (runs.Count > 0)
        {
            Open();
            SetStatus(_deleteConfirmationText, isError: false);
        }

        NotifyCommands();
    }

    private void NotifyCommands()
    {
        _refreshCommand.NotifyCanExecuteChanged();
        _startCommand.NotifyCanExecuteChanged();
        _previewCommand.NotifyCanExecuteChanged();
        _applyCommand.NotifyCanExecuteChanged();
        _applyThreeWayCommand.NotifyCanExecuteChanged();
        _requestDeleteCommand.NotifyCanExecuteChanged();
        _confirmDeleteCommand.NotifyCanExecuteChanged();
        _cancelDeleteCommand.NotifyCanExecuteChanged();
    }
}
