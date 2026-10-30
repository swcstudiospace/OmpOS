using System.Globalization;

namespace Autonogrammer.Presentation.Cloud;

/// <summary>
/// An in-memory <see cref="ICloudAgentsHost"/> for tests and for heads that have no service yet. It keeps its agents in a list, records
/// every call, and fails on request with a chosen <see cref="CloudHostException"/>. It makes no network call and reads time only from the
/// <see cref="TimeProvider"/> it is given.
/// </summary>
public sealed class FakeCloudAgentsHost : ICloudAgentsHost
{
    private readonly TimeProvider _timeProvider;
    private readonly List<CloudAgentRecord> _agents = [];
    private readonly Dictionary<string, CloudHostException> _failures = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TaskCompletionSource> _gates = new(StringComparer.Ordinal);
    private int _nextId = 1;

    /// <summary>Initializes a new instance of the <see cref="FakeCloudAgentsHost"/> class.</summary>
    /// <param name="timeProvider">The clock that stamps created agents.</param>
    /// <exception cref="ArgumentNullException"><paramref name="timeProvider"/> is null.</exception>
    public FakeCloudAgentsHost(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        _timeProvider = timeProvider;
    }

    /// <inheritdoc/>
    public event EventHandler? Changes;

    /// <summary>Gets or sets what <see cref="GetSetupAsync"/> returns. It starts configured with no environment and no model to choose.</summary>
    public CloudSetup Setup { get; set; } = new() { IsConfigured = true };

    /// <summary>Gets or sets the caps. The fake refuses a create with <see cref="CloudFailureKind.LimitReached"/> when the agent cap is reached.</summary>
    public int? MaxAgents { get; set; }

    /// <summary>Gets or sets the most awake workers, reported in <see cref="GetLimitsAsync"/>.</summary>
    public int? MaxAwakeAgents { get; set; }

    /// <summary>Gets the agents the fake holds.</summary>
    public IReadOnlyList<CloudAgentRecord> Agents => _agents;

    /// <summary>Gets every call as <c>Operation</c> or <c>Operation:argument</c>, in order.</summary>
    public IList<string> Calls { get; } = [];

    /// <summary>Gets the requests of every <see cref="CreateAsync"/> call, whether it succeeded or not.</summary>
    public IList<CloudAgentRequest> CreateRequests { get; } = [];

    /// <summary>Gets the messages sent with <see cref="PromptAsync"/>, as <c>agentId: text</c>.</summary>
    public IList<string> Prompts { get; } = [];

    /// <summary>Gets the pull request requests of <see cref="CreatePullRequestAsync"/>.</summary>
    public IList<CloudPullRequestRequest> PullRequestRequests { get; } = [];

    /// <summary>Gets the pull request pages shown through <see cref="OpenPullRequestAsync"/>.</summary>
    public IList<CloudPullRequestLink> OpenedPullRequests { get; } = [];

    /// <summary>Gets the number of calls of an operation, for example <c>Create</c>.</summary>
    /// <param name="operation">The operation name without argument.</param>
    /// <returns>The number of calls.</returns>
    public int CountOf(string operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        return Calls.Count(call => string.Equals(call, operation, StringComparison.Ordinal)
            || call.StartsWith(operation + ":", StringComparison.Ordinal));
    }

    /// <summary>Adds an agent to the list without a call, as if it existed already.</summary>
    /// <param name="agent">The agent.</param>
    public void Add(CloudAgentRecord agent)
    {
        ArgumentNullException.ThrowIfNull(agent);

        _agents.Add(agent);
    }

    /// <summary>Replaces an agent's status without a call and without raising <see cref="Changes"/>, as if the worker changed on its own.</summary>
    /// <param name="agentId">The agent.</param>
    /// <param name="status">The new status.</param>
    /// <exception cref="ArgumentException">No such agent.</exception>
    public void SetStatus(string agentId, CloudAgentStatus status)
    {
        ArgumentNullException.ThrowIfNull(agentId);

        var index = IndexOf(agentId);
        _agents[index] = _agents[index] with { Status = status, LastActivityAt = _timeProvider.GetUtcNow() };
    }

    /// <summary>Makes the next call of an operation throw. The failure is used once. Operations: Setup, Limits, List, Create, Wake, Stop, Delete, Prompt, CreatePullRequest, OpenPullRequest.</summary>
    /// <param name="operation">The operation name.</param>
    /// <param name="failure">What to throw.</param>
    public void FailNext(string operation, CloudHostException failure)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(failure);

