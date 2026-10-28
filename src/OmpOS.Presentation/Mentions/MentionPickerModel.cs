using OmpOS.Presentation.Observable;

namespace OmpOS.Presentation.Mentions;

/// <summary>
/// The state of the mention picker, shaped as an ARIA combobox with a listbox popup: the composer text is the combobox, this model is the popup. It opens while the
/// caret is in a mention token, searches every source of the token's trigger concurrently, groups the rows by kind, and keeps one selected row
/// (<see cref="ActiveDescendantId"/>). Typing on cancels the search in flight; a result that arrives for an older query is dropped. A source that fails or times out
/// does not hide the others; the failure shows only when no row is listed. The model never edits the draft: the composer asks <see cref="Accept"/> which row the user chose.
/// </summary>
public sealed class MentionPickerModel : ObservableModel, IDisposable
{
    /// <summary>The prefix of every option identifier.</summary>
    public const string OptionIdPrefix = "mention-option-";

    private static readonly MentionKind[] KindOrder =
    [
        MentionKind.File,
        MentionKind.Folder,
        MentionKind.Terminal,
        MentionKind.Chat,
        MentionKind.SideChat,
        MentionKind.GitDiffWorking,
        MentionKind.GitDiffBranch,
        MentionKind.Browser,
        MentionKind.Rule,
        MentionKind.Skill,
        MentionKind.PullRequest,
    ];

    private readonly IReadOnlyList<IMentionSource> _sources;
    private readonly TimeProvider _time;
    private readonly TimeSpan _sourceTimeout;
    private CancellationTokenSource? _search;
    private int _version;
    private bool _isOpen;
    private MentionTrigger _trigger;
    private string _query = string.Empty;
    private bool _isLoading;
    private IReadOnlyList<MentionPickerRow> _rows = [];
    private IReadOnlyList<MentionGroup> _groups = [];
    private int _selectedIndex = -1;
    private string? _message;
    private MentionMessageKind _messageKind;
    private string? _announcement;
    private TaskCompletionSource _settled = CompletedSettled();
    private (int Start, string Query)? _dismissed;
    private int _tokenStart = -1;
    private string? _confirmedKey;
    private bool _confirming;

    /// <summary>Creates a closed picker over the sources of a session.</summary>
    /// <param name="sources">The sources. A source serves the picker of its <see cref="IMentionSource.Trigger"/>.</param>
    /// <param name="timeProvider">The clock of the per-source timeout. Defaults to the system clock.</param>
    /// <param name="sourceTimeout">How long one source may take before the picker stops waiting for it. Defaults to 3 seconds.</param>
    /// <exception cref="ArgumentNullException"><paramref name="sources"/> or one of its entries is null.</exception>
    public MentionPickerModel(IReadOnlyList<IMentionSource> sources, TimeProvider? timeProvider = null, TimeSpan? sourceTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(sources);
        foreach (var source in sources)
        {
            ArgumentNullException.ThrowIfNull(source, nameof(sources));
        }

        _sources = sources;
        _time = timeProvider ?? TimeProvider.System;
        _sourceTimeout = sourceTimeout ?? TimeSpan.FromSeconds(3);
    }

    /// <summary>Gets the sources the picker searches.</summary>
    public IReadOnlyList<IMentionSource> Sources => _sources;

    /// <summary>Gets whether the popup is showing (<c>aria-expanded</c>).</summary>
    public bool IsOpen { get => _isOpen; private set => SetProperty(ref _isOpen, value); }

    /// <summary>Gets the trigger of the token being typed.</summary>
    public MentionTrigger Trigger { get => _trigger; private set => SetProperty(ref _trigger, value); }

    /// <summary>Gets the name typed after the trigger.</summary>
    public string Query { get => _query; private set => SetProperty(ref _query, value); }

    /// <summary>Gets whether a search is running.</summary>
    public bool IsLoading { get => _isLoading; private set => SetProperty(ref _isLoading, value); }

    /// <summary>Gets the rows in one flat list, in group order. The index of a row is its option index.</summary>
    public IReadOnlyList<MentionPickerRow> Rows
    {
        get => _rows;
        private set
        {
            if (SetProperty(ref _rows, value))
            {
                NotifyPropertyChanged(nameof(SelectedRow));
            }
        }
    }

