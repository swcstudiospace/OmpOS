using System.ComponentModel;
using Autonogrammer.Presentation.Observable;

namespace Autonogrammer.Presentation.AgentPanel;

/// <summary>
/// The root of the Agent Panel: the session rail (<see cref="Catalog"/>), the selected session's view model (<see cref="Selected"/>),
/// the panel state (Empty, Loading, Ready, Error), the banner with its retry, and the New agent, select and close operations.
/// </summary>
/// <remarks>
/// <para>
/// State: <see cref="PanelState.Loading"/> while any <see cref="BeginInitialising"/> scope is open; otherwise <see cref="PanelState.Error"/>
/// while a load failure is reported (by <see cref="ReportLoadFailure"/> or by the catalog); otherwise <see cref="PanelState.Empty"/> with no
/// session and <see cref="PanelState.Ready"/> with at least one. A closed session stays in the catalog, so closing the last open session
/// leaves the panel Ready with that session selected and offering Reopen; only a catalog with no session at all is Empty.
/// </para>
/// <para>
/// Selection: selecting a row awaits the host's open and then activates its context. Only the latest selection may bind
/// <see cref="Selected"/>. Immediate hosts still bind synchronously. Acquired view models are kept until their row leaves the catalog;
/// cancellation stops obsolete startup and context loads. A selection change never announces anything.
/// </para>
/// <para>
/// One place raises <see cref="ObservableModel.PropertyChanged"/>: every operation updates its fields and calls one private publisher,
/// which compares them with what it announced last. Not thread-safe: it runs on the UI context that owns the presentation models.
/// </para>
/// </remarks>
public sealed class AgentPanelViewModel : ObservableModel, IDisposable
{
    private readonly SessionCatalogModel _catalog;
    private readonly IAgentPanelHost _host;
    private readonly Dictionary<string, AgentSessionViewModel> _sessions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Task<AgentSessionViewModel?>> _opening = new(StringComparer.Ordinal);
    private readonly HashSet<string> _retiredOpenings = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _lifetime = new();
    private readonly AgentPanelCommand _newSessionCommand;
    private readonly AgentPanelCommand _newTerminalCommand;
    private readonly AgentPanelCommand _retryLoadingCommand;
    private AgentSessionViewModel? _selected;
    private int _initialising;
    private string? _hostError;
    private string? _selectionError;
    private string? _actionError;
    private string? _announcement;
    private int _announcementVersion;
    private PanelState _publishedState = PanelState.Empty;
    private string? _publishedBanner;
    private int _publishedCount;
    private AgentSessionViewModel? _publishedSelected;
    private string? _publishedAnnouncement;
    private int _publishedAnnouncementVersion;
    private string? _publishedActionError;
    private Task _selectionLoad = Task.CompletedTask;
    private CancellationTokenSource? _selectionCancellation;
    private int _rebinding;
    private bool _disposed;

    /// <summary>Creates the panel over a catalog model and a host.</summary>
    /// <param name="catalog">The rail model. The panel subscribes to it but does not own it.</param>
    /// <param name="host">The head's engine and registry operations.</param>
    /// <exception cref="ArgumentNullException"><paramref name="catalog"/> or <paramref name="host"/> is null.</exception>
    public AgentPanelViewModel(SessionCatalogModel catalog, IAgentPanelHost host)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(host);

