using System.Diagnostics;
using Autonogrammer.Coordination;

namespace Autonogrammer.Api.Workflows;

/// <summary>
/// Keeps a session's run lease alive while its run executes. It renews the lease once as soon as renewal starts, which refreshes the
/// time-to-live at dispatch, and then every <see cref="CoordinationDefaults.RunLeaseRenewInterval"/>. A renewal that reports the lease lost,
/// or renewals that keep failing for a full time-to-live, cancel the run.
/// </summary>
/// <remarks>
/// A renewal that is refused or fails after its stop token was cancelled is a stop, not a loss: the run is ending and the release is what
/// ended the renewal. The caller starts <see cref="RunUntilStoppedAsync"/>, keeps the task, and awaits it after stopping the loop.
/// This type neither owns nor disposes the lease or the run source.
/// </remarks>
internal sealed class LeaseRenewal
{
    private static readonly Action<ILogger, string, Exception?> RunLeaseLost =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(10, "RunLeaseLost"),
            "Run lease lost for session {SessionId}; cancelling the run.");

    private static readonly Action<ILogger, string, string, Exception?> RunLeaseRenewFailed =
        LoggerMessage.Define<string, string>(
            LogLevel.Warning,
            new EventId(11, "RunLeaseRenewFailed"),
            "Run lease renewal failed for session {SessionId}: {ErrorType}.");

    private readonly string _sessionId;
    private readonly ISessionRunLease _lease;
    private readonly CancellationTokenSource _run;
    private readonly ILogger _logger;

    /// <summary>Initializes a new instance of the <see cref="LeaseRenewal"/> class.</summary>
    /// <param name="sessionId">The session whose lease is renewed.</param>
    /// <param name="lease">The held run lease.</param>
    /// <param name="run">The run's cancellation source, cancelled when the lease is lost.</param>
    /// <param name="logger">The logger.</param>
    public LeaseRenewal(string sessionId, ISessionRunLease lease, CancellationTokenSource run, ILogger logger)
    {
        _sessionId = sessionId;
        _lease = lease;
        _run = run;
        _logger = logger;
    }

    /// <summary>Renews the lease at once and then on every tick until <paramref name="stop"/> is cancelled.</summary>
    /// <param name="stop">Stops renewal once the run has finished or released the lease.</param>
    /// <returns>True when the lease was lost while renewing (the run has been cancelled); false when renewal stopped.</returns>
    public async Task<bool> RunUntilStoppedAsync(CancellationToken stop)
    {
        using var timer = new PeriodicTimer(CoordinationDefaults.RunLeaseRenewInterval);
        var lastRenewed = Stopwatch.GetTimestamp();
        try
        {
            do
            {
                var attempt = await TryRenewAsync(stop).ConfigureAwait(false);
                if (attempt == RenewalAttempt.Stopped)
                {
                    return false;
                }

                if (attempt == RenewalAttempt.Renewed)
                {
                    lastRenewed = Stopwatch.GetTimestamp();
                }
                else if (attempt == RenewalAttempt.Lost || Stopwatch.GetElapsedTime(lastRenewed) >= CoordinationDefaults.RunLeaseTimeToLive)
                {
                    RunLeaseLost(_logger, _sessionId, null);
                    await _run.CancelAsync().ConfigureAwait(false);
                    return true;
                }
            }
            while (await timer.WaitForNextTickAsync(stop).ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested)
        {
            // The run has finished and renewal stops.
        }

        return false;
    }

    /// <summary>Makes one renewal attempt. Nothing it throws escapes: a failure is reported as <see cref="RenewalAttempt.Failed"/> or, once stopped, <see cref="RenewalAttempt.Stopped"/>.</summary>
    /// <param name="stop">Cancels the attempt.</param>
    /// <returns>The outcome of the attempt.</returns>
    private async Task<RenewalAttempt> TryRenewAsync(CancellationToken stop)
    {
        try
        {
            var renewed = await _lease.RenewAsync(CoordinationDefaults.RunLeaseTimeToLive, stop).ConfigureAwait(false);
            if (renewed)
            {
                return RenewalAttempt.Renewed;
            }

            return stop.IsCancellationRequested ? RenewalAttempt.Stopped : RenewalAttempt.Lost;
        }
#pragma warning disable CA1031 // A failed renewal is retried on the next tick; a failure after the stop was requested is the stop itself.
        catch (Exception ex)
        {
            if (stop.IsCancellationRequested)
            {
                return RenewalAttempt.Stopped;
            }

            RunLeaseRenewFailed(_logger, _sessionId, ex.GetType().FullName ?? "Unknown", null);
            return RenewalAttempt.Failed;
        }
#pragma warning restore CA1031
    }

    /// <summary>The outcome of one renewal attempt.</summary>
    private enum RenewalAttempt
    {
        /// <summary>The lease was renewed.</summary>
        Renewed,

        /// <summary>The store reported that the lease is no longer held by this owner while renewal was active.</summary>
        Lost,

        /// <summary>The attempt threw while renewal was active; the lease may still be held.</summary>
        Failed,

        /// <summary>The attempt was refused or failed after the stop was requested; it says nothing about the lease.</summary>
        Stopped,
    }
}
