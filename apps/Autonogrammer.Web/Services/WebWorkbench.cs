using Autonogrammer.Contracts.Catalog;
using Autonogrammer.Presentation.AgentPanel;
using Autonogrammer.Presentation.Changes;
using Autonogrammer.Presentation.Kanban;
using Autonogrammer.Presentation.Settings;
using Autonogrammer.Presentation.Terminal;
using Autonogrammer.Web.Services.Settings;
using Microsoft.JSInterop;

namespace Autonogrammer.Web.Services;

/// <summary>
/// The composition root of the web head's view models over the API (or the Demo handler). One instance per browser tab. It owns the
/// session catalogue and its rail model (<see cref="Panel"/>), the Kanban board (<see cref="Board"/>) and the Settings window model
/// (<see cref="Settings"/>); it opens the user event stream with resume and resync handling; and it builds one <c>WebSessionRuntime</c> per session
/// the panel opens. Pages mount it by name; they never talk to the API themselves.
/// </summary>
internal sealed class WebWorkbench : IAsyncDisposable
{
    private static readonly TimeSpan[] ReconnectDelays =
    [
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(15),
    ];

    private readonly IOmpApiClient _api;
    private readonly IUserEventFeed _feed;
    private readonly TimeProvider _time;
    private readonly WebSessionCatalog _catalog = new();
    private readonly SessionCatalogModel _catalogModel;
    private readonly WebToolApprovalBridge _approvals;
    private readonly Dictionary<string, WebSessionRuntime> _runtimes = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _lifetime = new();
    private Task? _pump;
    private bool _started;
    private int _disposed;

    /// <summary>Initializes a new instance of the <see cref="WebWorkbench"/> class. It starts nothing: call <see cref="StartAsync"/>.</summary>
    /// <param name="api">The API client.</param>
    /// <param name="feed">The user-level event stream.</param>
    /// <param name="js">The JavaScript runtime (the General settings page).</param>
    /// <param name="time">The clock.</param>
    /// <param name="options">How the head was composed.</param>
    /// <param name="extraPages">Settings pages other services register.</param>
    internal WebWorkbench(
        IOmpApiClient api,
        IUserEventFeed feed,
        IJSRuntime js,
        TimeProvider time,
        WebWorkbenchOptions options,
        IEnumerable<ISettingsPage> extraPages)
    {
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(feed);
        ArgumentNullException.ThrowIfNull(js);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(extraPages);
        _api = api;
        _feed = feed;
        _time = time;
        IsDemo = options.IsDemo;
        _approvals = new WebToolApprovalBridge(api);
        _approvals.Decided += OnApprovalDecided;
        _catalogModel = new SessionCatalogModel(
            _catalog,
            time,
            new SessionCatalogOptions { LiveStatus = _catalog.LiveStatusOf, IsClosed = _catalog.IsClosed });
        Panel = new AgentPanelViewModel(
            _catalogModel,
            new WebAgentPanelHost(api, _catalog, OpenSession, ReloadAsync));
        Board = new KanbanBoardModel(
            _catalogModel,
            time,
            new KanbanBoardOptions
            {
                ModelOf = ModelOf,
                Details = new WebKanbanDetailsSource(api),
                Steering = new WebCardSteering(api),
                StartLoading = true,
            });
        Board.StartSessionRequested += OnStartSessionRequested;
        Settings = WebSettingsFactory.Create(api, js, options.ApiHost, time, extraPages);
    }

    /// <summary>Occurs after something the shell shows changed that no view model announces: <see cref="Source"/>, <see cref="Workspace"/> or the connection.</summary>
    internal event EventHandler? Changed;

    /// <summary>Occurs when a slash command asks to open the Settings dialog on a page.</summary>
    internal event EventHandler<SettingsRequestedEventArgs>? SettingsRequested;

    /// <summary>Gets the Agent Panel: the rail, the selected session and the panel state.</summary>
    public AgentPanelViewModel Panel { get; }

    /// <summary>Gets the Kanban board over the same catalogue.</summary>
    public KanbanBoardModel Board { get; }

    /// <summary>Gets the Settings window model.</summary>
    public SettingsWindowModel Settings { get; }

    /// <summary>Gets a value indicating whether the head runs on the Demo handler. The shell shows its permanent Demo banner then.</summary>
    public bool IsDemo { get; }

    /// <summary>Gets the licence and source offer (<c>GET /source</c>), or null until it loaded or when the API did not answer.</summary>
    public SourceInfo? Source { get; private set; }

    /// <summary>Gets the workspace name and plugin check (<c>GET /workspace</c>), or null until it loaded or when the API did not answer.</summary>
    public WorkspaceInfo? Workspace { get; private set; }

