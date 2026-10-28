using System.Diagnostics;
using Microsoft.Extensions.Options;
using Autonogrammer.Api.Cloud.Options;
using Autonogrammer.Api.Services;
using Autonogrammer.Api.Sse;
using Autonogrammer.Contracts.Cloud;
using Autonogrammer.Contracts.Errors;
using Autonogrammer.Contracts.Sessions;
using Autonogrammer.Sandbox;
using Autonogrammer.Sessions;

namespace Autonogrammer.Api.Cloud;

/// <summary>
/// The cloud agent operations behind the endpoints: create, read, prompt, cancel, stop, wake, delete, limits and the pull request. Each takes the caller
/// and treats an agent of another owner exactly like an absent one. An agent's event log is an ordinary session (<see cref="CloudAgentRecord.SessionId"/>),
/// so a prompt is admitted and run by the same machinery as a local one, and the cloud harness factory sends the run to the worker.
/// </summary>
internal sealed class CloudAgentService
{
    private readonly ICloudAgentStore _store;
    private readonly ISessionRepository _repository;
    private readonly SessionService _sessions;
    private readonly SessionRegistry _registry;
    private readonly SessionChangePublisher _publisher;
    private readonly PromptRunner _runner;
    private readonly CloudAgentLifecycle _lifecycle;
    private readonly CloudCreateRequestValidator _validator;
    private readonly CloudRedactorFactory _redactors;
    private readonly PullRequestService _pullRequests;
    private readonly CloudRunTracker _runs;
    private readonly CloudBackgroundTasks _background;
    private readonly CloudMetrics _metrics;
    private readonly IOptionsMonitor<CloudOptions> _options;
    private readonly TimeProvider _time;

    /// <summary>Initializes a new instance of the <see cref="CloudAgentService"/> class.</summary>
    /// <param name="store">The agent store.</param>
    /// <param name="repository">The session store that holds each agent's event log.</param>
    /// <param name="sessions">The session service that admits prompts.</param>
    /// <param name="registry">The registry of sessions that the catalog lists.</param>
    /// <param name="publisher">Announces session changes to streams.</param>
    /// <param name="runner">Acquires run leases and starts runs.</param>
    /// <param name="lifecycle">The worker lifecycle.</param>
    /// <param name="validator">The create request validator.</param>
    /// <param name="redactors">The redactor factory.</param>
    /// <param name="pullRequests">The pull request service.</param>
    /// <param name="runs">The tracker of runs in flight.</param>
    /// <param name="background">The background task runner.</param>
    /// <param name="metrics">The cloud metrics.</param>
    /// <param name="options">The cloud options.</param>
    /// <param name="time">The clock.</param>
    public CloudAgentService(
        ICloudAgentStore store,
        ISessionRepository repository,
        SessionService sessions,
        SessionRegistry registry,
        SessionChangePublisher publisher,
        PromptRunner runner,
        CloudAgentLifecycle lifecycle,
        CloudCreateRequestValidator validator,
        CloudRedactorFactory redactors,
        PullRequestService pullRequests,
        CloudRunTracker runs,
        CloudBackgroundTasks background,
        CloudMetrics metrics,
        IOptionsMonitor<CloudOptions> options,
        TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(publisher);
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(lifecycle);
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(redactors);
        ArgumentNullException.ThrowIfNull(pullRequests);
        ArgumentNullException.ThrowIfNull(runs);
        ArgumentNullException.ThrowIfNull(background);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(time);
        _store = store;
        _repository = repository;
        _sessions = sessions;
        _registry = registry;
        _publisher = publisher;
        _runner = runner;
        _lifecycle = lifecycle;
        _validator = validator;
        _redactors = redactors;
        _pullRequests = pullRequests;
        _runs = runs;
        _background = background;
        _metrics = metrics;
        _options = options;
        _time = time;
    }

