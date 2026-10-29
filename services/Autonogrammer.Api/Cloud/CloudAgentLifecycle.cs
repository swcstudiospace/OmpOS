using System.Diagnostics;
using Autonogrammer.Api.Cloud.Options;
using Autonogrammer.Contracts.Cloud;
using Autonogrammer.Sandbox;
using Microsoft.Extensions.Options;

namespace Autonogrammer.Api.Cloud;

/// <summary>
/// The state machine of a cloud agent's worker: bring it up (create or find the container and volume, wake it), put it to idle, stop it, mark it
/// failed or orphaned, and remove it. Every change to a record goes through <see cref="ICloudAgentStore.UpdateAsync"/> with a guard on the status it
/// expects, so a sweeper and a request on two instances cannot overwrite each other's decision. Awake time is accounted when a stretch ends.
/// </summary>
internal sealed class CloudAgentLifecycle
{
    private readonly ICloudAgentStore _store;
    private readonly ICloudEnvironmentClient _client;
    private readonly ICloudWorkerCatalog _catalog;
    private readonly IOptionsMonitor<CloudOptions> _options;
    private readonly TimeProvider _time;
    private readonly CloudMetrics _metrics;
    private readonly ILogger<CloudAgentLifecycle> _logger;

    /// <summary>Initializes a new instance of the <see cref="CloudAgentLifecycle"/> class.</summary>
    /// <param name="store">The agent store.</param>
    /// <param name="client">The environment client that creates, wakes and idles workers.</param>
    /// <param name="catalog">The worker catalog that removes workers.</param>
    /// <param name="options">The cloud options.</param>
    /// <param name="time">The clock.</param>
    /// <param name="metrics">The cloud metrics.</param>
    /// <param name="logger">The logger.</param>
    public CloudAgentLifecycle(
        ICloudAgentStore store,
        ICloudEnvironmentClient client,
        ICloudWorkerCatalog catalog,
        IOptionsMonitor<CloudOptions> options,
        TimeProvider time,
        CloudMetrics metrics,
        ILogger<CloudAgentLifecycle> logger)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(logger);
        _store = store;
        _client = client;
        _catalog = catalog;
        _options = options;
        _time = time;
        _metrics = metrics;
        _logger = logger;
    }

    /// <summary>
    /// Creates or finds the agent's container and volume, mounts the volume and wakes the worker, then marks the agent running. It handles an agent in
    /// <paramref name="expected"/> status only (Provisioning for a new agent, Starting for a wake); any other status means someone else moved it. A failure
    /// marks the agent failed with a code; it is not rethrown. Cancellation by host shutdown is rethrown and leaves the agent for the sweeper to repair.
    /// </summary>
    /// <param name="agentId">The agent identifier.</param>
    /// <param name="expected">The status the agent must be in.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>A task that completes when the agent is running or failed.</returns>
    public async Task BringUpAsync(string agentId, CloudAgentStatus expected, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        var agent = await _store.GetAsync(agentId, cancellationToken).ConfigureAwait(false);
        if (agent is null || agent.Status != expected)
        {
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var container = await _client.FindContainerAsync(agent.SessionId, cancellationToken).ConfigureAwait(false)
                ?? await _client.CreateContainerAsync(agent.SessionId, cancellationToken).ConfigureAwait(false);
            var volume = await _client.FindVolumeAsync(agent.SessionId, cancellationToken).ConfigureAwait(false)
                ?? await _client.CreateVolumeAsync(agent.SessionId, cancellationToken).ConfigureAwait(false);
            await _client.AttachVolumeAsync(container.ContainerId, volume.VolumeId, _options.CurrentValue.Environment.VolumeMountPath, cancellationToken).ConfigureAwait(false);
            _ = await _client.EnsureAwakeAsync(container.ContainerId, cancellationToken).ConfigureAwait(false);
            var now = _time.GetUtcNow();
            _ = await _store.UpdateAsync(
                agentId,
                current => current.Status == expected
                    ? current with { Status = CloudAgentStatus.Running, WorkerRef = container.ContainerId, VolumeRef = volume.VolumeId, ActiveSince = now, LastActivityAt = now, UpdatedAt = now, IdleAt = null, Error = null }
                    : current,
                cancellationToken).ConfigureAwait(false);
            _metrics.Provisioned(stopwatch.Elapsed.TotalMilliseconds);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            await FailAsync(agentId, expected, CloudFailureMap.From(ex)).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Makes sure the agent's worker exists and is awake and returns its container. A worker the provider no longer has, or one that is not the worker the record
    /// names, marks the agent orphaned; a wake that fails marks it failed. Either way the caller gets a <see cref="CloudProvisioningException"/>.
    /// </summary>
    /// <param name="agent">The agent.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The awake container.</returns>
    /// <exception cref="CloudProvisioningException">The worker is missing, mismatched or did not wake.</exception>
    public async Task<CloudContainer> EnsureWorkerAwakeAsync(CloudAgentRecord agent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(agent);
        CloudContainer? container;
        try
        {
            container = await _client.FindContainerAsync(agent.SessionId, cancellationToken).ConfigureAwait(false);
        }
        catch (CloudEnvironmentException ex)
        {
            var error = CloudFailureMap.From(ex);
            throw new CloudProvisioningException(error.Code, error.Message);
        }

        if (container is null || (agent.WorkerRef is not null && !string.Equals(container.ContainerId, agent.WorkerRef, StringComparison.Ordinal)))
        {
            var error = new CloudAgentError { Code = "worker_missing", Message = "The worker for this agent no longer exists or is not the worker recorded for it." };
            await MarkOrphanedAsync(agent.Id, error, cancellationToken).ConfigureAwait(false);
            throw new CloudProvisioningException(error.Code, error.Message);
        }

        try
        {
            var awake = await _client.EnsureAwakeAsync(container.ContainerId, cancellationToken).ConfigureAwait(false);
            await MarkAwakeAsync(agent.Id, awake.ContainerId, cancellationToken).ConfigureAwait(false);
            return awake;
        }
        catch (CloudEnvironmentException ex)
        {
            var error = CloudFailureMap.From(ex);
            await FailAsync(agent.Id, null, error).ConfigureAwait(false);
            throw new CloudProvisioningException(error.Code, error.Message);
        }
    }

    /// <summary>Moves a Starting or Running agent to Running with the current awake stretch counted from now (or kept, when one is open).</summary>
    /// <param name="agentId">The agent identifier.</param>
    /// <param name="containerId">The worker's provider identifier.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the record is updated.</returns>
    public async Task MarkAwakeAsync(string agentId, string containerId, CancellationToken cancellationToken)
    {
        var now = _time.GetUtcNow();
        _ = await _store.UpdateAsync(
            agentId,
            current => current.Status is CloudAgentStatus.Starting or CloudAgentStatus.Running
                ? current with { Status = CloudAgentStatus.Running, WorkerRef = containerId, ActiveSince = current.ActiveSince ?? now, LastActivityAt = now, UpdatedAt = now, IdleAt = null, Error = null }
                : current,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Records activity (a run started or finished), so the idle window restarts.</summary>
    /// <param name="agentId">The agent identifier.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the record is updated.</returns>
    public async Task TouchAsync(string agentId, CancellationToken cancellationToken)
    {
        var now = _time.GetUtcNow();
        _ = await _store.UpdateAsync(
            agentId,
            current => current.Status == CloudAgentStatus.Running ? current with { LastActivityAt = now, UpdatedAt = now } : current,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Puts a running agent to idle: asks the provider to let the worker sleep, then records Idle and settles the awake stretch. Nothing happens when the agent
    /// is not running, or when it had activity since the caller read it (a prompt arrived while the sweeper decided).
    /// </summary>
    /// <param name="agent">The agent as the caller read it.</param>
    /// <param name="cause">Why: <c>sweep</c>, <c>stop</c> or <c>limit</c>; a metric tag.</param>
    /// <param name="finalStatus">The status to record: Idle, or LimitReached when the compute budget is spent.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>True when the agent was moved.</returns>
    public async Task<bool> GoIdleAsync(CloudAgentRecord agent, string cause, CloudAgentStatus finalStatus, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(agent);
        if (agent.Status != CloudAgentStatus.Running)
        {
            return false;
        }

        if (agent.WorkerRef is { } containerId)
        {
            try
            {
                _ = await _client.RequestIdleAsync(containerId, cancellationToken).ConfigureAwait(false);
            }
            catch (CloudEnvironmentException ex)
            {
                Log.IdleRefused(_logger, agent.Id, ex.Failure.ToString());
                return false;
            }
        }

        return await SettleAsync(agent.Id, current => current.Status == CloudAgentStatus.Running && current.LastActivityAt == agent.LastActivityAt, finalStatus, cause, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Stops an agent that is starting, running or idle: moves it to Stopping, asks the provider to idle the worker, settles the awake stretch and records Stopped.
    /// A provider that refuses the idle request does not keep the agent from being stopped: the record is the owner's decision, and the worker sleeps by itself.
    /// </summary>
    /// <param name="agentId">The agent identifier.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The record after the stop, or null when there is no such agent.</returns>
    public async Task<CloudAgentRecord?> StopAsync(string agentId, CancellationToken cancellationToken)
    {
        var now = _time.GetUtcNow();
        var moved = false;
        var stopping = await _store.UpdateAsync(
            agentId,
            current =>
            {
                moved = current.Status is CloudAgentStatus.Starting or CloudAgentStatus.Running or CloudAgentStatus.Idle;
                return moved ? current with { Status = CloudAgentStatus.Stopping, UpdatedAt = now } : current;
            },
            cancellationToken).ConfigureAwait(false);
        if (stopping is null || !moved)
        {
            return stopping;
        }

        if (stopping.WorkerRef is { } containerId)
        {
            try
            {
                _ = await _client.RequestIdleAsync(containerId, cancellationToken).ConfigureAwait(false);
            }
            catch (CloudEnvironmentException ex)
            {
                Log.IdleRefused(_logger, agentId, ex.Failure.ToString());
            }
        }

        _ = await SettleAsync(agentId, current => current.Status == CloudAgentStatus.Stopping, CloudAgentStatus.Stopped, "stop", cancellationToken).ConfigureAwait(false);
        return await _store.GetAsync(agentId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Moves an agent to Deleting, settling any open awake stretch, so no run or wake starts on it. The caller removes the worker next. An agent that is
    /// provisioning is left alone (the caller refuses first): its worker is being created and would be left behind.
    /// </summary>
    /// <param name="agentId">The agent identifier.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The record, in Deleting unless it was provisioning; null when there is no such agent.</returns>
    public async Task<CloudAgentRecord?> BeginDeleteAsync(string agentId, CancellationToken cancellationToken)
    {
        var now = _time.GetUtcNow();
        var seconds = 0.0;
        var moved = false;
        var record = await _store.UpdateAsync(
            agentId,
            current =>
            {
                moved = current.Status is not (CloudAgentStatus.Provisioning or CloudAgentStatus.Deleting);
                if (!moved)
                {
                    return current;
                }

                seconds = OpenSeconds(current, now);
                return current with { Status = CloudAgentStatus.Deleting, UpdatedAt = now, ActiveSince = null, ComputeSeconds = current.ComputeSeconds + seconds, IdleAt = null };
            },
            cancellationToken).ConfigureAwait(false);
        if (moved)
        {
            _metrics.ComputeSeconds(seconds);
        }

        return record;
    }

    /// <summary>Marks an agent failed with an error, settling any open awake stretch. Nothing changes when the agent is no longer in <paramref name="expected"/> status.</summary>
    /// <param name="agentId">The agent identifier.</param>
    /// <param name="expected">The status the agent must be in, or null for any status except Deleting and Stopped.</param>
    /// <param name="error">The error to record.</param>
    /// <returns>A task that completes when the record is updated.</returns>
    public async Task FailAsync(string agentId, CloudAgentStatus? expected, CloudAgentError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        var now = _time.GetUtcNow();
        var seconds = 0.0;
        var failed = false;
        _ = await _store.UpdateAsync(
            agentId,
            current =>
            {
                failed = expected is { } status ? current.Status == status : current.Status is not (CloudAgentStatus.Deleting or CloudAgentStatus.Stopped or CloudAgentStatus.Stopping);
                if (!failed)
                {
                    return current;
                }

                seconds = OpenSeconds(current, now);
                return current with { Status = CloudAgentStatus.Failed, Error = error, UpdatedAt = now, ActiveSince = null, ComputeSeconds = current.ComputeSeconds + seconds, IdleAt = null };
            },
            CancellationToken.None).ConfigureAwait(false);
        if (failed)
        {
            _metrics.ComputeSeconds(seconds);
            _metrics.ProvisionFailed(error.Code);
        }
    }

    /// <summary>Marks an agent orphaned: its record names a worker the provider no longer has.</summary>
    /// <param name="agentId">The agent identifier.</param>
    /// <param name="error">The reason.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the record is updated.</returns>
    public async Task MarkOrphanedAsync(string agentId, CloudAgentError error, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(error);
        var now = _time.GetUtcNow();
        var seconds = 0.0;
        var moved = false;
        _ = await _store.UpdateAsync(
            agentId,
            current =>
            {
                moved = current.Status is CloudAgentStatus.Running or CloudAgentStatus.Idle or CloudAgentStatus.Starting or CloudAgentStatus.Failed or CloudAgentStatus.Stopped or CloudAgentStatus.Deleting;
                if (!moved)
                {
                    return current;
                }

                seconds = OpenSeconds(current, now);
                return current with { Status = CloudAgentStatus.Orphaned, Error = error, UpdatedAt = now, ActiveSince = null, ComputeSeconds = current.ComputeSeconds + seconds, IdleAt = null };
            },
            cancellationToken).ConfigureAwait(false);
        if (moved)
        {
            _metrics.ComputeSeconds(seconds);
        }
    }

    /// <summary>
    /// Removes the agent's worker and volume. The worker the provider holds for the agent's session must be the one the record names, and the label
    /// <see cref="CloudWorkerLabels.AgentKey"/> must name this agent: otherwise nothing is removed.
    /// </summary>
    /// <param name="agent">The agent being deleted.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>True when the worker is gone (or never existed); false when it was left alone because it could not be matched to the agent.</returns>
    public async Task<bool> RemoveWorkerAsync(CloudAgentRecord agent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(agent);
        var found = await _client.FindContainerAsync(agent.SessionId, cancellationToken).ConfigureAwait(false);
        if (found is null && agent.WorkerRef is null && agent.VolumeRef is null)
        {
            return true;
        }

        if (found is not null && agent.WorkerRef is not null && !string.Equals(found.ContainerId, agent.WorkerRef, StringComparison.Ordinal))
        {
            return false;
        }

        var containerId = agent.WorkerRef ?? found?.ContainerId;
        if (containerId is null)
        {
            return true;
        }

        var worker = new CloudWorkerInfo(
            containerId,
            agent.SessionId,
            agent.VolumeRef,
            new Dictionary<string, string>(StringComparer.Ordinal) { [CloudWorkerLabels.AgentKey] = agent.Id });
        if (!CloudWorkerGuard.BelongsTo(worker, agent.Id))
        {
            return false;
        }

        await _catalog.RemoveAsync(worker, cancellationToken).ConfigureAwait(false);
        return true;
    }

    private async Task<bool> SettleAsync(string agentId, Func<CloudAgentRecord, bool> guard, CloudAgentStatus finalStatus, string cause, CancellationToken cancellationToken)
    {
        var now = _time.GetUtcNow();
        var seconds = 0.0;
        var moved = false;
        _ = await _store.UpdateAsync(
            agentId,
            current =>
            {
                moved = guard(current);
                if (!moved)
                {
                    return current;
                }

                seconds = OpenSeconds(current, now);
                return current with
                {
                    Status = finalStatus,
                    UpdatedAt = now,
                    ActiveSince = null,
                    ComputeSeconds = current.ComputeSeconds + seconds,
                    IdleAt = finalStatus == CloudAgentStatus.Stopped ? null : now,
                };
            },
            cancellationToken).ConfigureAwait(false);
        if (moved)
        {
            _metrics.ComputeSeconds(seconds);
            _metrics.WentIdle(cause);
        }

        return moved;
    }

    private static double OpenSeconds(CloudAgentRecord record, DateTimeOffset now) =>
        record.ActiveSince is { } since ? Math.Max(0, (now - since).TotalSeconds) : 0;

    private static class Log
    {
        private static readonly Action<ILogger, string, string, Exception?> IdleRefusedMessage =
            LoggerMessage.Define<string, string>(LogLevel.Warning, new EventId(71, "CloudIdleRefused"), "The provider refused the idle request for cloud agent {AgentId}: {Failure}.");

        public static void IdleRefused(ILogger logger, string agentId, string failure) => IdleRefusedMessage(logger, agentId, failure, null);
    }
}
