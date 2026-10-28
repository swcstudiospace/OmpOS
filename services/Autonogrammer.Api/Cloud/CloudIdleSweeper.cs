using Microsoft.Extensions.Options;
using Autonogrammer.Api.Cloud.Options;
using Autonogrammer.Contracts.Cloud;
using Autonogrammer.Sandbox;
using Autonogrammer.Sessions;

namespace Autonogrammer.Api.Cloud;

/// <summary>
/// The background service that keeps cost and state in check. Each sweep puts running agents that were inactive for their idle window (default 15 minutes)
/// to idle, stops agents that used their compute budget, marks agents stuck in provisioning or starting as failed, finishes deletions that a provider
/// refused, and, at its own interval, scans for orphans. Time comes from the injected <see cref="TimeProvider"/>. The sweep touches an agent only through a
/// guarded store update, so several instances sweeping at once do not undo each other.
/// </summary>
internal sealed class CloudIdleSweeper : BackgroundService
{
    private readonly ICloudAgentStore _store;
    private readonly ISessionRepository _sessions;
    private readonly CloudAgentLifecycle _lifecycle;
    private readonly CloudOrphanScanner _orphans;
    private readonly CloudRunTracker _runs;
    private readonly IOptionsMonitor<CloudOptions> _options;
    private readonly TimeProvider _time;
    private readonly ILogger<CloudIdleSweeper> _logger;
    private DateTimeOffset _lastOrphanScan = DateTimeOffset.MinValue;

