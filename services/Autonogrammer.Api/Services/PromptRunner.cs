using System.Diagnostics;
using Microsoft.Extensions.Options;
using Autonogrammer.Api.Options;
using Autonogrammer.Api.Sse;
using Autonogrammer.Api.Telemetry;
using Autonogrammer.Api.Workflows;
using Autonogrammer.Contracts.Sessions;
using Autonogrammer.Contracts.Workflows;
using Autonogrammer.Coordination;
using Autonogrammer.Sessions;
using Autonogrammer.Workflows;

namespace Autonogrammer.Api.Services;

/// <summary>
/// Admits and executes prompts. A new turn acquires its session's run lease through <see cref="TryAcquireLeaseAsync"/> before
/// the prompt is admitted; the lease is then handed to the run, which holds it until the run ends. Every background run is
/// observed through a tracked continuation, so no task result is ever dropped and no failure escapes unlogged.
/// </summary>
internal sealed class PromptRunner
{
    // One identity per process: a lease owner is "{instance}:{runId}", so a lease left by a crashed process never matches a live run.
    private static readonly Guid InstanceId = Guid.NewGuid();

    private static readonly Action<ILogger, string, string, Exception?> PromptStarted =
        LoggerMessage.Define<string, string>(
            LogLevel.Information,
            new EventId(1, "PromptStarted"),
            "Prompt started for session {SessionId} with prompt {Prompt}");

    private static readonly Action<ILogger, string, string, Exception?> PromptCompleted =
        LoggerMessage.Define<string, string>(
            LogLevel.Information,
            new EventId(2, "PromptCompleted"),
            "Prompt completed for session {SessionId} with prompt {Prompt}");

    private static readonly Action<ILogger, string, string, Exception?> PromptRunFailed =
        LoggerMessage.Define<string, string>(
            LogLevel.Error,
            new EventId(3, "PromptRunFailed"),
            "Prompt run failed for session {SessionId}: {ErrorType}");

    private static readonly Action<ILogger, string, string, Exception?> PromptSettleSkipped =
        LoggerMessage.Define<string, string>(
            LogLevel.Warning,
            new EventId(4, "PromptSettleSkipped"),
            "Prompt settle skipped for session {SessionId}: {ErrorCode}");

    private static readonly Action<ILogger, string, string, Exception?> PromptSettleFailed =
        LoggerMessage.Define<string, string>(
            LogLevel.Error,
            new EventId(5, "PromptSettleFailed"),
            "Prompt settle failed for session {SessionId}: {ErrorType}");

    private static readonly Action<ILogger, string, string, Exception?> BackgroundRunFaulted =
        LoggerMessage.Define<string, string>(
            LogLevel.Error,
            new EventId(6, "BackgroundRunFaulted"),
            "Background prompt run faulted for session {SessionId}: {ErrorType}");

    private static readonly Action<ILogger, string, int, Exception?> StaleRunSettleSkipped =
        LoggerMessage.Define<string, int>(
            LogLevel.Warning,
            new EventId(7, "StaleRunSettleSkipped"),
            "Stale run settle skipped for session {SessionId}: expected turn {ExpectedTurn} already superseded");

    private readonly SessionService _sessions;
    private readonly ISessionRepository _repository;
    private readonly ISessionRunLeases _leases;
    private readonly DirectSessionRunDispatcher _dispatcher;
    private readonly SessionChangePublisher _publisher;
    private readonly IOptions<DebugOptions> _debug;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<PromptRunner> _logger;

    /// <summary>Initializes a new instance of the <see cref="PromptRunner"/> class.</summary>
    /// <param name="sessions">The session service.</param>
    /// <param name="repository">The session store.</param>
    /// <param name="leases">The run-lease store.</param>
    /// <param name="dispatcher">Runs each admitted turn under its lease.</param>
    /// <param name="publisher">Announces session changes to this process's streams and to the other replicas.</param>
    /// <param name="debug">The debug options (prompt redaction switch).</param>
    /// <param name="lifetime">The host lifetime (shutdown signal).</param>
    /// <param name="logger">The logger.</param>
    public PromptRunner(
        SessionService sessions,
        ISessionRepository repository,
        ISessionRunLeases leases,
        DirectSessionRunDispatcher dispatcher,
        SessionChangePublisher publisher,
        IOptions<DebugOptions> debug,
        IHostApplicationLifetime lifetime,
        ILogger<PromptRunner> logger)
    {
        _sessions = sessions;
        _repository = repository;
        _leases = leases;
        _dispatcher = dispatcher;
        _publisher = publisher;
        _debug = debug;
        _lifetime = lifetime;
        _logger = logger;
    }