    /// <summary>Gets the rows by group.</summary>
    public IReadOnlyList<MentionGroup> Groups { get => _groups; private set => SetProperty(ref _groups, value); }

    /// <summary>Gets the index of the selected row, or -1.</summary>
    public int SelectedIndex
    {
        get => _selectedIndex;
        private set
        {
            if (SetProperty(ref _selectedIndex, value))
            {
                NotifyPropertyChanged(nameof(SelectedRow));
                NotifyPropertyChanged(nameof(ActiveDescendantId));
            }
        }
    }

    /// <summary>Gets the selected row, or <see langword="null"/>.</summary>
    public MentionPickerRow? SelectedRow => _selectedIndex >= 0 && _selectedIndex < _rows.Count ? _rows[_selectedIndex] : null;

    /// <summary>Gets the identifier of the selected option (<c>aria-activedescendant</c>), or <see langword="null"/> when no row is selected.</summary>
    public string? ActiveDescendantId => SelectedRow?.OptionId;

    /// <summary>Gets the text of the message line: why nothing is listed, a failure, or the confirmation question. It is <see langword="null"/> when rows are listed and nothing needs saying.</summary>
    public string? Message { get => _message; private set => SetProperty(ref _message, value); }

    /// <summary>Gets what <see cref="Message"/> says.</summary>
    public MentionMessageKind MessageKind { get => _messageKind; private set => SetProperty(ref _messageKind, value); }

    /// <summary>Gets the sentence for the live region: the number of rows, or the message.</summary>
    public string? Announcement { get => _announcement; private set => SetProperty(ref _announcement, value); }

    /// <summary>Gets whether the user must confirm the selected row before it is attached.</summary>
    public bool IsConfirming => _confirming;

    /// <summary>Returns a task that completes when the search of the latest <see cref="Update"/> has settled. A test or a shell awaits the result; it never faults.</summary>
    public Task WaitSettledAsync() => _settled.Task.WaitAsync(CancellationToken.None);

    /// <summary>Builds the option identifier of a row index.</summary>
    /// <param name="index">The row index.</param>
    /// <returns>For example <c>mention-option-3</c>.</returns>
    public static string OptionIdOf(int index) => OptionIdPrefix + index.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// Reports the token the caret is in. A token opens the picker and starts a search when its trigger or name changed; no token closes it. A picker the user
    /// dismissed stays closed for the same token and opens again for another one.
    /// </summary>
    /// <param name="token">The token under the caret, from <see cref="MentionParser.FindActive"/>, or <see langword="null"/>.</param>
    public void Update(MentionToken? token)
    {
        if (token is null)
        {
            _dismissed = null;
            Close();
            return;
        }

        if (_dismissed is { } dismissed && dismissed.Start == token.Start && string.Equals(dismissed.Query, token.Query, StringComparison.Ordinal))
        {
            return;
        }

        _dismissed = null;
        if (_isOpen && _trigger == token.Trigger && string.Equals(_query, token.Query, StringComparison.Ordinal))
        {
            _tokenStart = token.Start;
            return;
        }

        _tokenStart = token.Start;
        CancelConfirmation();
        Trigger = token.Trigger;
        Query = token.Query;
        IsOpen = true;
        StartSearch(token.Trigger, token.Query);
    }

    /// <summary>Moves the selection by <paramref name="delta"/> rows, wrapping past either end. It does nothing when no row is listed.</summary>
    /// <param name="delta">The number of rows to move; negative moves up.</param>
    public void MoveSelection(int delta)
    {
        if (_rows.Count == 0)
        {
            return;
        }

        var count = _rows.Count;
        CancelConfirmation();
        SelectedIndex = (((_selectedIndex + delta) % count) + count) % count;
        RefreshMessage();
    }

    /// <summary>Selects a row by index (a click). A pending confirmation is cancelled.</summary>
    /// <param name="index">The row index.</param>
    /// <returns><see langword="true"/> when the index named a row.</returns>
    public bool Select(int index)
    {
        if (index < 0 || index >= _rows.Count)
        {
            return false;
        }

        CancelConfirmation();
        SelectedIndex = index;
        RefreshMessage();
        return true;
    }