    /// <summary>Gets what the New terminal control may do in this head. In the browser it is never available.</summary>
    public static TerminalAvailability TerminalAvailability => TerminalAvailability.ForLocal(TerminalHostKind.Web);

    /// <summary>
    /// Loads the catalogue, the workspace and the source, selects the most recently active session, starts the periodic refresh of the relative times and
    /// opens the user event stream. A failure to load is shown by the panel (its Error state and Retry), not thrown. Calling it again does nothing.
    /// </summary>
    /// <param name="cancellationToken">Cancels the load. The event stream outlives the call and ends with <see cref="DisposeAsync"/>.</param>
    /// <returns>A task that completes when the first load ended.</returns>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (_started)
        {
            return;
        }

        _started = true;
        using (Panel.BeginInitialising())
        {
            try
            {
                await ReloadAsync(cancellationToken).ConfigureAwait(true);
            }
            catch (Exception fault) when (WebFailureText.IsExpected(fault))
            {
                Panel.ReportLoadFailure(WebFailureText.Describe(fault));
            }
        }

        Board.CompleteLoading();
        if (Panel.Catalog.SelectedId is null && _catalog.Entries.Count > 0)
        {
            // The API lists the most recently active session first; the rail groups by status, so its first row is not that session.
            _ = Panel.SelectSession(_catalog.Entries[0].Id);
        }

        _catalogModel.StartRefreshing();
        _pump = PumpUserEventsAsync(_lifetime.Token);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Sends the last prompt of a session again (the Retry of a recoverable error).</summary>
    /// <param name="sessionId">The session.</param>
    /// <returns>A task that completes when the prompt was submitted or refused.</returns>
    public Task RetryAsync(string sessionId) =>
        _runtimes.TryGetValue(sessionId, out var runtime) ? runtime.RetryAsync() : Task.CompletedTask;

    /// <summary>Gets the Changes bar model of an opened session.</summary>
    /// <param name="sessionId">The session.</param>
    /// <returns>The model, or null when the session was not opened.</returns>
    public ChangeSetModel? ChangesOf(string sessionId) =>
        _runtimes.TryGetValue(sessionId, out var runtime) ? runtime.Changes.Model : null;

    /// <summary>Gets the sentence that explains the last refused or unapplied change decision of a session.</summary>
    /// <param name="sessionId">The session.</param>
    /// <returns>The sentence, or null.</returns>
    public string? ChangeNoticeOf(string sessionId) =>
        _runtimes.TryGetValue(sessionId, out var runtime) ? runtime.Changes.Notice : null;

    /// <summary>Stops the event stream, ends every session runtime and disposes the view models. A second call does nothing.</summary>
    /// <returns>A task that completes when the stream and the runtimes ended.</returns>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _approvals.Decided -= OnApprovalDecided;
        Board.StartSessionRequested -= OnStartSessionRequested;
        await _lifetime.CancelAsync().ConfigureAwait(true);
        if (_pump is not null)
        {
            try
            {
                await _pump.WaitAsync(TimeSpan.FromSeconds(2), _time).ConfigureAwait(true);
            }
            catch (Exception fault) when (fault is not OutOfMemoryException)
            {
                // The pump ends with the token. At shutdown its outcome does not matter: a timeout, a cancellation or a failure of a stream
                // that is already closing must not hold the tab's shutdown.
            }
        }

        foreach (var runtime in _runtimes.Values.ToList())
        {
            await runtime.DisposeAsync().ConfigureAwait(true);
        }

