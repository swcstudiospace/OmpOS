using OmpOS.Presentation.Observable;

namespace OmpOS.Presentation.Cloud;

/// <summary>
/// The Cloud agents page: the user's cloud agents with a status in words and a glyph, a status filter, a selection with its details, the
/// limits, and a new-agent dialog model. The list refreshes itself when <see cref="ICloudAgentsHost.Changes"/> fires. When the host says
/// cloud agents are not configured the model is in <see cref="CloudAgentsViewState.NotConfigured"/> and offers no agent command.
/// </summary>
public sealed class CloudAgentsViewModel : ObservableModel, IDisposable
{
    private readonly ICloudAgentsHost _host;
    private readonly TimeProvider _timeProvider;
    private readonly Action<Action> _dispatch;
    private readonly CloudFilterOption[] _filterOptions;
    private IReadOnlyList<CloudAgentRecord> _records = [];
    private CloudAgentRow[] _rows = [];
    private CloudSetup _setup = CloudSetup.NotConfigured();
    private CloudLimits? _limits;
    private CloudAgentsViewState _state = CloudAgentsViewState.Loading;
    private CloudAgentStatus? _statusFilter;
    private CloudAgentDetailsModel? _details;
    private string? _selectedId;
    private string _loadError = string.Empty;
    private string _refreshError = string.Empty;
    private bool _loaded;
    private bool _isRefreshing;
    private bool _refreshAgain;
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="CloudAgentsViewModel"/> class. It subscribes to <see cref="ICloudAgentsHost.Changes"/>; <see cref="Dispose"/> unsubscribes.</summary>
    /// <param name="host">Where the agents come from.</param>
    /// <param name="timeProvider">The clock for the relative times.</param>
    /// <param name="options">The head hooks. Null uses the defaults.</param>
    /// <exception cref="ArgumentNullException"><paramref name="host"/> or <paramref name="timeProvider"/> is null.</exception>
    public CloudAgentsViewModel(ICloudAgentsHost host, TimeProvider timeProvider, CloudAgentsOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _host = host;
        _timeProvider = timeProvider;
        _dispatch = options?.Dispatch ?? (static action => action());
        _filterOptions =
        [
            new CloudFilterOption(null, CloudCopy.AllStatuses),
            .. CloudAgentStatusMap.All.Select(static status => new CloudFilterOption(status, CloudAgentStatusMap.TextOf(status))),
        ];
        _host.Changes += OnHostChanges;
    }

    /// <summary>Occurs when the user asks to open an agent's session in Agents.</summary>
    public event EventHandler<CloudAgentOpenEventArgs>? OpenInAgentsRequested;

    /// <summary>Gets the state the page is in.</summary>
    public CloudAgentsViewState State => _state;

    /// <summary>Gets the reason the first load failed, or an empty string.</summary>
    public string LoadError => _loadError;

    /// <summary>Gets the reason the latest refresh failed, or an empty string. The rows then still show the last good list.</summary>
    public string RefreshError => _refreshError;

    /// <summary>Gets the reason cloud agents are not available, for <see cref="CloudAgentsViewState.NotConfigured"/>.</summary>
    public string NotConfiguredReason => _setup.NotConfiguredReason is { Length: > 0 } reason ? reason : CloudCopy.NotConfiguredIntro;

    /// <summary>Gets what the host offers for a new agent.</summary>
    public CloudSetup Setup => _setup;

    /// <summary>Gets the caps and counts, or null when they are not known.</summary>
    public CloudLimits? Limits => _limits;

    /// <summary>Gets the usage line, for example <c>3 agents, 1 awake (limit 2)</c>, or an empty string when the limits are not known.</summary>
    public string LimitsText => _limits is { } limits ? CloudCopy.LimitsLine(limits) : string.Empty;

    /// <summary>Gets the agents that match <see cref="StatusFilter"/>, newest activity first. Replaced as a whole when anything changes.</summary>
    public IReadOnlyList<CloudAgentRow> Rows => _rows;

    /// <summary>Gets the number of agents, ignoring the filter.</summary>
    public int TotalCount => _records.Count;