    /// <summary>
    /// Closes the picker until the token changes. A pending confirmation is cancelled first: the first Escape cancels the question, the second closes the list.
    /// </summary>
    public void Dismiss()
    {
        if (_confirming)
        {
            CancelConfirmation();
            RefreshMessage();
            return;
        }

        _dismissed = (_tokenStart, _query);
        Close();
    }

    /// <summary>
    /// Takes the selected row. A disabled row is not taken: the message says why. A row that may hold secrets is not taken the first time: the message asks
    /// the user to confirm, and the next call takes it.
    /// </summary>
    /// <returns>The row to attach, or <see langword="null"/> when nothing was taken.</returns>
    public MentionCandidate? Accept()
    {
        if (SelectedRow is not { } row)
        {
            return null;
        }

        var candidate = row.Candidate;
        if (candidate.DisabledReason is not null)
        {
            SetMessage(MentionMessageKind.Unavailable, candidate.DisabledReason);
            return null;
        }

        var key = candidate.Kind + "|" + candidate.Id;
        if (candidate.RequiresConfirmation && !string.Equals(_confirmedKey, key, StringComparison.Ordinal))
        {
            _confirmedKey = key;
            _confirming = true;
            NotifyPropertyChanged(nameof(IsConfirming));
            SetMessage(MentionMessageKind.Confirm, MentionCopy.ConfirmSecret(candidate.Display));
            return null;
        }

        CancelConfirmation();
        return candidate;
    }

    /// <summary>Shows a failure of the last accept (the file vanished, the host refused) as the picker's message.</summary>
    /// <param name="reason">The reason, as a plain sentence.</param>
    /// <exception cref="ArgumentNullException"><paramref name="reason"/> is null.</exception>
    public void ShowFailure(string reason)
    {
        ArgumentNullException.ThrowIfNull(reason);
        SetMessage(MentionMessageKind.Unavailable, reason);
    }

    /// <summary>Returns the source that attaches a kind.</summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The source, or <see langword="null"/> when none is registered.</returns>
    public IMentionSource? SourceFor(MentionKind kind) => _sources.FirstOrDefault(source => source.Kind == kind);

    /// <inheritdoc/>
    public void Dispose()
    {
        _search?.Cancel();
        _search?.Dispose();
        _search = null;
    }

    private void CancelConfirmation()
    {
        _confirmedKey = null;
        if (_confirming)
        {
            _confirming = false;
            NotifyPropertyChanged(nameof(IsConfirming));
        }
    }

    private void Close()
    {
        _version++;
        _search?.Cancel();
        _confirming = false;
        _confirmedKey = null;
        IsOpen = false;
        IsLoading = false;
        Query = string.Empty;
        Rows = [];
        Groups = [];
        SelectedIndex = -1;
        Message = null;
        MessageKind = MentionMessageKind.None;
        Announcement = null;
        _tokenStart = -1;
        _settled = CompletedSettled();
    }

    private void StartSearch(MentionTrigger trigger, string query)
    {
        _search?.Cancel();
        _search?.Dispose();
        _search = new CancellationTokenSource();
        var version = ++_version;
        IsLoading = true;
        if (_rows.Count == 0)
        {
            SetMessage(MentionMessageKind.Searching, MentionCopy.Searching);
        }

        _settled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = RunAsync(trigger, query, version, _settled, _search.Token);
    }

    private static TaskCompletionSource CompletedSettled()
    {
        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        source.SetResult();
        return source;
    }

    private async Task RunAsync(MentionTrigger trigger, string query, int version, TaskCompletionSource settled, CancellationToken token)
    {
        try
        {
            var limit = query.EndsWith('/') ? MentionLimits.RowsPerSourceTyped * 3 : query.Length == 0 ? MentionLimits.RowsPerSourceEmpty : MentionLimits.RowsPerSourceTyped;
            var active = _sources.Where(source => source.Trigger == trigger).ToList();
            var outcomes = await Task.WhenAll(active.Select(source => SearchOneAsync(source, query, limit, token))).ConfigureAwait(true);
            if (token.IsCancellationRequested || version != _version)
            {
                return;
            }

            Apply(outcomes);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // A newer query replaced this search: its answer is not wanted.
        }
        finally
        {
            settled.TrySetResult();
        }
    }

