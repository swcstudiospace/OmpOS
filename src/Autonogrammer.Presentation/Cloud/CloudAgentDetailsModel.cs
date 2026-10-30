using Autonogrammer.Presentation.AgentPanel;
using Autonogrammer.Presentation.Observable;

namespace Autonogrammer.Presentation.Cloud;

/// <summary>
/// The details of one cloud agent: its status and history, repositories, branch, pull requests and last activity, and the commands Wake,
/// Stop, Delete (after a confirmation), Send a message, Open pull request and Open in Agents. A command that does not fit the status is
/// disabled, never hidden behind a failure. An expected failure becomes <see cref="ActionError"/>; nothing here throws it.
/// </summary>
public sealed class CloudAgentDetailsModel : ObservableModel
{
    private const int MaxTextChars = 300;

    private readonly ICloudAgentsHost _host;
    private readonly TimeProvider _timeProvider;
    private CloudAgentRecord _record;
    private IReadOnlyList<CloudTimelineRow> _timeline = [];
    private IReadOnlyList<CloudPullRequestRow> _pullRequests = [];
    private bool _isBusy;
    private bool _isConfirmingDelete;
    private bool _isDeleted;
    private string _notice = string.Empty;
    private string _actionError = string.Empty;
    private string _promptText = string.Empty;

    /// <summary>Initializes a new instance of the <see cref="CloudAgentDetailsModel"/> class.</summary>
    /// <param name="host">Where the commands go.</param>
    /// <param name="timeProvider">The clock for the relative times.</param>
    /// <param name="record">The agent to show.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public CloudAgentDetailsModel(ICloudAgentsHost host, TimeProvider timeProvider, CloudAgentRecord record)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(record);