    /// <summary>
    /// Acquires the session's run lease for a new run. Called before the prompt is admitted, so a turn held by another owner is refused
    /// before any acceptance. A refused acquisition is retried for up to <see cref="RunLeaseRetry.Deadline"/>, because the previous run
    /// releases its lease a few milliseconds after its terminal event reaches the client. A lease another owner holds for longer still
    /// returns null, and the caller answers 409.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="runId">The run identifier; the lease owner is derived from it.</param>
    /// <param name="cancellationToken">Cancels the acquisition, including any wait between attempts.</param>
    /// <returns>The held lease, or null when the deadline passed with another owner still holding the session's run lease.</returns>
    public Task<ISessionRunLease?> TryAcquireLeaseAsync(string sessionId, string runId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        ArgumentNullException.ThrowIfNull(runId);
        var owner = $"{InstanceId:N}:{runId}";
        return RunLeaseRetry.AcquireWithinDeadlineAsync(
            ct => _leases.TryAcquireAsync(sessionId, owner, CoordinationDefaults.RunLeaseTimeToLive, ct),
            cancellationToken);
    }

    /// <summary>Starts an admitted run in the background, under the lease the caller acquired, and tracks it until it settles.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="runId">The run identifier the lease was acquired for.</param>
    /// <param name="prompt">The prompt text.</param>
    /// <param name="correlationId">The end-to-end correlation identifier.</param>
    /// <param name="lease">The run lease; the run takes ownership of it.</param>
    /// <param name="parent">The accepting request's trace context, or null.</param>
    /// <param name="runCancellation">Stops this run when cancelled, in addition to host shutdown. A hosted run passes the token its cancel endpoint signals; a plain prompt passes none.</param>
    public void Start(string sessionId, string runId, string prompt, string correlationId, ISessionRunLease lease, ActivityContext? parent, CancellationToken runCancellation = default)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        ArgumentNullException.ThrowIfNull(runId);
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(correlationId);
        ArgumentNullException.ThrowIfNull(lease);
        var run = RunDispatchedAsync(BuildInput(sessionId, runId, prompt, correlationId), lease, parent, runCancellation);
        _ = run.ContinueWith(
            static (completed, state) =>
            {
                var (key, logger) = ((string Key, ILogger<PromptRunner> Logger))state!;
                if (completed.IsFaulted)
                {
                    BackgroundRunFaulted(logger, key, completed.Exception?.GetType().FullName ?? "Unknown", null);
                }
            },
            (sessionId, _logger),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    /// <summary>Builds the run input for one prompt: one run means one provision and one teardown.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="runId">The run identifier.</param>
    /// <param name="prompt">The prompt text.</param>
    /// <param name="correlationId">The end-to-end correlation identifier.</param>
    /// <returns>The run input for the run.</returns>
    private static SessionRunInput BuildInput(string sessionId, string runId, string prompt, string correlationId) =>
        new()
        {
            RunId = runId,
            SessionId = sessionId,
            SandboxId = $"sb-{runId}",
            CorrelationId = correlationId,
            Prompt = prompt,
            ResumeFromSeq = 0,
            IdempotencyKey = $"prompt-{runId}",
        };

    /// <summary>
    /// Runs the admitted turn under the lease handed over and, once the run completes, settles the session from its result. The
    /// endpoint already admitted the prompt synchronously, so this runs in the background and the 202 response is unaffected.
    /// </summary>
    /// <param name="input">The run input for this prompt.</param>
    /// <param name="lease">The run lease; the dispatcher releases it when the run ends.</param>
    /// <param name="parent">The accepting request's trace context, or null.</param>
    /// <param name="runCancellation">Stops this run when cancelled, in addition to the host shutdown signal.</param>
    private async Task RunDispatchedAsync(SessionRunInput input, ISessionRunLease lease, ActivityContext? parent, CancellationToken runCancellation)
    {
        using var stopping = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.ApplicationStopping, runCancellation);
        var stoppingToken = stopping.Token;
        using var activity = parent.HasValue
            ? ApiTelemetry.Activities.StartActivity("harness.run", ActivityKind.Internal, parent.Value)
            : ApiTelemetry.Activities.StartActivity("harness.run");
        if (_logger.IsEnabled(LogLevel.Information))
        {
            PromptStarted(_logger, input.SessionId, PromptRedactor.Format(input.Prompt, _debug.Value), null);
        }

        // The turn identity of this run: the activities settle our turn before the runner tears the
        // sandbox down, so a later teardown failure must never settle a newer turn admitted meanwhile.
        // The run has not started yet, so a failure here must release the lease it was handed.
        int expectedTurn;
        try
        {
            expectedTurn = await GetTurnAsync(input.SessionId).ConfigureAwait(false);
        }
        catch (Exception)
        {
            await SettleAsync(input.SessionId, SessionStatus.Failed, null).ConfigureAwait(false);
            await lease.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        try
        {
            var result = await _dispatcher.RunAsync(input, lease, stoppingToken).ConfigureAwait(false);
            await SettleFromResultAsync(input, expectedTurn, result).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await SettleIfRunningAsync(input.SessionId, expectedTurn, SessionStatus.Cancelled, null).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Background execution must never fault the tracked task; the activities already settled above.
        catch (Exception ex)
        {
            PromptRunFailed(_logger, input.SessionId, ex.GetType().FullName ?? "Unknown", null);
            await SettleIfRunningAsync(input.SessionId, expectedTurn, SessionStatus.Failed, null).ConfigureAwait(false);
        }
#pragma warning restore CA1031
    }

    /// <summary>Logs an in-process run's outcome and settles the session from the status the run reported.</summary>
    /// <param name="input">The run input for this prompt.</param>
    /// <param name="expectedTurn">The turn identity captured when this run started.</param>
    /// <param name="result">The run result.</param>
    private async Task SettleFromResultAsync(SessionRunInput input, int expectedTurn, SessionRunResult result)
    {
        if (result.Status == WorkflowRunStatus.Completed)
        {
            // The run completes even when the turn it carried failed, so log the status the session settled with.
            var settled = await _repository.GetAsync(input.SessionId, CancellationToken.None).ConfigureAwait(false);
            if (settled?.Status == SessionStatus.Completed)
            {
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    PromptCompleted(_logger, input.SessionId, PromptRedactor.Format(input.Prompt, _debug.Value), null);
                }
            }
            else
            {
                PromptRunFailed(_logger, input.SessionId, settled?.Status.ToString() ?? "Missing", null);
            }
        }
        else if (result.Status == WorkflowRunStatus.Failed)
        {
            // Log the cause (often a teardown failure for an already-settled turn) before the guarded
            // settle below, so a skipped stale settlement never swallows the cleanup failure.
            PromptRunFailed(_logger, input.SessionId, result.FailureDetail ?? result.Status.ToString(), null);
            await SettleIfRunningAsync(input.SessionId, expectedTurn, SessionStatus.Failed, PersistableFailureDetail(result.FailureDetail)).ConfigureAwait(false);
        }
        else if (result.Status == WorkflowRunStatus.Cancelled)
        {
            await SettleIfRunningAsync(input.SessionId, expectedTurn, SessionStatus.Cancelled, null).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Returns the failure detail a run may persist to the session log. Only the Autonogrammer-authored lease-lost text qualifies; runtime
    /// error messages stay out of the log, as they always have.
    /// </summary>
    /// <param name="detail">The run's failure detail.</param>
    /// <returns>The detail to persist, or null.</returns>
    private static string? PersistableFailureDetail(string? detail) =>
        string.Equals(detail, DirectSessionRunDispatcher.LeaseLostDetail, StringComparison.Ordinal) ? detail : null;

    /// <summary>Records a terminal status for the session and announces it.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="final">The terminal status.</param>
    /// <param name="failureDetail">The detail appended to a failed turn's terminal message, or null.</param>
    private async Task SettleAsync(string sessionId, SessionStatus final, string? failureDetail)
    {
        try
        {
            await _sessions.RecordCompletionAsync(sessionId, final, CancellationToken.None, failureDetail: failureDetail).ConfigureAwait(false);
            await _publisher.NotifyAsync(sessionId).ConfigureAwait(false);
        }
        catch (SessionException ex)
        {
            PromptSettleSkipped(_logger, sessionId, ex.Error.Code.ToString(), null);
        }
#pragma warning disable CA1031 // Settle is the last-resort boundary; its own failure must be logged, never thrown.
        catch (Exception ex)
        {
            PromptSettleFailed(_logger, sessionId, ex.GetType().FullName ?? "Unknown", null);
        }
#pragma warning restore CA1031
    }

    /// <summary>Settles the session as failed or cancelled only while its own turn is still running, so a stale run never settles a newer turn admitted after its own turn already settled.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="expectedTurn">The turn identity captured when this run started; settlement is skipped when the session moved on.</param>
    /// <param name="final">The terminal status to record.</param>
    /// <param name="failureDetail">The detail appended to a failed turn's terminal message, or null.</param>
    private async Task SettleIfRunningAsync(string sessionId, int expectedTurn, SessionStatus final, string? failureDetail)
    {
        var session = await _repository.GetAsync(sessionId, CancellationToken.None).ConfigureAwait(false);
        if (session is not { Status: SessionStatus.Running })
        {
            return;
        }

        if (TurnOf(session) != expectedTurn)
        {
            StaleRunSettleSkipped(_logger, sessionId, expectedTurn, null);
            return;
        }

        await SettleAsync(sessionId, final, failureDetail).ConfigureAwait(false);
    }

    /// <summary>Reads the turn identity of the run in flight: the number of user prompts admitted so far.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <returns>The turn identity, or -1 when the session is gone.</returns>
    private async Task<int> GetTurnAsync(string sessionId)
    {
        var session = await _repository.GetAsync(sessionId, CancellationToken.None).ConfigureAwait(false);
        return session is null ? -1 : TurnOf(session);
    }

    /// <summary>Counts admitted user prompts: steer and follow-up messages never change the turn.</summary>
    /// <param name="session">The session to inspect.</param>
    /// <returns>The number of user prompts admitted.</returns>
    private static int TurnOf(Session session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var turn = 0;
        foreach (var message in session.Messages)
        {
            // Mirrors SessionService.UserRole: the admission role marking a new turn.
            if (string.Equals(message.Role, "user", StringComparison.Ordinal))
            {
                turn++;
            }
        }

        return turn;
    }
}