        _catalog = catalog;
        _host = host;
        _newSessionCommand = new AgentPanelCommand(
            _ => NewSessionAsync(_lifetime.Token),
            fault => SetActionError(AgentPanelCopy.ActionFailed("start a new agent", PanelText.Plain(fault.Message, 200))));
        _newTerminalCommand = new AgentPanelCommand(
            _ => NewTerminalAsync(_lifetime.Token),
            fault => SetActionError(AgentPanelCopy.ActionFailed("start a new omp terminal", PanelText.Plain(fault.Message, 200))));
        _retryLoadingCommand = new AgentPanelCommand(
            _ => RetryLoadingAsync(_lifetime.Token),
            fault => ReportLoadFailure(fault.Message),
            _ => _publishedState == PanelState.Error);
        _catalog.PropertyChanged += OnCatalogChanged;
        _catalog.SelectionChanged += OnSelectionChanged;
        _catalog.CloseRequested += OnCloseRequested;
        SyncSessions();
        Publish();
    }

    /// <summary>Gets the rail model: rows, groups, filter and selection.</summary>
    public SessionCatalogModel Catalog => _catalog;

    /// <summary>Gets the view model of the selected session, or null when none is selected.</summary>
    public AgentSessionViewModel? Selected => _publishedSelected;

    /// <summary>Gets the panel state.</summary>
    public PanelState State => _publishedState;

    /// <summary>Gets the panel error banner, <c>Could not load your agents. {reason}. Your saved agents are not lost.</c>, or null outside the Error state.</summary>
    public string? BannerText => _publishedBanner;

    /// <summary>Gets the number of sessions, including closed ones.</summary>
    public int SessionCount => _publishedCount;

    /// <summary>
    /// Gets the latest sentence for the polite <c>Pipeline announcements</c> region: a stage transition of the selected session, or that an
    /// agent was closed. Null after the selection changed, because a selection change announces nothing.
    /// </summary>
    public string? PipelineAnnouncement => _publishedAnnouncement;

    /// <summary>Gets a number that grows with every announcement, including one that repeats the previous text.</summary>
    public int PipelineAnnouncementVersion => _publishedAnnouncementVersion;

    /// <summary>Gets the text of the last failed New agent or close, or null. The next successful action clears it.</summary>
    public string? ActionError => _publishedActionError;

    /// <summary>
    /// Gets a task that completes after the latest selection's startup and context activation, including cleanup of superseded selections.
    /// It never faults. After <see cref="Dispose"/>, awaiting it drains outstanding startup without adding an asynchronous disposal API.
    /// The non-cancellable wait retains the current task's identity, including while its startup is pending.
    /// </summary>
    public Task SelectionLoad => _selectionLoad.WaitAsync(CancellationToken.None);

    /// <summary>Gets the command that creates a session and selects it. The view moves focus to the new session's prompt.</summary>
    public AgentPanelCommand NewSessionCommand => _newSessionCommand;

    /// <summary>Gets the command that creates an omp terminal session and selects it. The view moves focus to the terminal.</summary>
    public AgentPanelCommand NewTerminalCommand => _newTerminalCommand;

    /// <summary>Gets the command that retries loading after an error. It is enabled only in the Error state.</summary>
    public AgentPanelCommand RetryLoadingCommand => _retryLoadingCommand;

    /// <summary>
    /// Opens an initialisation scope: the panel is <see cref="PanelState.Loading"/> until every open scope is disposed. The panel stays usable.
    /// </summary>
    /// <returns>The scope. Disposing it more than once is harmless.</returns>
    public IDisposable BeginInitialising()
    {
        _initialising++;
        Publish();
        return new InitialisationScope(this);
    }

    /// <summary>Reports that loading failed. The panel shows the Error state with a banner that names <paramref name="reason"/>.</summary>
    /// <param name="reason">A short reason without a trailing full stop. Untrusted text is made plain and capped.</param>
    public void ReportLoadFailure(string reason)
    {
        var plain = PanelText.Plain(reason, 200);
        _hostError = plain.Length == 0 ? "Loading failed" : plain;
        Publish();
    }

    /// <summary>Reloads through the host and re-reads the catalog. The panel is Loading meanwhile, then Ready, Empty or Error again.</summary>
    /// <param name="cancellationToken">Cancels the reload.</param>
    /// <returns>A task that completes when the reload ended. A failure of the host shows as the Error state and does not fault the task.</returns>
    public async Task RetryLoadingAsync(CancellationToken cancellationToken)
    {
        _hostError = null;
        var retrySelection = _selectionError is not null;
        using (BeginInitialising())
        {
            try
            {
                await _host.ReloadAsync(cancellationToken).ConfigureAwait(true);
                _catalog.Refresh();
                if (retrySelection && _selectionError is not null)
                {
                    Rebind(_catalog.SelectedId);
                    await SelectionLoad.WaitAsync(CancellationToken.None).ConfigureAwait(true);
                }
            }
            catch (OperationCanceledException)
            {
                // The reload was cancelled; the previous error text is gone, so show what the catalog has.
            }
            catch (Exception fault) when (fault is not OutOfMemoryException)
            {
                _hostError = PanelText.Plain(fault.Message, 200);
            }
        }
    }

    /// <summary>Creates a session through the host, refreshes the catalog and selects the new session.</summary>
    /// <param name="cancellationToken">Cancels the creation.</param>
    /// <returns>A task that completes when the new session is selected.</returns>
    public async Task NewSessionAsync(CancellationToken cancellationToken)
    {
        var id = await _host.CreateSessionAsync(cancellationToken).ConfigureAwait(true);
        _catalog.Refresh();
        SelectSession(id);
        await SelectionLoad.WaitAsync(CancellationToken.None).ConfigureAwait(true);
        SetActionError(null);
    }

    /// <summary>Creates an omp terminal session through the host, refreshes the catalog and selects the new session.</summary>
    /// <param name="cancellationToken">Cancels the creation.</param>
    /// <returns>A task that completes when the new session is selected.</returns>
    public async Task NewTerminalAsync(CancellationToken cancellationToken)
    {
        var id = await _host.CreateTerminalSessionAsync(cancellationToken).ConfigureAwait(true);
        _catalog.Refresh();
        SelectSession(id);
        await SelectionLoad.WaitAsync(CancellationToken.None).ConfigureAwait(true);
        SetActionError(null);
    }

    /// <summary>Selects a catalog row immediately; <see cref="SelectionLoad"/> tracks its awaited startup and context activation.</summary>
    /// <param name="sessionId">The session to select, or null to clear the selection.</param>
    /// <returns>True when the selection now equals <paramref name="sessionId"/>; false when the catalog has no such session.</returns>
    public bool SelectSession(string? sessionId) => _catalog.Select(sessionId);

    /// <summary>
    /// Closes a session: the host stops its engine, the session view model is marked closed, and when it was selected the next row (or the
    /// previous one when it was last) is selected. The closing is announced as <c>Agent "{title}" closed.</c>
    /// </summary>
    /// <param name="sessionId">The session to close.</param>
    /// <param name="cancellationToken">Cancels the close.</param>
    /// <returns>A task that completes when the session is closed. It faults when the host fails to stop the engine; the session is then unchanged.</returns>
    /// <exception cref="ArgumentException"><paramref name="sessionId"/> is null, empty or white space.</exception>
    public async Task CloseSessionAsync(string sessionId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        var row = _catalog.Sessions.FirstOrDefault(candidate => string.Equals(candidate.Id, sessionId, StringComparison.Ordinal));
        if (row is null)
        {
            return;
        }

        var order = (_catalog.Rows.Any(candidate => string.Equals(candidate.Id, sessionId, StringComparison.Ordinal)) ? _catalog.Rows : _catalog.Sessions)
            .Select(candidate => candidate.Id)
            .ToList();
        await _host.CloseSessionAsync(sessionId, cancellationToken).ConfigureAwait(true);
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            session.MarkClosed();
        }

        _catalog.Refresh();
        if (string.Equals(_catalog.SelectedId, sessionId, StringComparison.Ordinal) && Neighbour(order, sessionId) is { } next)
        {
            SelectSession(next);
        }

        _announcement = AgentPanelCopy.ClosedAnnouncement(row.Title);
        _announcementVersion++;
        SetActionError(null);
    }

    /// <summary>Unsubscribes from the catalog, cancels outstanding operations and disposes the session view models.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _catalog.PropertyChanged -= OnCatalogChanged;
        _catalog.SelectionChanged -= OnSelectionChanged;
        _catalog.CloseRequested -= OnCloseRequested;
        _lifetime.Cancel();
        _selectionCancellation?.Dispose();
        _selectionCancellation = null;
        _selected?.Deactivate();
        foreach (var session in _sessions.Values.ToList())
        {
            Release(session);
        }

        _sessions.Clear();
        _lifetime.Dispose();
    }

    private static string? Neighbour(List<string> order, string sessionId)
    {
        var index = order.IndexOf(sessionId);
        if (index < 0)
        {
            return null;
        }

        if (index + 1 < order.Count)
        {
            return order[index + 1];
        }

        return index > 0 ? order[index - 1] : null;
    }

    private void OnCatalogChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(SessionCatalogModel.Sessions), StringComparison.Ordinal))
        {
            SyncSessions();
        }

        if (e.PropertyName is nameof(SessionCatalogModel.Sessions) or nameof(SessionCatalogModel.LoadError))
        {
            Publish();
        }
    }

    private void OnSelectionChanged(object? sender, SessionSelectionChangedEventArgs e) => Rebind(e.SessionId);

    private void OnCloseRequested(object? sender, SessionSelectionChangedEventArgs e)
    {
        if (e.SessionId is { } id)
        {
            _ = CloseTrackedAsync(id);
        }
    }

    private void OnSessionCloseRequested(object? sender, EventArgs e)
    {
        if (sender is AgentSessionViewModel session)
        {
            _ = CloseTrackedAsync(session.Id);
        }
    }

    private async Task CloseTrackedAsync(string sessionId)
    {
        try
        {
            await CloseSessionAsync(sessionId, _lifetime.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // The panel was disposed while the engine stopped.
        }
        catch (Exception fault) when (fault is not OutOfMemoryException)
        {
            SetActionError(AgentPanelCopy.ActionFailed("close this agent", PanelText.Plain(fault.Message, 200)));
        }

        Publish();
    }

    private void OnTimelineChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_selected is not null
            && ReferenceEquals(sender, _selected.Timeline)
            && string.Equals(e.PropertyName, nameof(StageTimelineModel.StatusAnnouncement), StringComparison.Ordinal))
        {
            _announcement = _selected.Timeline.StatusAnnouncement;
            _announcementVersion++;
            Publish();
        }
    }

    private void SyncSessions()
    {
        var rows = _catalog.Sessions;
        var live = rows.Select(row => row.Id).ToHashSet(StringComparer.Ordinal);
        _retiredOpenings.UnionWith(_opening.Keys.Where(id => !live.Contains(id)));
        foreach (var id in _sessions.Keys.Where(known => !live.Contains(known)).ToList())
        {
            Release(_sessions[id]);
            _sessions.Remove(id);
        }

        foreach (var row in rows)
        {
            if (_sessions.TryGetValue(row.Id, out var session))
            {
                session.ApplyRow(row);
            }
        }
    }

    private void Release(AgentSessionViewModel session)
    {
        session.CloseRequested -= OnSessionCloseRequested;
        session.Timeline.PropertyChanged -= OnTimelineChanged;
        session.Dispose();
    }

    private async Task<AgentSessionViewModel?> OpenAsync(string sessionId, CancellationToken cancellationToken)
    {
        if (_opening.TryGetValue(sessionId, out var pending))
        {
            try
            {
                await pending.ConfigureAwait(true);
            }
            catch (Exception fault) when (fault is not OutOfMemoryException)
            {
                // A superseded attempt has finished its cleanup. A live selection retries rather than inheriting its failure.
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (_sessions.TryGetValue(sessionId, out var existing))
        {
            return existing;
        }

        var row = _catalog.Sessions.FirstOrDefault(candidate => string.Equals(candidate.Id, sessionId, StringComparison.Ordinal));
        if (row is null)
        {
            return null;
        }

        _retiredOpenings.Remove(sessionId);
        var opening = OpenCoreAsync(row, cancellationToken);
        _opening[sessionId] = opening;
        try
        {
            return await opening.ConfigureAwait(true);
        }
        finally
        {
            if (_opening.TryGetValue(sessionId, out var current) && ReferenceEquals(current, opening))
            {
                _opening.Remove(sessionId);
                _retiredOpenings.Remove(sessionId);
            }
        }
    }

    private async Task<AgentSessionViewModel?> OpenCoreAsync(AgentRow row, CancellationToken cancellationToken)
    {
        var session = await _host.OpenSessionAsync(row, cancellationToken).ConfigureAwait(true);
        var currentRow = _catalog.Sessions.FirstOrDefault(candidate => string.Equals(candidate.Id, row.Id, StringComparison.Ordinal));
        if (_disposed || currentRow is null || _retiredOpenings.Contains(row.Id))
        {
            Release(session);
            return null;
        }

        // Even an obsolete request may finish successfully. Keep its model, but never activate it from that request.
        session.ApplyRow(currentRow);
        session.CloseRequested += OnSessionCloseRequested;
        session.Timeline.PropertyChanged += OnTimelineChanged;
        _sessions[row.Id] = session;
        return session;
    }

    private void Rebind(string? sessionId)
    {
        if (_disposed)
        {
            return;
        }

        _rebinding++;
        try
        {
            _selectionCancellation?.Cancel();
            _selected?.Deactivate();
            _selected = null;
            _announcement = null;
            _selectionError = null;
            var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
            _selectionCancellation = cancellation;
            _selectionLoad = RebindAsync(sessionId, cancellation, _selectionLoad, cancellation.Token);
        }
        finally
        {
            // Publish only after SelectionLoad is installed, even when startup and activation complete synchronously.
            _rebinding--;
            Publish();
        }
    }

    private async Task RebindAsync(string? sessionId, CancellationTokenSource cancellation, Task previous, CancellationToken cancellationToken)
    {
        try
        {
            // Use the token captured before startup; Dispose owns the source even while this operation is still draining.
            var session = sessionId is null ? null : await OpenAsync(sessionId, cancellationToken).ConfigureAwait(true);
            if (_disposed || cancellationToken.IsCancellationRequested)
            {
                return;
            }

            _selected = session;
            Publish();
            if (session is not null && !cancellationToken.IsCancellationRequested && !_disposed)
            {
                await session.ActivateAsync().ConfigureAwait(true);
            }
        }
        catch (OperationCanceledException)
        {
            // Cancellation is a normal end of a selection, not a load failure.
        }
        catch (Exception fault) when (fault is not OutOfMemoryException)
        {
            if (!_disposed && !cancellationToken.IsCancellationRequested)
            {
                _selectionError = PanelText.Plain(fault.Message, 200);
                Publish();
            }
        }
        finally
        {
            if (ReferenceEquals(_selectionCancellation, cancellation))
            {
                _selectionCancellation = null;
            }

            cancellation.Dispose();
            // No superseded startup/cleanup task is abandoned, including when the latest selection is cleared or disposed.
            await previous.WaitAsync(CancellationToken.None).ConfigureAwait(true);
        }
    }

    private void SetActionError(string? text)
    {
        _actionError = text;
        Publish();
    }

    private PanelState ComputeState()
    {
        if (_initialising > 0)
        {
            return PanelState.Loading;
        }

        if (ErrorReason is not null)
        {
            return PanelState.Error;
        }

        return _catalog.TotalCount == 0 ? PanelState.Empty : PanelState.Ready;
    }

    private string? ErrorReason => _hostError ?? _selectionError ?? _catalog.LoadError;

    /// <summary>The only place that raises <see cref="ObservableModel.PropertyChanged"/>: it announces whatever differs from the last publication.</summary>
    private void Publish()
    {
        if (_disposed || _rebinding > 0)
        {
            return;
        }

        var state = ComputeState();
        SetProperty(ref _publishedState, state, nameof(State));
        SetProperty(ref _publishedBanner, state == PanelState.Error && ErrorReason is { } reason ? AgentPanelCopy.PanelError(reason) : null, nameof(BannerText));
        SetProperty(ref _publishedCount, _catalog.TotalCount, nameof(SessionCount));
        SetProperty(ref _publishedSelected, _selected, nameof(Selected));
        SetProperty(ref _publishedAnnouncement, _announcement, nameof(PipelineAnnouncement));
        SetProperty(ref _publishedAnnouncementVersion, _announcementVersion, nameof(PipelineAnnouncementVersion));
        SetProperty(ref _publishedActionError, _actionError, nameof(ActionError));
        _retryLoadingCommand.NotifyCanExecuteChanged();
    }

    private sealed class InitialisationScope : IDisposable
    {
        private readonly AgentPanelViewModel _panel;
        private bool _disposed;

        internal InitialisationScope(AgentPanelViewModel panel)
        {
            _panel = panel;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _panel._initialising--;
            _panel.Publish();
        }
    }
}