        _host = host;
        _timeProvider = timeProvider;
        _record = record;
        Rebuild();
    }

    /// <summary>Occurs after a command succeeded, so the owner of the list refreshes it.</summary>
    public event EventHandler? ActionCompleted;

    /// <summary>Occurs when the user asks to open the agent's session in Agents.</summary>
    public event EventHandler<CloudAgentOpenEventArgs>? OpenInAgentsRequested;

    /// <summary>Gets the agent as the host last reported it.</summary>
    public CloudAgentRecord Record => _record;

    /// <summary>Gets the agent identifier.</summary>
    public string Id => _record.Id;

    /// <summary>Gets the display name.</summary>
    public string Name => NameOf(_record);

    /// <summary>Gets the status.</summary>
    public CloudAgentStatus Status => _record.Status;

    /// <summary>Gets the status in words.</summary>
    public string StatusText => CloudAgentStatusMap.TextOf(_record.Status);

    /// <summary>Gets the glyph key of the status.</summary>
    public string IconKey => CloudAgentStatusMap.IconOf(_record.Status);

    /// <summary>Gets the sentence that explains the status.</summary>
    public string StatusDescription => CloudAgentStatusMap.DescriptionOf(_record.Status);

    /// <summary>Gets the reason the host gave for the status, or an empty string.</summary>
    public string StatusDetail => PanelText.Plain(_record.StatusDetail, MaxTextChars);

    /// <summary>Gets the repositories as <c>owner/repository</c> names.</summary>
    public IReadOnlyList<string> Repositories => _record.Repositories.Select(static repository => repository.DisplayName).ToArray();

    /// <summary>Gets the branch line, for example <c>Branch: autonogrammer/ab12</c>, or the words for a branch that does not exist yet.</summary>
    public string BranchText => CloudCopy.BranchLine(PanelText.Plain(_record.Branch, MaxTextChars));

    /// <summary>Gets the base branch line.</summary>
    public string BaseBranchText => CloudCopy.BaseBranchLine(PanelText.Plain(_record.BaseBranch, MaxTextChars));

    /// <summary>Gets the environment line.</summary>
    public string EnvironmentText => CloudCopy.EnvironmentLine(PanelText.Plain(_record.EnvironmentName, MaxTextChars));

    /// <summary>Gets the model line.</summary>
    public string ModelText => CloudCopy.ModelLine(PanelText.Plain(_record.Model, MaxTextChars));

    /// <summary>Gets the idle line, for example <c>Sleeps after 15 minutes without activity</c>, or an empty string when the host did not report it.</summary>
    public string IdleText => _record.IdleMinutes > 0 ? CloudCopy.IdleLine(_record.IdleMinutes) : string.Empty;

    /// <summary>Gets the last activity line.</summary>
    public string LastActivityText => CloudCopy.LastActivityLine(
        _record.LastActivityAt is { } at ? RelativeTimeText.Format(at, _timeProvider.GetUtcNow()) : CloudCopy.NoActivity);

    /// <summary>Gets the status history, oldest first.</summary>
    public IReadOnlyList<CloudTimelineRow> Timeline => _timeline;

    /// <summary>Gets one pull request line per repository.</summary>
    public IReadOnlyList<CloudPullRequestRow> PullRequests => _pullRequests;

    /// <summary>Gets whether a command is running. Every command is disabled meanwhile.</summary>
    public bool IsBusy => _isBusy;

    /// <summary>Gets whether the agent was deleted. The details then stay readable but every command is disabled.</summary>
    public bool IsDeleted => _isDeleted;

    /// <summary>Gets whether Wake is enabled.</summary>
    public bool CanWake => IsAvailable && CloudAgentStatusMap.CanWake(_record.Status);

    /// <summary>Gets whether Stop is enabled.</summary>
    public bool CanStop => IsAvailable && CloudAgentStatusMap.CanStop(_record.Status);

    /// <summary>Gets whether Delete is enabled.</summary>
    public bool CanDelete => IsAvailable && !_isConfirmingDelete && CloudAgentStatusMap.CanDelete(_record.Status);

    /// <summary>Gets whether Open in Agents is enabled: the status allows it and the host reported a session.</summary>
    public bool CanOpenInAgents => !_isDeleted && CloudAgentStatusMap.CanOpenInAgents(_record.Status) && !string.IsNullOrEmpty(_record.SessionId);

    /// <summary>Gets whether the message box is enabled: the agent is running or idle and no command is running.</summary>
    public bool CanPrompt => IsAvailable && CloudAgentStatusMap.CanPrompt(_record.Status);

    /// <summary>Gets whether Send is enabled: <see cref="CanPrompt"/> and the message has text.</summary>
    public bool CanSendPrompt => CanPrompt && !string.IsNullOrWhiteSpace(_promptText);

    /// <summary>Gets whether the delete confirmation is showing.</summary>
    public bool IsConfirmingDelete => _isConfirmingDelete;

    /// <summary>Gets the question of the delete confirmation: what is removed and what stays.</summary>
    public string DeleteWarning => CloudCopy.DeleteWarning(Name);

    /// <summary>Gets or sets the message typed for the agent. Sending clears it.</summary>
    public string PromptText
    {
        get => _promptText;
        set
        {
            var next = value ?? string.Empty;
            if (SetProperty(ref _promptText, next))
            {
                NotifyPropertyChanged(nameof(CanSendPrompt));
            }
        }
    }

    /// <summary>Gets the polite announcement of the latest command that succeeded, or an empty string.</summary>
    public string Notice => _notice;

    /// <summary>Gets the reason the latest command failed, or an empty string. It is announced assertively.</summary>
    public string ActionError => _actionError;

    /// <summary>Replaces the record with a newer snapshot of the same agent. The confirmation and the typed message stay.</summary>
    /// <param name="record">The new snapshot.</param>
    /// <exception cref="ArgumentNullException"><paramref name="record"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="record"/> belongs to another agent.</exception>
    public void Update(CloudAgentRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (!string.Equals(record.Id, _record.Id, StringComparison.Ordinal))
        {
            throw new ArgumentException("The record belongs to another agent.", nameof(record));
        }

        _record = record;
        if (!CloudAgentStatusMap.CanDelete(record.Status))
        {
            _isConfirmingDelete = false;
        }

        Rebuild();
        Publish();
    }

    /// <summary>Recomputes the relative times. A view calls it on its clock tick.</summary>
    public void RefreshTimes()
    {
        Rebuild();
        Publish();
    }

    /// <summary>Wakes the agent when <see cref="CanWake"/>; otherwise does nothing.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the command ended or failed.</returns>
    public Task WakeAsync(CancellationToken cancellationToken) =>
        !CanWake ? Task.CompletedTask : RunAsync(
            async token => Apply(await _host.WakeAsync(_record.Id, token).ConfigureAwait(true)),
            CloudCopy.WakeRequested(Name),
            cancellationToken);

    /// <summary>Stops the agent's worker when <see cref="CanStop"/>; otherwise does nothing.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the command ended or failed.</returns>
    public Task StopAsync(CancellationToken cancellationToken) =>
        !CanStop ? Task.CompletedTask : RunAsync(
            async token => Apply(await _host.StopAsync(_record.Id, token).ConfigureAwait(true)),
            CloudCopy.StopRequested(Name),
            cancellationToken);

    /// <summary>Shows the delete confirmation when <see cref="CanDelete"/>. Nothing is deleted until <see cref="ConfirmDeleteAsync"/>.</summary>
    public void RequestDelete()
    {
        if (!CanDelete)
        {
            return;
        }

        _isConfirmingDelete = true;
        _actionError = string.Empty;
        _notice = string.Empty;
        Publish();
    }

    /// <summary>Hides the delete confirmation without deleting.</summary>
    public void CancelDelete()
    {
        if (!_isConfirmingDelete)
        {
            return;
        }

        _isConfirmingDelete = false;
        Publish();
    }

    /// <summary>Deletes the agent. It does nothing unless the confirmation is showing.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the command ended or failed.</returns>
    public async Task ConfirmDeleteAsync(CancellationToken cancellationToken)
    {
        if (!_isConfirmingDelete || _isBusy || !CloudAgentStatusMap.CanDelete(_record.Status))
        {
            return;
        }

        _isConfirmingDelete = false;
        var deleted = await RunCoreAsync(
            async token => await _host.DeleteAsync(_record.Id, token).ConfigureAwait(true),
            CloudCopy.DeleteRequested(Name),
            cancellationToken).ConfigureAwait(true);
        if (deleted)
        {
            _isDeleted = true;
            Publish();
            ActionCompleted?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Sends <see cref="PromptText"/> to the agent when <see cref="CanSendPrompt"/>. The text is cleared once the service accepted it.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the command ended or failed.</returns>
    public Task SendPromptAsync(CancellationToken cancellationToken) =>
        !CanSendPrompt ? Task.CompletedTask : RunAsync(
            async token =>
            {
                await _host.PromptAsync(_record.Id, _promptText.Trim(), token).ConfigureAwait(true);
                PromptText = string.Empty;
            },
            CloudCopy.PromptSent(Name),
            cancellationToken);

    /// <summary>
    /// Opens the pull request of a repository: shows the page when the pull request exists, otherwise asks the host to push the branch and
    /// open a draft pull request, then shows it. It does nothing for a repository that is not the agent's or when commands are disabled.
    /// </summary>
    /// <param name="repository">The repository address of a <see cref="CloudPullRequestRow"/>.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the command ended or failed.</returns>
    public Task OpenPullRequestAsync(string repository, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repository);

        var row = _pullRequests.FirstOrDefault(candidate => string.Equals(candidate.Repository, repository, StringComparison.Ordinal));
        if (row is null || !row.CanAct)
        {
            return Task.CompletedTask;
        }

        var existing = _record.PullRequests.FirstOrDefault(link => string.Equals(link.Repository, repository, StringComparison.Ordinal));
        if (existing is not null)
        {
            return RunAsync(
                token => _host.OpenPullRequestAsync(existing, token),
                CloudCopy.PullRequestOpened(row.RepositoryName),
                cancellationToken);
        }

        var request = new CloudPullRequestRequest
        {
            Repository = repository,
            Title = Name,
            BaseBranch = string.IsNullOrWhiteSpace(_record.BaseBranch) ? null : _record.BaseBranch.Trim(),
        };
        return RunAsync(
            async token =>
            {
                var created = await _host.CreatePullRequestAsync(_record.Id, request, token).ConfigureAwait(true);
                _record = _record with { PullRequests = [.. _record.PullRequests, created] };
                Rebuild();
                await _host.OpenPullRequestAsync(created, token).ConfigureAwait(true);
            },
            CloudCopy.PullRequestCreated(row.RepositoryName),
            cancellationToken);
    }

    /// <summary>Asks to open the agent's session in Agents when <see cref="CanOpenInAgents"/>; the owner of the engines opens it.</summary>
    public void OpenInAgents()
    {
        if (CanOpenInAgents && _record.SessionId is { } sessionId)
        {
            OpenInAgentsRequested?.Invoke(this, new CloudAgentOpenEventArgs(_record.Id, sessionId));
        }
    }

    private static string NameOf(CloudAgentRecord record)
    {
        var name = PanelText.Plain(record.Name, MaxTextChars);
        return name.Length == 0 ? CloudCopy.UntitledAgent : name;
    }

    private bool IsAvailable => !_isBusy && !_isDeleted;

    private void Apply(CloudAgentRecord record)
    {
        if (string.Equals(record.Id, _record.Id, StringComparison.Ordinal))
        {
            _record = record;
            Rebuild();
        }
    }

    private async Task RunAsync(Func<CancellationToken, Task> action, string notice, CancellationToken cancellationToken)
    {
        if (!await RunCoreAsync(action, notice, cancellationToken).ConfigureAwait(true))
        {
            return;
        }

        ActionCompleted?.Invoke(this, EventArgs.Empty);
    }

    private async Task<bool> RunCoreAsync(Func<CancellationToken, Task> action, string notice, CancellationToken cancellationToken)
    {
        if (_isBusy)
        {
            return false;
        }

        _isBusy = true;
        _actionError = string.Empty;
        _notice = string.Empty;
        Rebuild();
        Publish();
        try
        {
            await action(cancellationToken).ConfigureAwait(true);
            _notice = notice;
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception fault) when (CloudFailures.IsHandled(fault))
        {
            _actionError = CloudFailures.MessageOf(fault, CloudCopy.ActionFailed);
            return false;
        }
        finally
        {
            _isBusy = false;
            Rebuild();
            Publish();
        }
    }

    private void Rebuild()
    {
        var now = _timeProvider.GetUtcNow();
        _timeline = _record.Timeline
            .Select(step => ToRow(step, now))
            .ToArray();
        _pullRequests = _record.Repositories
            .Select(repository => ToRow(repository))
            .ToArray();
    }

    private static CloudTimelineRow ToRow(CloudStatusEvent step, DateTimeOffset now)
    {
        var text = CloudAgentStatusMap.TextOf(step.Status);
        var time = RelativeTimeText.Format(step.At, now);
        var note = PanelText.Plain(step.Note, MaxTextChars);
        return new CloudTimelineRow(step.Status, text, CloudAgentStatusMap.IconOf(step.Status), time, note, CloudCopy.TimelineAccessibleName(text, time, note));
    }

    private CloudPullRequestRow ToRow(CloudRepository repository)
    {
        var link = _record.PullRequests.FirstOrDefault(candidate => string.Equals(candidate.Repository, repository.Address, StringComparison.Ordinal));
        var has = link is not null;
        var canAct = IsAvailable && (has || CloudAgentStatusMap.CanCreatePullRequest(_record.Status));
        return new CloudPullRequestRow(
            repository.Address,
            repository.DisplayName,
            has,
            PanelText.Plain(link?.Title, MaxTextChars),
            has ? CloudCopy.OpenPullRequestLabel : CloudCopy.CreatePullRequestLabel,
            has ? CloudCopy.OpenPullRequestName(repository.DisplayName) : CloudCopy.CreatePullRequestName(repository.DisplayName),
            canAct);
    }

    // Every derived property can change with the record or the busy flag, so one notification for all of them is the honest signal.
    private void Publish() => NotifyPropertyChanged(string.Empty);
}