    /// <summary>Initializes a new instance of the <see cref="CloudIdleSweeper"/> class.</summary>
    /// <param name="store">The agent store.</param>
    /// <param name="sessions">The session store, to see whether a run is still going.</param>
    /// <param name="lifecycle">The worker lifecycle.</param>
    /// <param name="orphans">The orphan scanner.</param>
    /// <param name="runs">The tracker of runs on this instance.</param>
    /// <param name="options">The cloud options.</param>
    /// <param name="time">The clock.</param>
    /// <param name="logger">The logger.</param>
    public CloudIdleSweeper(
        ICloudAgentStore store,
        ISessionRepository sessions,
        CloudAgentLifecycle lifecycle,
        CloudOrphanScanner orphans,
        CloudRunTracker runs,
        IOptionsMonitor<CloudOptions> options,
        TimeProvider time,
        ILogger<CloudIdleSweeper> logger)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(lifecycle);
        ArgumentNullException.ThrowIfNull(orphans);
        ArgumentNullException.ThrowIfNull(runs);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(logger);
        _store = store;
        _sessions = sessions;
        _lifecycle = lifecycle;
        _orphans = orphans;
        _runs = runs;
        _options = options;
        _time = time;
        _logger = logger;
    }

    /// <summary>Runs one sweep now. The background loop calls it at <c>Cloud:Sweeper:Interval</c>; a test calls it with a controlled clock.</summary>
    /// <param name="cancellationToken">Cancels the sweep.</param>
    /// <returns>What the sweep did.</returns>
    public async Task<CloudSweepResult> SweepOnceAsync(CancellationToken cancellationToken)
    {
        var settings = _options.CurrentValue;
        var now = _time.GetUtcNow();
        var (idled, limitStopped) = await SweepRunningAsync(settings, now, cancellationToken).ConfigureAwait(false);

        var timedOut = 0;
        foreach (var agent in await _store.ListByStatusAsync([CloudAgentStatus.Provisioning, CloudAgentStatus.Starting], cancellationToken).ConfigureAwait(false))
        {
            if (now - agent.UpdatedAt >= settings.Sweeper.ProvisioningTimeout)
            {
                await _lifecycle.FailAsync(
                    agent.Id,
                    agent.Status,
                    new CloudAgentError { Code = "provisioning_timeout", Message = "The agent did not finish starting in time. Wake it to try again." }).ConfigureAwait(false);
                timedOut++;
            }
        }

        var deleted = 0;
        foreach (var agent in await _store.ListByStatusAsync([CloudAgentStatus.Deleting], cancellationToken).ConfigureAwait(false))
        {
            if (await FinishDeletionAsync(agent, cancellationToken).ConfigureAwait(false))
            {
                deleted++;
            }
        }

        CloudOrphanReport? orphans = null;
        if (now - _lastOrphanScan >= settings.Sweeper.OrphanScanInterval)
        {
            _lastOrphanScan = now;
            orphans = await _orphans.ScanAsync(settings.Sweeper.OrphanCleanup, cancellationToken).ConfigureAwait(false);
        }

        return new CloudSweepResult(idled, limitStopped, timedOut, deleted, orphans);
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.CurrentValue.Enabled)
        {
            return;
        }

        using var timer = new PeriodicTimer(_options.CurrentValue.Sweeper.Interval, _time);
        do
        {
            try
            {
                _ = await SweepOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Log.SweepFailed(_logger, ex.GetType().FullName ?? "Unknown");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    private async Task<(int Idled, int LimitStopped)> SweepRunningAsync(CloudOptions settings, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var idled = 0;
        var limitStopped = 0;
        foreach (var agent in await _store.ListByStatusAsync([CloudAgentStatus.Running], cancellationToken).ConfigureAwait(false))
        {
            if (settings.Limits.MaxComputeSecondsPerAgent > 0
                && agent.ComputeSeconds + (agent.ActiveSince is { } since ? Math.Max(0, (now - since).TotalSeconds) : 0) >= settings.Limits.MaxComputeSecondsPerAgent)
            {
                _ = _runs.TryCancel(agent.SessionId);
                if (await _lifecycle.GoIdleAsync(agent, "limit", CloudAgentStatus.LimitReached, cancellationToken).ConfigureAwait(false))
                {
                    limitStopped++;
                }

                continue;
            }

            if (now - agent.LastActivityAt < TimeSpan.FromMinutes(agent.IdleMinutes) || await IsRunningAsync(agent, cancellationToken).ConfigureAwait(false))
            {
                continue;
            }

            if (await _lifecycle.GoIdleAsync(agent, "sweep", CloudAgentStatus.Idle, cancellationToken).ConfigureAwait(false))
            {
                idled++;
            }
        }

        return (idled, limitStopped);
    }

    private async Task<bool> IsRunningAsync(CloudAgentRecord agent, CancellationToken cancellationToken)
    {
        // The session status is the durable truth about a run, on every instance: a prompt sets it Running when it is admitted.
        var session = await _sessions.GetAsync(agent.SessionId, cancellationToken).ConfigureAwait(false);
        return session?.Status is Autonogrammer.Contracts.Sessions.SessionStatus.Running or Autonogrammer.Contracts.Sessions.SessionStatus.AwaitingToolApproval;
    }

    private async Task<bool> FinishDeletionAsync(CloudAgentRecord agent, CancellationToken cancellationToken)
    {
        try
        {
            if (!await _lifecycle.RemoveWorkerAsync(agent, cancellationToken).ConfigureAwait(false))
            {
                return false;
            }

            return await _store.DeleteAsync(agent.Id, cancellationToken).ConfigureAwait(false);
        }
        catch (CloudEnvironmentException ex)
        {
            Log.DeletionRetry(_logger, agent.Id, ex.Failure.ToString());
            return false;
        }
    }

    private static class Log
    {
        private static readonly Action<ILogger, string, Exception?> SweepFailedMessage =
            LoggerMessage.Define<string>(LogLevel.Error, new EventId(74, "CloudSweepFailed"), "The cloud agent sweep failed: {ErrorType}.");

        private static readonly Action<ILogger, string, string, Exception?> DeletionRetryMessage =
            LoggerMessage.Define<string, string>(LogLevel.Warning, new EventId(75, "CloudDeletionRetry"), "Removing the worker of cloud agent {AgentId} failed ({Failure}); the sweep retries.");

        public static void SweepFailed(ILogger logger, string errorType) => SweepFailedMessage(logger, errorType, null);

        public static void DeletionRetry(ILogger logger, string agentId, string failure) => DeletionRetryMessage(logger, agentId, failure, null);
    }
}