    /// <summary>Creates an agent and starts provisioning its worker in the background.</summary>
    /// <param name="ownerId">The caller.</param>
    /// <param name="request">The request as received.</param>
    /// <param name="idempotencyKey">The Idempotency-Key header, or null.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The agent (in Provisioning, or the earlier agent for a replay), or the refusal.</returns>
    public async Task<CloudOutcome<CloudCreateResult>> CreateAsync(string ownerId, CreateCloudAgentRequest request, string? idempotencyKey, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        ArgumentNullException.ThrowIfNull(request);
        var settings = _options.CurrentValue;
        var checkedRequest = await _validator.ValidateAsync(ownerId, request, settings, cancellationToken).ConfigureAwait(false);
        if (checkedRequest.Problem is not null)
        {
            return CloudOutcome<CloudCreateResult>.Fail(checkedRequest.Problem);
        }

        var normalized = checkedRequest.Value!;
        var fingerprint = CloudCreateRequestValidator.Fingerprint(normalized);
        if (idempotencyKey is not null)
        {
            var earlier = await _store.FindByIdempotencyKeyAsync(ownerId, idempotencyKey, cancellationToken).ConfigureAwait(false);
            if (earlier is not null)
            {
                return ReplayOrConflict(earlier, fingerprint);
            }
        }

        var limits = new CloudAgentLimits(settings.Limits.MaxAgentsPerOwner, settings.Limits.MaxConcurrentRunningPerOwner);
        var usage = await _store.GetUsageAsync(ownerId, cancellationToken).ConfigureAwait(false);
        if (usage.Agents >= limits.MaxAgents)
        {
            return LimitRefused(CreateOutcomeProblem(CloudAgentCreateOutcome.AgentLimitReached, limits));
        }

        if (usage.Active >= limits.MaxActive)
        {
            return LimitRefused(CreateOutcomeProblem(CloudAgentCreateOutcome.ActiveLimitReached, limits));
        }

        var now = _time.GetUtcNow();
        var agentId = Guid.NewGuid().ToString("N");
        var sessionId = CloudSessionIds.New();
        await SaveSessionAsync(sessionId, ownerId, now, cancellationToken).ConfigureAwait(false);
        var record = BuildRecord(agentId, sessionId, ownerId, normalized, idempotencyKey, fingerprint, now);
        var created = await _store.CreateAsync(record, limits, cancellationToken).ConfigureAwait(false);
        switch (created.Outcome)
        {
            case CloudAgentCreateOutcome.Created:
                _metrics.AgentCreated();
                _background.Start("provision", token => _lifecycle.BringUpAsync(agentId, CloudAgentStatus.Provisioning, token));
                return CloudOutcome<CloudCreateResult>.Ok(new CloudCreateResult(CloudAgentMapper.ToResponse(created.Record!, now), Replayed: false));
            case CloudAgentCreateOutcome.Replayed:
                return ReplayOrConflict(created.Record!, fingerprint);
            case CloudAgentCreateOutcome.IdempotencyKeyReused:
                return CloudOutcome<CloudCreateResult>.Fail(KeyReused());
            default:
                return LimitRefused(CreateOutcomeProblem(created.Outcome, limits));
        }
    }

    /// <summary>Reads one agent the caller owns.</summary>
    /// <param name="ownerId">The caller.</param>
    /// <param name="id">The agent identifier.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The agent, or a 404 for an absent agent and for another owner's agent alike.</returns>
    public async Task<CloudOutcome<CloudAgentResponse>> GetAsync(string ownerId, string id, CancellationToken cancellationToken)
    {
        var agent = await FindOwnedAsync(ownerId, id, cancellationToken).ConfigureAwait(false);
        return agent is null
            ? CloudOutcome<CloudAgentResponse>.Fail(CloudProblem.AgentNotFound(id))
            : CloudOutcome<CloudAgentResponse>.Ok(CloudAgentMapper.ToResponse(agent, _time.GetUtcNow()));
    }