        _failures[operation] = failure;
    }

    /// <summary>Makes the next call of an operation wait until the returned action is called, so a test can observe the busy state.</summary>
    /// <param name="operation">The operation name.</param>
    /// <returns>The action that releases the call.</returns>
    public Action HoldNext(string operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _gates[operation] = gate;
        return () => gate.TrySetResult();
    }

    /// <summary>Raises <see cref="Changes"/>, as a service does when an agent changes.</summary>
    public void PublishChanges() => Changes?.Invoke(this, EventArgs.Empty);

    /// <inheritdoc/>
    public async Task<CloudSetup> GetSetupAsync(CancellationToken cancellationToken)
    {
        await EnterAsync("Setup", "Setup", cancellationToken).ConfigureAwait(true);
        return Setup;
    }

    /// <inheritdoc/>
    public async Task<CloudLimits> GetLimitsAsync(CancellationToken cancellationToken)
    {
        await EnterAsync("Limits", "Limits", cancellationToken).ConfigureAwait(true);
        return new CloudLimits(_agents.Count, MaxAgents, _agents.Count(static agent => IsAwake(agent.Status)), MaxAwakeAgents);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CloudAgentRecord>> ListAsync(CancellationToken cancellationToken)
    {
        await EnterAsync("List", "List", cancellationToken).ConfigureAwait(true);
        return [.. _agents];
    }

    /// <inheritdoc/>
    public async Task<CloudAgentRecord> CreateAsync(CloudAgentRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        CreateRequests.Add(request);
        await EnterAsync("Create", "Create", cancellationToken).ConfigureAwait(true);
        if (MaxAgents is { } max && _agents.Count >= max)
        {
            throw new CloudHostException(CloudFailureKind.LimitReached, CloudCopy.AgentCapReached(max));
        }

        var number = _nextId++;
        var now = _timeProvider.GetUtcNow();
        var agent = new CloudAgentRecord
        {
            Id = string.Create(CultureInfo.InvariantCulture, $"cloud-{number}"),
            Name = request.Name,
            Status = CloudAgentStatus.Provisioning,
            CreatedAt = now,
            Repositories = request.Repositories,
            Branch = request.Branch ?? string.Create(CultureInfo.InvariantCulture, $"autonogrammer/cloud-{number}"),
            BaseBranch = request.BaseBranch,
            EnvironmentName = request.EnvironmentId,
            Model = request.Model,
            IdleMinutes = request.IdleMinutes,
            LastActivityAt = now,
            SessionId = string.Create(CultureInfo.InvariantCulture, $"session-{number}"),
            Timeline = [new CloudStatusEvent(now, CloudAgentStatus.Provisioning)],
        };
        _agents.Add(agent);
        return agent;
    }

    /// <inheritdoc/>
    public async Task<CloudAgentRecord> WakeAsync(string agentId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(agentId);

        await EnterAsync("Wake", "Wake:" + agentId, cancellationToken).ConfigureAwait(true);
        return Transition(agentId, CloudAgentStatus.Starting);
    }

    /// <inheritdoc/>
    public async Task<CloudAgentRecord> StopAsync(string agentId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(agentId);

        await EnterAsync("Stop", "Stop:" + agentId, cancellationToken).ConfigureAwait(true);
        return Transition(agentId, CloudAgentStatus.Stopped);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(string agentId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(agentId);

        await EnterAsync("Delete", "Delete:" + agentId, cancellationToken).ConfigureAwait(true);
        _agents.RemoveAt(IndexOf(agentId));
    }

    /// <inheritdoc/>
    public async Task PromptAsync(string agentId, string text, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(agentId);
        ArgumentNullException.ThrowIfNull(text);

        await EnterAsync("Prompt", "Prompt:" + agentId, cancellationToken).ConfigureAwait(true);
        Prompts.Add(agentId + ": " + text);
    }

    /// <inheritdoc/>
    public async Task<CloudPullRequestLink> CreatePullRequestAsync(string agentId, CloudPullRequestRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(agentId);
        ArgumentNullException.ThrowIfNull(request);

        PullRequestRequests.Add(request);
        await EnterAsync("CreatePullRequest", "CreatePullRequest:" + agentId, cancellationToken).ConfigureAwait(true);
        var index = IndexOf(agentId);
        var link = new CloudPullRequestLink(request.Repository, new Uri(request.Repository + "/pull/1"), request.Title);
        _agents[index] = _agents[index] with { PullRequests = [.. _agents[index].PullRequests, link] };
        return link;
    }

    /// <inheritdoc/>
    public async Task OpenPullRequestAsync(CloudPullRequestLink link, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(link);

        await EnterAsync("OpenPullRequest", "OpenPullRequest", cancellationToken).ConfigureAwait(true);
        OpenedPullRequests.Add(link);
    }

    private static bool IsAwake(CloudAgentStatus status) =>
        status is CloudAgentStatus.Starting or CloudAgentStatus.Running or CloudAgentStatus.Idle;

    private int IndexOf(string agentId)
    {
        var index = _agents.FindIndex(agent => string.Equals(agent.Id, agentId, StringComparison.Ordinal));
        return index >= 0 ? index : throw new CloudHostException(CloudFailureKind.NotFound, CloudCopy.AgentNotFound);
    }

    private CloudAgentRecord Transition(string agentId, CloudAgentStatus status)
    {
        var index = IndexOf(agentId);
        var now = _timeProvider.GetUtcNow();
        var agent = _agents[index];
        _agents[index] = agent with { Status = status, LastActivityAt = now, Timeline = [.. agent.Timeline, new CloudStatusEvent(now, status)] };
        return _agents[index];
    }

    private async Task EnterAsync(string operation, string call, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Calls.Add(call);
        if (_gates.Remove(operation, out var gate))
        {
            await gate.Task.WaitAsync(cancellationToken).ConfigureAwait(true);
        }

        if (_failures.Remove(operation, out var failure))
        {
            throw failure;
        }
    }
}