        _runtimes.Clear();
        Board.Dispose();
        Panel.Dispose();
        _catalogModel.Dispose();
        _lifetime.Dispose();
    }

    private KanbanModelInfo? ModelOf(string sessionId) =>
        _runtimes.TryGetValue(sessionId, out var runtime) ? runtime.Controls.CurrentModel : null;

    private async Task ReloadAsync(CancellationToken cancellationToken)
    {
        var sessions = await _api.ListSessionsAsync(cancellationToken).ConfigureAwait(true);
        _catalog.Replace(sessions);
        await PruneRuntimesAsync().ConfigureAwait(true);
        Workspace = await TryAsync(_api.GetWorkspaceAsync, cancellationToken).ConfigureAwait(true);
        Source = await TryAsync(_api.GetSourceAsync, cancellationToken).ConfigureAwait(true);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static async Task<T?> TryAsync<T>(Func<CancellationToken, Task<T>> load, CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            return await load(cancellationToken).ConfigureAwait(true);
        }
        catch (Exception fault) when (WebFailureText.IsExpected(fault))
        {
            return null;
        }
    }

    private AgentSessionViewModel OpenSession(AgentRow row)
    {
        if (_runtimes.TryGetValue(row.Id, out var existing))
        {
            return existing.ViewModel;
        }

        var runtime = new WebSessionRuntime(new WebSessionRuntimeOptions
        {
            Row = row,
            Api = _api,
            Catalog = _catalog,
            Approvals = _approvals,
            TimeProvider = _time,
            Targets = new WebSlashTargets { OpenSettings = OpenSettingsPage, NewChat = NewChatAsync },
        });
        _runtimes[row.Id] = runtime;
        runtime.Start();
        return runtime.ViewModel;
    }

    private bool OpenSettingsPage(string name)
    {
        var section = string.IsNullOrWhiteSpace(name)
            ? Settings.Pages[0].Section
            : Enum.TryParse<SettingsSection>(name.Trim(), ignoreCase: true, out var parsed) ? parsed : (SettingsSection?)null;
        if (section is not { } target || !Settings.Pages.Any(page => page.Section == target))
        {
            return false;
        }

        Settings.SelectSection(target);
        SettingsRequested?.Invoke(this, new SettingsRequestedEventArgs(target));
        return true;
    }

    private Task NewChatAsync(CancellationToken cancellationToken) => Panel.NewSessionAsync(cancellationToken);

    private void OnStartSessionRequested(object? sender, EventArgs e) => _ = Panel.NewSessionCommand.ExecuteAsync(null);

    private void OnApprovalDecided(object? sender, ApprovalDecidedEventArgs e)
    {
        if (_runtimes.TryGetValue(e.SessionId, out var runtime))
        {
            _ = runtime.RefreshApprovalsAsync();
        }
    }

    private async Task PruneRuntimesAsync()
    {
        var listed = _catalog.Entries.Select(static entry => entry.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var id in _runtimes.Keys.Where(id => !listed.Contains(id)).ToList())
        {
            var runtime = _runtimes[id];
            _ = _runtimes.Remove(id);
            await runtime.DisposeAsync().ConfigureAwait(true);
        }
    }

    private async Task PumpUserEventsAsync(CancellationToken cancellationToken)
    {
        long? last = null;
        var failures = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await foreach (var evt in _feed.StreamAsync(last, cancellationToken).ConfigureAwait(true))
                {
                    failures = 0;
                    last = string.Equals(evt.Kind, CatalogEventKinds.Resync, StringComparison.Ordinal) ? null : evt.Id ?? last;
                    await ApplyAsync(evt, cancellationToken).ConfigureAwait(true);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception fault) when (WebFailureText.IsExpected(fault))
            {
                failures++;
                try
                {
                    await Task.Delay(ReconnectDelays[Math.Min(failures, ReconnectDelays.Length) - 1], _time, cancellationToken).ConfigureAwait(true);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }

    private async Task ApplyAsync(UserStreamEvent evt, CancellationToken cancellationToken)
    {
        switch (evt.Kind)
        {
            case CatalogEventKinds.CatalogChanged when evt.Catalog is { } change:
                if (!_catalog.Apply(change))
                {
                    await ReloadCatalogAsync(cancellationToken).ConfigureAwait(true);
                }

                break;
            case CatalogEventKinds.SessionStatus when evt.Status is { } status:
                if (!_catalog.Apply(status))
                {
                    await ReloadCatalogAsync(cancellationToken).ConfigureAwait(true);
                }

                if (_runtimes.TryGetValue(status.SessionId, out var observed))
                {
                    await observed.ApplyStatusAsync(status.RowStatus).ConfigureAwait(true);
                }

                break;
            case CatalogEventKinds.Stage when evt.Stage is { } stage:
                if (_runtimes.TryGetValue(stage.SessionId, out var staged))
                {
                    staged.ApplyStage(stage);
                }

                break;
            case CatalogEventKinds.Resync:
                await ReloadCatalogAsync(cancellationToken).ConfigureAwait(true);
                foreach (var runtime in _runtimes.Values.ToList())
                {
                    if (_catalog.Find(runtime.ViewModel.Id) is { } entry)
                    {
                        await runtime.ApplyStatusAsync(entry.RowStatus).ConfigureAwait(true);
                    }
                }

                break;
            default:
                break;
        }
    }

    private async Task ReloadCatalogAsync(CancellationToken cancellationToken)
    {
        try
        {
            _catalog.Replace(await _api.ListSessionsAsync(cancellationToken).ConfigureAwait(true));
            await PruneRuntimesAsync().ConfigureAwait(true);
        }
        catch (Exception fault) when (WebFailureText.IsExpected(fault))
        {
            // The stream stays open; the next event or Retry loads the list again.
        }
    }
}