    /// <summary>Gets the entries of the status filter: all statuses first, then each status.</summary>
    public IReadOnlyList<CloudFilterOption> FilterOptions => _filterOptions;

    /// <summary>Gets or sets the status the list keeps, or null for every status.</summary>
    public CloudAgentStatus? StatusFilter
    {
        get => _statusFilter;
        set
        {
            if (_statusFilter == value)
            {
                return;
            }

            _statusFilter = value;
            NotifyPropertyChanged();
            Rebuild();
        }
    }

    /// <summary>Gets whether the filter hides every agent.</summary>
    public bool HasNoMatches => _statusFilter is not null && _rows.Length == 0 && _records.Count > 0;

    /// <summary>Gets the sentence of the filter-matches-nothing state.</summary>
    public string NoMatchText => _statusFilter is { } status ? CloudCopy.NoMatches(CloudAgentStatusMap.TextOf(status)) : string.Empty;

    /// <summary>Gets the identifier of the selected agent, or null.</summary>
    public string? SelectedId => _selectedId;

    /// <summary>Gets the details of the selected agent, or null when none is selected.</summary>
    public CloudAgentDetailsModel? Details => _details;

    /// <summary>Gets whether a refresh is running.</summary>
    public bool IsRefreshing => _isRefreshing;

    /// <summary>Gets whether New cloud agent is enabled: cloud is configured and the agent cap is not reached.</summary>
    public bool CanCreate => _state is CloudAgentsViewState.Empty or CloudAgentsViewState.Ready && _limits is not { AgentCapReached: true };

    /// <summary>Gets why New cloud agent is disabled while the page is otherwise usable, or an empty string.</summary>
    public string CreateDisabledReason => _state is CloudAgentsViewState.Empty or CloudAgentsViewState.Ready && _limits is { AgentCapReached: true, MaxAgents: { } max }
        ? CloudCopy.AgentCapReached(max)
        : string.Empty;