    /// <summary>Lists the caller's agents, newest first.</summary>
    /// <param name="ownerId">The caller.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The agents.</returns>
    public async Task<CloudAgentListResponse> ListAsync(string ownerId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        var now = _time.GetUtcNow();
        var records = await _store.ListByOwnerAsync(ownerId, cancellationToken).ConfigureAwait(false);
        return new CloudAgentListResponse { Agents = [.. records.Select(record => CloudAgentMapper.ToResponse(record, now))] };
    }

    /// <summary>Finds the session of an agent the caller owns, for the event stream.</summary>
    /// <param name="ownerId">The caller.</param>
    /// <param name="id">The agent identifier.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The session identifier, or null when the agent is absent or another owner's.</returns>
    public async Task<string?> FindSessionIdAsync(string ownerId, string id, CancellationToken cancellationToken) =>
        (await FindOwnedAsync(ownerId, id, cancellationToken).ConfigureAwait(false))?.SessionId;

    /// <summary>Admits a prompt and starts its run on the agent's worker.</summary>
    /// <param name="ownerId">The caller.</param>
    /// <param name="id">The agent identifier.</param>
    /// <param name="prompt">The validated prompt text.</param>
    /// <param name="correlationId">The end-to-end correlation identifier.</param>
    /// <param name="parent">The accepting request's trace context, or null.</param>
    /// <param name="cancellationToken">Cancels the admission.</param>
    /// <returns>The accepted response, or the refusal.</returns>
    public async Task<CloudOutcome<CloudPromptAccepted>> PromptAsync(string ownerId, string id, string prompt, string correlationId, ActivityContext? parent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(correlationId);
        var agent = await FindOwnedAsync(ownerId, id, cancellationToken).ConfigureAwait(false);
        if (agent is null)
        {
            return CloudOutcome<CloudPromptAccepted>.Fail(CloudProblem.AgentNotFound(id));
        }

        var settings = _options.CurrentValue;
        var gate = Gate(agent, settings);
        if (gate is not null)
        {
            return CloudOutcome<CloudPromptAccepted>.Fail(gate);
        }

        if (agent.Status == CloudAgentStatus.Idle)
        {
            var activation = await _store.TryActivateAsync(agent.Id, settings.Limits.MaxConcurrentRunningPerOwner, _time.GetUtcNow(), cancellationToken).ConfigureAwait(false);
            if (activation.Outcome == CloudAgentActivationOutcome.LimitReached)
            {
                _metrics.LimitRefused("running");
                return CloudOutcome<CloudPromptAccepted>.Fail(RunningLimit(settings.Limits.MaxConcurrentRunningPerOwner));
            }

            if (activation.Outcome is CloudAgentActivationOutcome.NotFound or CloudAgentActivationOutcome.NotWakeable)
            {
                return CloudOutcome<CloudPromptAccepted>.Fail(CloudProblem.Conflict("agent_not_wakeable", "Cloud agent cannot be woken.", "The agent changed state while the prompt was admitted. Read it and try again."));
            }

            agent = activation.Record ?? agent;
        }

        return await AdmitAsync(agent, prompt, correlationId, parent, cancellationToken).ConfigureAwait(false);
    }

