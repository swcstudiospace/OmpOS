using Autonogrammer.Api.Cloud.Options;
using Autonogrammer.Contracts.Cloud;
using Microsoft.Extensions.Options;

namespace Autonogrammer.Api.Cloud;

/// <summary>
/// The control plane's side of one run: before the engine session opens, find the agent from its session, check that it may run (status, caps, compute
/// budget), wake its worker and work out the endpoint; after the run, record the activity and the engine's session id. Any instance can do this: it
/// reads the durable store, not process memory.
/// </summary>
internal sealed class CloudAgentRuntime
{
    private readonly ICloudAgentStore _store;
    private readonly CloudAgentLifecycle _lifecycle;
    private readonly ICloudWorkerEndpoints _endpoints;
    private readonly CloudRedactorFactory _redactors;
    private readonly IOptionsMonitor<CloudOptions> _options;
    private readonly TimeProvider _time;
    private readonly CloudMetrics _metrics;
    private readonly CloudRunTracker _runs;

    /// <summary>Initializes a new instance of the <see cref="CloudAgentRuntime"/> class.</summary>
    /// <param name="store">The agent store.</param>
    /// <param name="lifecycle">The worker lifecycle.</param>
    /// <param name="endpoints">The worker endpoint resolver.</param>
    /// <param name="redactors">The redactor factory.</param>
    /// <param name="options">The cloud options.</param>
    /// <param name="time">The clock.</param>
    /// <param name="metrics">The cloud metrics.</param>
    /// <param name="runs">The tracker of runs in flight.</param>
    public CloudAgentRuntime(
        ICloudAgentStore store,
        CloudAgentLifecycle lifecycle,
        ICloudWorkerEndpoints endpoints,
        CloudRedactorFactory redactors,
        IOptionsMonitor<CloudOptions> options,
        TimeProvider time,
        CloudMetrics metrics,
        CloudRunTracker runs)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(lifecycle);
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(redactors);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(runs);
        _store = store;
        _lifecycle = lifecycle;
        _endpoints = endpoints;
        _redactors = redactors;
        _options = options;
        _time = time;
        _metrics = metrics;
        _runs = runs;
    }

    /// <summary>Prepares a run of the agent that owns <paramref name="sessionId"/>: wakes its worker and returns how to reach it.</summary>
    /// <param name="sessionId">The session the run belongs to.</param>
    /// <param name="cancellationToken">Cancels the preparation.</param>
    /// <returns>The attachment.</returns>
    /// <exception cref="CloudProvisioningException">The session has no cloud agent, the agent may not run, or its worker is missing or did not wake. The message is safe to show.</exception>
    public async Task<CloudAgentAttachment> AttachAsync(string sessionId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        var settings = _options.CurrentValue;
        var agent = await _store.GetBySessionAsync(sessionId, cancellationToken).ConfigureAwait(false)
            ?? throw new CloudProvisioningException("agent_not_found", "This session belongs to no cloud agent.");

        if (settings.Limits.MaxComputeSecondsPerAgent > 0 && ComputeSeconds(agent) >= settings.Limits.MaxComputeSecondsPerAgent)
        {
            _metrics.LimitRefused("compute");
            throw new CloudProvisioningException("compute_limit_reached", "The agent used its compute budget.");
        }

        switch (agent.Status)
        {
            case CloudAgentStatus.Starting:
            case CloudAgentStatus.Running:
                break;
            case CloudAgentStatus.Idle:
                var activation = await _store.TryActivateAsync(agent.Id, settings.Limits.MaxConcurrentRunningPerOwner, _time.GetUtcNow(), cancellationToken).ConfigureAwait(false);
                if (activation.Outcome == CloudAgentActivationOutcome.LimitReached)
                {
                    _metrics.LimitRefused("running");
                    throw new CloudProvisioningException("running_limit_reached", "The owner already has the maximum number of running agents.");
                }

                if (activation.Outcome is CloudAgentActivationOutcome.NotFound or CloudAgentActivationOutcome.NotWakeable)
                {
                    throw new CloudProvisioningException("agent_not_wakeable", "The agent cannot be woken now.");
                }

                break;
            case CloudAgentStatus.Provisioning:
                throw new CloudProvisioningException("agent_starting", "The agent is still being provisioned.");
            case CloudAgentStatus.Stopped:
                throw new CloudProvisioningException("agent_stopped", "The agent is stopped; wake it first.");
            case CloudAgentStatus.LimitReached:
                throw new CloudProvisioningException("compute_limit_reached", "The agent used its compute budget.");
            default:
                throw new CloudProvisioningException("agent_" + CloudAgentStatusWord.Of(agent.Status), "The agent cannot run in its current state.");
        }

        var container = await _lifecycle.EnsureWorkerAwakeAsync(agent, cancellationToken).ConfigureAwait(false);
        Uri endpoint;
        try
        {
            endpoint = _endpoints.Resolve(container);
        }
        catch (InvalidOperationException)
        {
            throw new CloudProvisioningException("worker_unreachable", "The worker has no usable address.");
        }

        var current = await _store.GetAsync(agent.Id, cancellationToken).ConfigureAwait(false) ?? agent;
        var redactor = await _redactors.CreateAsync(current, cancellationToken).ConfigureAwait(false);
        var directory = CloudWorkerLayout.PrimaryWorkingDirectory(settings.Worker.WorkspaceRoot, current, settings.Git.AllowedHosts);
        return new CloudAgentAttachment(current, endpoint, directory, redactor);
    }

    /// <summary>Records that a run ended: its cancellation handle is released and the idle window restarts from now.</summary>
    /// <param name="sessionId">The session the run belonged to.</param>
    /// <param name="agentId">The agent identifier, or null when the run never attached to an agent.</param>
    /// <returns>A task that completes when the record is updated.</returns>
    public Task RunEndedAsync(string sessionId, string? agentId)
    {
        _runs.Complete(sessionId);
        return agentId is null ? Task.CompletedTask : _lifecycle.TouchAsync(agentId, CancellationToken.None);
    }

    /// <summary>Records the engine's own session identifier, so the next run can resume the conversation with <c>session/load</c>.</summary>
    /// <param name="agentId">The agent identifier.</param>
    /// <param name="nativeSessionId">The engine session identifier.</param>
    /// <returns>A task that completes when the record is updated.</returns>
    public async Task RecordNativeSessionAsync(string agentId, string nativeSessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(nativeSessionId);
        _ = await _store.UpdateAsync(
            agentId,
            current => string.Equals(current.NativeSessionId, nativeSessionId, StringComparison.Ordinal) ? current : current with { NativeSessionId = nativeSessionId, UpdatedAt = _time.GetUtcNow() },
            CancellationToken.None).ConfigureAwait(false);
    }

    private double ComputeSeconds(CloudAgentRecord agent) =>
        agent.ComputeSeconds + (agent.ActiveSince is { } since ? Math.Max(0, (_time.GetUtcNow() - since).TotalSeconds) : 0);
}