    /// <summary>Loads the setup and the agents. It shows <see cref="CloudAgentsViewState.Loading"/> first; a host that is not configured ends in <see cref="CloudAgentsViewState.NotConfigured"/> without listing anything. This method does not throw an expected failure.</summary>
    /// <param name="cancellationToken">Cancels the load.</param>
    /// <returns>A task that completes when the page is ready, empty, not configured or has failed to load.</returns>
    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        _loaded = false;
        _loadError = string.Empty;
        _refreshError = string.Empty;
        SetState(CloudAgentsViewState.Loading);
        try
        {
            var setup = await _host.GetSetupAsync(cancellationToken).ConfigureAwait(true);
            _setup = setup;
            if (!setup.IsConfigured)
            {
                _records = [];
                _limits = null;
                SetState(CloudAgentsViewState.NotConfigured);
                Rebuild();
                return;
            }

            await ReadAsync(cancellationToken).ConfigureAwait(true);
            _loaded = true;
            Rebuild();
        }
        catch (OperationCanceledException)
        {
            SetState(CloudAgentsViewState.Error);
            _loadError = CloudCopy.LoadCancelled;
            NotifyPropertyChanged(nameof(LoadError));
        }
        catch (Exception fault) when (CloudFailures.IsHandled(fault))
        {
            if (CloudFailures.KindOf(fault) == CloudFailureKind.NotConfigured)
            {
                _setup = CloudSetup.NotConfigured(CloudFailures.MessageOf(fault, CloudCopy.NotConfiguredIntro));
                SetState(CloudAgentsViewState.NotConfigured);
                return;
            }

            _loadError = CloudFailures.MessageOf(fault, CloudCopy.LoadFailed);
            SetState(CloudAgentsViewState.Error);
            NotifyPropertyChanged(nameof(LoadError));
        }
    }

    /// <summary>
    /// Reads the agents and limits again without showing the loading state. Calls that arrive while a refresh runs are folded into one more
    /// pass. A failure keeps the last good list and sets <see cref="RefreshError"/>. Before the first successful load it loads instead.
    /// </summary>
    /// <param name="cancellationToken">Cancels the refresh.</param>
    /// <returns>A task that completes when the refresh ended.</returns>
    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        if (_disposed)
        {
            return;
        }

        if (!_loaded)
        {
            if (_state != CloudAgentsViewState.Loading)
            {
                await LoadAsync(cancellationToken).ConfigureAwait(true);
            }

            return;
        }

        if (_isRefreshing)
        {
            _refreshAgain = true;
            return;
        }

        _isRefreshing = true;
        NotifyPropertyChanged(nameof(IsRefreshing));
        try
        {
            do
            {
                _refreshAgain = false;
                await RefreshOnceAsync(cancellationToken).ConfigureAwait(true);
            }
            while (_refreshAgain && !_disposed && !cancellationToken.IsCancellationRequested);
        }
        finally
        {
            _isRefreshing = false;
            NotifyPropertyChanged(nameof(IsRefreshing));
        }
    }

    /// <summary>Selects an agent and builds its details. An identifier that is not in the list clears the selection.</summary>
    /// <param name="agentId">The agent, or null to clear.</param>
    public void Select(string? agentId)
    {
        var record = agentId is null ? null : _records.FirstOrDefault(candidate => string.Equals(candidate.Id, agentId, StringComparison.Ordinal));
        if (record is null)
        {
            SetSelection(null, null);
            return;
        }

        if (string.Equals(_selectedId, record.Id, StringComparison.Ordinal) && _details is not null)
        {
            return;
        }

        var details = new CloudAgentDetailsModel(_host, _timeProvider, record);
        SetSelection(record.Id, details);
    }

    /// <summary>Creates the model of the new-agent dialog from the current setup and limits. It makes no host call.</summary>
    /// <returns>The dialog model, or null when <see cref="CanCreate"/> is false.</returns>
    public NewCloudAgentModel? BeginNewAgent()
    {
        if (!CanCreate)
        {
            return null;
        }

        var model = new NewCloudAgentModel(_host, _setup, _limits);
        model.Created += OnAgentCreated;
        return model;
    }

    /// <summary>Recomputes the relative times of the rows and the details. A view calls it on its clock tick.</summary>
    public void RefreshTimes()
    {
        Rebuild();
        _details?.RefreshTimes();
    }

    /// <summary>Stops listening to the host.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _host.Changes -= OnHostChanges;
        DetachDetails();
    }

    private void OnHostChanges(object? sender, EventArgs e)
    {
        if (!_disposed)
        {
            _dispatch(StartRefresh);
        }
    }

    // The refresh reports its failures through RefreshError, so the task is not observed here.
    private void StartRefresh()
    {
        if (!_disposed)
        {
            _ = RefreshAsync(CancellationToken.None);
        }
    }

    private void OnAgentCreated(object? sender, CloudAgentCreatedEventArgs e)
    {
        if (sender is NewCloudAgentModel model)
        {
            model.Created -= OnAgentCreated;
        }

        // The new agent may not be in the host's list yet, so it is added to the snapshot and selected; the next refresh confirms it.
        if (_records.All(record => !string.Equals(record.Id, e.Agent.Id, StringComparison.Ordinal)))
        {
            _records = [.. _records, e.Agent];
            if (_state == CloudAgentsViewState.Empty)
            {
                SetState(CloudAgentsViewState.Ready);
            }

            Rebuild();
        }

        Select(e.Agent.Id);
        StartRefresh();
    }

    private async Task ReadAsync(CancellationToken cancellationToken)
    {
        var records = await _host.ListAsync(cancellationToken).ConfigureAwait(true);
        var limits = await _host.GetLimitsAsync(cancellationToken).ConfigureAwait(true);
        _records = records;
        _limits = limits;
        SetState(records.Count == 0 ? CloudAgentsViewState.Empty : CloudAgentsViewState.Ready);
        NotifyPropertyChanged(nameof(LimitsText));
        NotifyPropertyChanged(nameof(Limits));
        NotifyPropertyChanged(nameof(TotalCount));
        NotifyPropertyChanged(nameof(CanCreate));
        NotifyPropertyChanged(nameof(CreateDisabledReason));
    }

    private async Task RefreshOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            await ReadAsync(cancellationToken).ConfigureAwait(true);
            SetRefreshError(string.Empty);
            Rebuild();
        }
        catch (OperationCanceledException)
        {
            // A cancelled refresh keeps the last good list and says nothing.
        }
        catch (Exception fault) when (CloudFailures.IsHandled(fault))
        {
            if (CloudFailures.KindOf(fault) == CloudFailureKind.NotConfigured)
            {
                _setup = CloudSetup.NotConfigured(CloudFailures.MessageOf(fault, CloudCopy.NotConfiguredIntro));
                _loaded = false;
                _records = [];
                _limits = null;
                SetState(CloudAgentsViewState.NotConfigured);
                Rebuild();
                return;
            }

            SetRefreshError(CloudFailures.MessageOf(fault, CloudCopy.RefreshFailed));
        }
    }

    private void Rebuild()
    {
        var now = _timeProvider.GetUtcNow();
        _rows = _records
            .Where(record => _statusFilter is null || record.Status == _statusFilter)
            .OrderByDescending(static record => record.LastActivityAt ?? record.CreatedAt)
            .ThenBy(static record => record.Name, StringComparer.Ordinal)
            .ThenBy(static record => record.Id, StringComparer.Ordinal)
            .Select(record => CloudAgentRow.From(record, now))
            .ToArray();
        NotifyPropertyChanged(nameof(Rows));
        NotifyPropertyChanged(nameof(TotalCount));
        NotifyPropertyChanged(nameof(HasNoMatches));
        NotifyPropertyChanged(nameof(NoMatchText));

        if (_selectedId is null)
        {
            return;
        }

        var selected = _records.FirstOrDefault(record => string.Equals(record.Id, _selectedId, StringComparison.Ordinal));
        if (selected is null || _details is { IsDeleted: true })
        {
            SetSelection(null, null);
        }
        else
        {
            _details?.Update(selected);
        }
    }

    private void SetSelection(string? id, CloudAgentDetailsModel? details)
    {
        if (string.Equals(_selectedId, id, StringComparison.Ordinal) && ReferenceEquals(_details, details))
        {
            return;
        }

        DetachDetails();
        _selectedId = id;
        _details = details;
        if (details is not null)
        {
            details.ActionCompleted += OnDetailsActionCompleted;
            details.OpenInAgentsRequested += OnDetailsOpenRequested;
        }

        NotifyPropertyChanged(nameof(SelectedId));
        NotifyPropertyChanged(nameof(Details));
    }

    private void DetachDetails()
    {
        if (_details is null)
        {
            return;
        }

        _details.ActionCompleted -= OnDetailsActionCompleted;
        _details.OpenInAgentsRequested -= OnDetailsOpenRequested;
    }

    private void OnDetailsActionCompleted(object? sender, EventArgs e)
    {
        if (sender is CloudAgentDetailsModel { IsDeleted: true } deleted)
        {
            _records = _records.Where(record => !string.Equals(record.Id, deleted.Id, StringComparison.Ordinal)).ToArray();
            SetState(_records.Count == 0 ? CloudAgentsViewState.Empty : CloudAgentsViewState.Ready);
            Rebuild();
        }

        StartRefresh();
    }

    private void OnDetailsOpenRequested(object? sender, CloudAgentOpenEventArgs e) => OpenInAgentsRequested?.Invoke(this, e);

    private void SetState(CloudAgentsViewState state)
    {
        if (_state == state)
        {
            return;
        }

        _state = state;
        NotifyPropertyChanged(nameof(State));
        NotifyPropertyChanged(nameof(CanCreate));
        NotifyPropertyChanged(nameof(CreateDisabledReason));
        NotifyPropertyChanged(nameof(NotConfiguredReason));
    }

    private void SetRefreshError(string message)
    {
        if (!string.Equals(_refreshError, message, StringComparison.Ordinal))
        {
            _refreshError = message;
            NotifyPropertyChanged(nameof(RefreshError));
        }
    }
}