    private async Task<SourceOutcome> SearchOneAsync(IMentionSource source, string query, int limit, CancellationToken token)
    {
        using var timeout = new CancellationTokenSource(_sourceTimeout, _time);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, timeout.Token);
        try
        {
            var result = await source.SearchAsync(query, limit, linked.Token).ConfigureAwait(false);
            return new SourceOutcome(source.Kind, result, null);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return new SourceOutcome(source.Kind, null, MentionCopy.SourceTimedOut(MentionCopy.GroupTitle(source.Kind)));
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return new SourceOutcome(source.Kind, null, exception.Message);
        }
    }

    private static List<(MentionCandidate Candidate, string Title)> OrderOutcomes(IReadOnlyList<SourceOutcome> outcomes, out string? unavailable, out string? failure)
    {
        var ordered = new List<(MentionCandidate Candidate, string Title)>();
        unavailable = null;
        failure = null;
        foreach (var kind in KindOrder)
        {
            foreach (var outcome in outcomes.Where(outcome => outcome.Kind == kind))
            {
                if (outcome.Result is { } result)
                {
                    unavailable ??= result.UnavailableReason;
                    var title = MentionCopy.GroupTitle(kind);
                    foreach (var candidate in result.Items.Take(Math.Max(MentionLimits.MaxRows - ordered.Count, 0)))
                    {
                        ordered.Add((candidate, title));
                    }
                }
                else
                {
                    failure ??= outcome.Error;
                }
            }
        }

        return ordered;
    }

    private void Apply(IReadOnlyList<SourceOutcome> outcomes)
    {
        var previous = SelectedRow?.Candidate;
        var ordered = OrderOutcomes(outcomes, out var unavailable, out var failure);

        var rows = ordered
            .Select((entry, index) => new MentionPickerRow(
                index,
                entry.Candidate,
                entry.Candidate.Kind == MentionKind.PullRequest ? "#" + entry.Candidate.Display : entry.Candidate.Display,
                MentionCopy.RowName(entry.Candidate),
                index == 0 || !string.Equals(ordered[index - 1].Title, entry.Title, StringComparison.Ordinal),
                entry.Title))
            .ToList();
        Rows = rows;
        Groups = rows
            .GroupBy(static row => row.GroupTitle, StringComparer.Ordinal)
            .Select(static grouped => new MentionGroup(grouped.Key, grouped.ToList()))
            .ToList();

        var keep = previous is null ? -1 : rows.FindIndex(row => row.Candidate.Kind == previous.Kind && string.Equals(row.Candidate.Id, previous.Id, StringComparison.Ordinal));
        SelectedIndex = keep >= 0 ? keep : rows.Count > 0 ? 0 : -1;
        IsLoading = false;

        if (rows.Count > 0)
        {
            Announcement = MentionCopy.ResultCount(rows.Count);
            RefreshMessage();
        }
        else if (failure is not null && unavailable is null)
        {
            SetMessage(MentionMessageKind.Error, MentionCopy.SearchFailed + ": " + failure);
        }
        else if (unavailable is not null)
        {
            SetMessage(MentionMessageKind.Unavailable, unavailable);
        }
        else
        {
            SetMessage(MentionMessageKind.Empty, MentionCopy.NothingMatches);
        }
    }

    private void RefreshMessage()
    {
        if (_rows.Count == 0)
        {
            return;
        }

        if (SelectedRow is { Candidate.DisabledReason: { } reason })
        {
            SetMessage(MentionMessageKind.Unavailable, reason);
        }
        else if (_confirming && SelectedRow is { } row)
        {
            SetMessage(MentionMessageKind.Confirm, MentionCopy.ConfirmSecret(row.Candidate.Display));
        }
        else
        {
            Message = null;
            MessageKind = MentionMessageKind.None;
            Announcement = MentionCopy.ResultCount(_rows.Count);
        }
    }

    private void SetMessage(MentionMessageKind kind, string text)
    {
        MessageKind = kind;
        Message = text;
        Announcement = text;
    }

    private sealed record SourceOutcome(MentionKind Kind, MentionSearchResult? Result, string? Error);
}