    private async Task<CloudOutcome<CloudPromptAccepted>> AdmitAsync(CloudAgentRecord agent, string prompt, string correlationId, ActivityContext? parent, CancellationToken cancellationToken)
    {
        var redactor = await _redactors.CreateAsync(agent, cancellationToken).ConfigureAwait(false);
        var text = redactor.Redact(prompt) ?? prompt;
        var runId = Guid.NewGuid().ToString("N");
        var lease = await _runner.TryAcquireLeaseAsync(agent.SessionId, runId, cancellationToken).ConfigureAwait(false);
        if (lease is null)
        {
            return CloudOutcome<CloudPromptAccepted>.Fail(CloudProblem.Conflict("prompt_in_flight", "Prompt already in flight.", "The agent is running another prompt."));
        }

        try
        {
            await _sessions.SubmitPromptAsync(agent.SessionId, text, CancellationToken.None).ConfigureAwait(false);
            await _publisher.NotifyAsync(agent.SessionId).ConfigureAwait(false);
        }
        catch (SessionException ex) when (ex.Error.Code is ErrorCode.PromptInFlight or ErrorCode.SessionNotFound)
        {
            await lease.DisposeAsync().ConfigureAwait(false);
            return CloudOutcome<CloudPromptAccepted>.Fail(CloudProblem.Conflict("prompt_in_flight", "Prompt already in flight.", "The agent is running another prompt."));
        }
        catch (Exception)
        {
            await lease.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        var stop = _runs.Register(agent.SessionId);
        _runner.Start(agent.SessionId, runId, text, correlationId, lease, parent, stop);
        return CloudOutcome<CloudPromptAccepted>.Ok(new CloudPromptAccepted { AgentId = agent.Id, SessionId = agent.SessionId, Status = agent.Status });
    }

    /// <summary>Cancels the run in flight on this instance.</summary>
    /// <param name="ownerId">The caller.</param>
    /// <param name="id">The agent identifier.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The agent, or the refusal (404, or 409 when no run is in flight here).</returns>
    public async Task<CloudOutcome<CloudAgentResponse>> CancelAsync(string ownerId, string id, CancellationToken cancellationToken)
    {
        var agent = await FindOwnedAsync(ownerId, id, cancellationToken).ConfigureAwait(false);
        if (agent is null)
        {
            return CloudOutcome<CloudAgentResponse>.Fail(CloudProblem.AgentNotFound(id));
        }

        return _runs.TryCancel(agent.SessionId)
            ? CloudOutcome<CloudAgentResponse>.Ok(CloudAgentMapper.ToResponse(agent, _time.GetUtcNow()))
            : CloudOutcome<CloudAgentResponse>.Fail(CloudProblem.Conflict("no_run_in_flight", "No run to cancel.", "No run of this agent is executing on this server instance."));
    }

    /// <summary>Stops an agent: cancels its run, idles its worker and records Stopped.</summary>
    /// <param name="ownerId">The caller.</param>
    /// <param name="id">The agent identifier.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The stopped agent, or the refusal.</returns>
    public async Task<CloudOutcome<CloudAgentResponse>> StopAsync(string ownerId, string id, CancellationToken cancellationToken)
    {
        var agent = await FindOwnedAsync(ownerId, id, cancellationToken).ConfigureAwait(false);
        if (agent is null)
        {
            return CloudOutcome<CloudAgentResponse>.Fail(CloudProblem.AgentNotFound(id));
        }

        if (agent.Status is CloudAgentStatus.Stopped)
        {
            return CloudOutcome<CloudAgentResponse>.Ok(CloudAgentMapper.ToResponse(agent, _time.GetUtcNow()));
        }

        if (agent.Status is not (CloudAgentStatus.Starting or CloudAgentStatus.Running or CloudAgentStatus.Idle))
        {
            return CloudOutcome<CloudAgentResponse>.Fail(CloudProblem.Conflict("agent_not_stoppable", "Cloud agent cannot be stopped.", $"The agent is {Word(agent.Status)}."));
        }

        _ = _runs.TryCancel(agent.SessionId);
        var stopped = await _lifecycle.StopAsync(agent.Id, cancellationToken).ConfigureAwait(false);
        return stopped is null
            ? CloudOutcome<CloudAgentResponse>.Fail(CloudProblem.AgentNotFound(id))
            : CloudOutcome<CloudAgentResponse>.Ok(CloudAgentMapper.ToResponse(stopped, _time.GetUtcNow()));
    }

    /// <summary>Wakes an idle, stopped, failed or orphaned agent: moves it to Starting and brings its worker up in the background.</summary>
    /// <param name="ownerId">The caller.</param>
    /// <param name="id">The agent identifier.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The agent (Starting, or as it was when already active), or the refusal.</returns>
    public async Task<CloudOutcome<CloudAgentResponse>> WakeAsync(string ownerId, string id, CancellationToken cancellationToken)
    {
        var agent = await FindOwnedAsync(ownerId, id, cancellationToken).ConfigureAwait(false);
        if (agent is null)
        {
            return CloudOutcome<CloudAgentResponse>.Fail(CloudProblem.AgentNotFound(id));
        }

        var settings = _options.CurrentValue;
        if (settings.Limits.MaxComputeSecondsPerAgent > 0 && agent.Status == CloudAgentStatus.LimitReached)
        {
            return CloudOutcome<CloudAgentResponse>.Fail(ComputeLimit());
        }

        var activation = await _store.TryActivateAsync(agent.Id, settings.Limits.MaxConcurrentRunningPerOwner, _time.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        switch (activation.Outcome)
        {
            case CloudAgentActivationOutcome.Activated:
                _background.Start("wake", token => _lifecycle.BringUpAsync(agent.Id, CloudAgentStatus.Starting, token));
                return CloudOutcome<CloudAgentResponse>.Ok(CloudAgentMapper.ToResponse(activation.Record!, _time.GetUtcNow()));
            case CloudAgentActivationOutcome.AlreadyActive:
                return CloudOutcome<CloudAgentResponse>.Ok(CloudAgentMapper.ToResponse(activation.Record!, _time.GetUtcNow()));
            case CloudAgentActivationOutcome.LimitReached:
                _metrics.LimitRefused("running");
                return CloudOutcome<CloudAgentResponse>.Fail(RunningLimit(settings.Limits.MaxConcurrentRunningPerOwner));
            case CloudAgentActivationOutcome.NotFound:
                return CloudOutcome<CloudAgentResponse>.Fail(CloudProblem.AgentNotFound(id));
            default:
                return CloudOutcome<CloudAgentResponse>.Fail(CloudProblem.Conflict("agent_not_wakeable", "Cloud agent cannot be woken.", $"The agent is {Word(activation.Record?.Status ?? agent.Status)}."));
        }
    }

    /// <summary>Deletes an agent: cancels its run, removes its worker and volume (only when the label matches), and removes the record. The session log stays: the session store has no delete.</summary>
    /// <param name="ownerId">The caller.</param>
    /// <param name="id">The agent identifier.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>An empty success, or the refusal.</returns>
    public async Task<CloudOutcome<bool>> DeleteAsync(string ownerId, string id, CancellationToken cancellationToken)
    {
        var agent = await FindOwnedAsync(ownerId, id, cancellationToken).ConfigureAwait(false);
        if (agent is null)
        {
            return CloudOutcome<bool>.Fail(CloudProblem.AgentNotFound(id));
        }

        if (agent.Status == CloudAgentStatus.Provisioning)
        {
            return CloudOutcome<bool>.Fail(CloudProblem.Conflict("agent_provisioning", "Cloud agent is being provisioned.", "Wait until the agent has finished provisioning or failed, then delete it."));
        }

        _ = _runs.TryCancel(agent.SessionId);
        var deleting = await _lifecycle.BeginDeleteAsync(agent.Id, cancellationToken).ConfigureAwait(false) ?? agent;
        try
        {
            // An orphaned agent's record is deleted even when its worker cannot be matched: the worker is not this agent's, so it is left alone.
            if (!await _lifecycle.RemoveWorkerAsync(deleting, cancellationToken).ConfigureAwait(false) && agent.Status != CloudAgentStatus.Orphaned)
            {
                await _lifecycle.MarkOrphanedAsync(agent.Id, new CloudAgentError { Code = "worker_mismatch", Message = "The worker at the provider is not the one recorded for this agent, so it was left alone." }, cancellationToken).ConfigureAwait(false);
                return CloudOutcome<bool>.Fail(CloudProblem.Conflict("worker_mismatch", "The worker was not removed.", "The worker at the provider could not be matched to this agent by its label, so nothing was deleted."));
            }
        }
        catch (CloudEnvironmentException)
        {
            return CloudOutcome<bool>.Fail(new CloudProblem(502, "worker_removal_failed", "The worker could not be removed.", "The provider refused or could not be reached. The agent stays in deleting and the sweeper retries."));
        }

        _ = await _store.DeleteAsync(agent.Id, cancellationToken).ConfigureAwait(false);
        return CloudOutcome<bool>.Ok(true);
    }

    /// <summary>Opens a pull request for the agent's branch.</summary>
    /// <param name="ownerId">The caller.</param>
    /// <param name="id">The agent identifier.</param>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The pull request, or the refusal.</returns>
    public async Task<CloudOutcome<CloudPullRequest>> CreatePullRequestAsync(string ownerId, string id, CreateCloudPullRequestRequest request, CancellationToken cancellationToken)
    {
        var agent = await FindOwnedAsync(ownerId, id, cancellationToken).ConfigureAwait(false);
        return agent is null
            ? CloudOutcome<CloudPullRequest>.Fail(CloudProblem.AgentNotFound(id))
            : await _pullRequests.CreateAsync(agent, request, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reports the caller's caps and use.</summary>
    /// <param name="ownerId">The caller.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The limits.</returns>
    public async Task<CloudLimitsResponse> LimitsAsync(string ownerId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        var limits = _options.CurrentValue.Limits;
        var usage = await _store.GetUsageAsync(ownerId, cancellationToken).ConfigureAwait(false);
        var open = (await _store.ListByOwnerAsync(ownerId, cancellationToken).ConfigureAwait(false))
            .Where(static record => record.ActiveSince is not null)
            .Sum(record => Math.Max(0, (_time.GetUtcNow() - record.ActiveSince!.Value).TotalSeconds));
        return new CloudLimitsResponse
        {
            MaxAgents = limits.MaxAgentsPerOwner,
            MaxConcurrentRunning = limits.MaxConcurrentRunningPerOwner,
            Agents = usage.Agents,
            Running = usage.Active,
            DefaultIdleMinutes = limits.DefaultIdleMinutes,
            MaxComputeSecondsPerAgent = limits.MaxComputeSecondsPerAgent,
            ComputeSeconds = usage.ComputeSeconds + open,
        };
    }

    private async Task SaveSessionAsync(string sessionId, string ownerId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await _repository.SaveAsync(
            new Session
            {
                Id = sessionId,
                OwnerId = ownerId,
                CreatedAt = now,
                UpdatedAt = now,
                Status = SessionStatus.Idle,
                Messages = [],
                Events = [],
            },
            cancellationToken).ConfigureAwait(false);
        _registry.Add(sessionId);
    }

    private static CloudAgentRecord BuildRecord(string agentId, string sessionId, string ownerId, CreateCloudAgentRequest normalized, string? idempotencyKey, string fingerprint, DateTimeOffset now) =>
        new()
        {
            Id = agentId,
            OwnerId = ownerId,
            SessionId = sessionId,
            Name = normalized.Name!,
            Status = CloudAgentStatus.Provisioning,
            CreatedAt = now,
            UpdatedAt = now,
            Repositories = normalized.Repositories!,
            Branch = GitBranchNames.ForAgent(agentId),
            IdleMinutes = normalized.IdleMinutes!.Value,
            LastActivityAt = now,
            EnvironmentId = normalized.EnvironmentId,
            SecretRefs = normalized.SecretRefs ?? [],
            Model = string.IsNullOrEmpty(normalized.Model) ? null : normalized.Model,
            Mode = string.IsNullOrEmpty(normalized.Mode) ? null : normalized.Mode,
            IdempotencyKey = idempotencyKey,
            RequestFingerprint = fingerprint,
        };

    private async Task<CloudAgentRecord?> FindOwnedAsync(string ownerId, string id, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        ArgumentNullException.ThrowIfNull(id);
        var agent = await _store.GetAsync(id, cancellationToken).ConfigureAwait(false);
        return agent is not null && string.Equals(agent.OwnerId, ownerId, StringComparison.Ordinal) ? agent : null;
    }

    private CloudProblem? Gate(CloudAgentRecord agent, CloudOptions settings)
    {
        if (settings.Limits.MaxComputeSecondsPerAgent > 0)
        {
            var seconds = agent.ComputeSeconds + (agent.ActiveSince is { } since ? Math.Max(0, (_time.GetUtcNow() - since).TotalSeconds) : 0);
            if (agent.Status == CloudAgentStatus.LimitReached || seconds >= settings.Limits.MaxComputeSecondsPerAgent)
            {
                _metrics.LimitRefused("compute");
                return ComputeLimit();
            }
        }

        return agent.Status switch
        {
            CloudAgentStatus.Running or CloudAgentStatus.Idle => null,
            CloudAgentStatus.Starting when agent.WorkerRef is not null => null,
            CloudAgentStatus.Provisioning or CloudAgentStatus.Starting =>
                CloudProblem.Conflict("agent_starting", "Cloud agent is starting.", "The agent is still being provisioned. Poll it until it is running."),
            CloudAgentStatus.Stopped =>
                CloudProblem.Conflict("agent_stopped", "Cloud agent is stopped.", "Wake the agent before sending a prompt."),
            CloudAgentStatus.Failed =>
                CloudProblem.Conflict("agent_failed", "Cloud agent failed.", "Wake the agent to retry; its error says why it failed."),
            CloudAgentStatus.Orphaned =>
                CloudProblem.Conflict("agent_orphaned", "Cloud agent has no worker.", "The agent's worker is gone. Wake the agent to provision a new one."),
            _ => CloudProblem.Conflict("agent_not_ready", "Cloud agent is not ready.", $"The agent is {Word(agent.Status)}."),
        };
    }

    private CloudOutcome<CloudCreateResult> ReplayOrConflict(CloudAgentRecord earlier, string fingerprint) =>
        string.Equals(earlier.RequestFingerprint, fingerprint, StringComparison.Ordinal)
            ? CloudOutcome<CloudCreateResult>.Ok(new CloudCreateResult(CloudAgentMapper.ToResponse(earlier, _time.GetUtcNow()), Replayed: true))
            : CloudOutcome<CloudCreateResult>.Fail(KeyReused());

    private CloudOutcome<CloudCreateResult> LimitRefused(CloudProblem problem)
    {
        _metrics.LimitRefused(string.Equals(problem.Code, "agent_limit_reached", StringComparison.Ordinal) ? "agents" : "running");
        return CloudOutcome<CloudCreateResult>.Fail(problem);
    }

    private static CloudProblem CreateOutcomeProblem(CloudAgentCreateOutcome outcome, CloudAgentLimits limits) =>
        outcome == CloudAgentCreateOutcome.AgentLimitReached
            ? new CloudProblem(429, "agent_limit_reached", "Too many cloud agents.", $"This caller may own at most {limits.MaxAgents.ToString(System.Globalization.CultureInfo.InvariantCulture)} cloud agents. Delete one first.")
            : RunningLimit(limits.MaxActive);

    private static CloudProblem RunningLimit(int max) =>
        new(429, "running_limit_reached", "Too many running cloud agents.", $"This caller may have at most {max.ToString(System.Globalization.CultureInfo.InvariantCulture)} cloud agents running at once. Stop or wait for one to go idle.");

    private static CloudProblem ComputeLimit() =>
        new(429, "compute_limit_reached", "Compute budget used.", "This agent used its compute-second budget and was stopped.");

    private static CloudProblem KeyReused() =>
        CloudProblem.Conflict("idempotency_key_reused", "Idempotency key reused.", "This Idempotency-Key was already used for a different request.");

    private static string Word(CloudAgentStatus status) => CloudAgentStatusWord.Of(status);
}
