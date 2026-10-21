using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Options;
using OmpOS.Api.Options;
using OmpOS.Api.Sse;
using OmpOS.Api.Telemetry;
using OmpOS.Api.Workflows;
using OmpOS.Contracts.Sessions;
using OmpOS.Contracts.Workflows;
using OmpOS.Sessions;
using OmpOS.Workflows;

namespace OmpOS.Api.Services;

/// <summary>
/// Executes accepted prompts in the background through <see cref="ISessionRunDispatcher"/>, which selects the run path
/// from Workflows:Enabled. An in-process run reports its completion on the handle and is settled here from its result;
/// a routed run reports no completion and is settled by the process that executes it. Every background run is observed
/// through a tracked continuation, so no task result is ever dropped and no failure escapes unlogged.
/// </summary>
internal sealed class PromptRunner
{
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

    private readonly ConcurrentDictionary<string, Task> _pending = new(StringComparer.Ordinal);
    private readonly SessionService _sessions;
    private readonly ISessionRepository _repository;
    private readonly ISessionRunDispatcher _dispatcher;
    private readonly SessionEventStreamer _streamer;
    private readonly IOptions<DebugOptions> _debug;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<PromptRunner> _logger;

    /// <summary>Initializes a new instance of the <see cref="PromptRunner"/> class.</summary>
    /// <param name="sessions">The session service.</param>
    /// <param name="repository">The session store.</param>
    /// <param name="dispatcher">The dispatcher that starts each session run.</param>
    /// <param name="streamer">The SSE broadcaster.</param>
    /// <param name="debug">The debug options (prompt redaction switch).</param>
    /// <param name="lifetime">The host lifetime (shutdown signal).</param>
    /// <param name="logger">The logger.</param>
    public PromptRunner(
        SessionService sessions,
        ISessionRepository repository,
        ISessionRunDispatcher dispatcher,
        SessionEventStreamer streamer,
        IOptions<DebugOptions> debug,
        IHostApplicationLifetime lifetime,
        ILogger<PromptRunner> logger)
    {
        _sessions = sessions;
        _repository = repository;
        _dispatcher = dispatcher;
        _streamer = streamer;
        _debug = debug;
        _lifetime = lifetime;
        _logger = logger;
    }

    /// <summary>Starts prompt execution in the background and tracks it until it settles.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="prompt">The prompt text.</param>
    /// <param name="correlationId">The end-to-end correlation identifier.</param>
    /// <param name="parent">The accepting request's trace context, or null.</param>
    public void Start(string sessionId, string prompt, string correlationId, ActivityContext? parent)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(correlationId);
        var run = RunDispatchedAsync(BuildInput(sessionId, prompt, correlationId), parent, _lifetime.ApplicationStopping);
        _pending[sessionId] = run;
        _ = run.ContinueWith(
            static (completed, state) =>
            {
                var (pending, key, expected, logger) = (ValueTuple<ConcurrentDictionary<string, Task>, string, Task, ILogger<PromptRunner>>)state!;
                pending.TryRemove(new KeyValuePair<string, Task>(key, expected));
                if (completed.IsFaulted)
                {
                    BackgroundRunFaulted(logger, key, completed.Exception?.GetType().FullName ?? "Unknown", null);
                }
            },
            ValueTuple.Create(_pending, sessionId, run, _logger),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    /// <summary>Builds one workflow run input per prompt: one run means one provision and one teardown.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="prompt">The prompt text.</param>
    /// <param name="correlationId">The end-to-end correlation identifier.</param>
    /// <returns>The workflow input for the run.</returns>
    private static SessionRunInput BuildInput(string sessionId, string prompt, string correlationId)
    {
        var runId = Guid.NewGuid().ToString("N");
        return new SessionRunInput
        {
            RunId = runId,
            SessionId = sessionId,
            SandboxId = $"sb-{runId}",
            CorrelationId = correlationId,
            Prompt = prompt,
            ResumeFromSeq = 0,
            IdempotencyKey = $"prompt-{runId}",
        };
    }

    /// <summary>
    /// Starts the run through <see cref="ISessionRunDispatcher"/> and, when the dispatcher reports an in-process
    /// completion, settles the session from that result. The endpoint already admitted the prompt synchronously, so this
    /// runs in the background and the 202 response is unaffected.
    /// </summary>
    /// <param name="input">The run input for this prompt.</param>
    /// <param name="parent">The accepting request's trace context, or null.</param>
    /// <param name="stoppingToken">The host shutdown signal.</param>
    private async Task RunDispatchedAsync(SessionRunInput input, ActivityContext? parent, CancellationToken stoppingToken)
    {
        using var activity = parent.HasValue
            ? OmpTelemetry.Activities.StartActivity("harness.run", ActivityKind.Internal, parent.Value)
            : OmpTelemetry.Activities.StartActivity("harness.run");
        if (_logger.IsEnabled(LogLevel.Information))
        {
            PromptStarted(_logger, input.SessionId, PromptRedactor.Format(input.Prompt, _debug.Value), null);
        }

        // The turn identity of this run: the activities settle our turn before the runner tears the
        // sandbox down, so a later teardown failure must never settle a newer turn admitted meanwhile.
        var expectedTurn = await GetTurnAsync(input.SessionId).ConfigureAwait(false);

        try
        {
            var handle = await _dispatcher.StartAsync(input, stoppingToken).ConfigureAwait(false);
            if (handle.Completion is not null)
            {
                var result = await handle.Completion.ConfigureAwait(false);
                await SettleFromResultAsync(input, expectedTurn, result).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            await SettleIfRunningAsync(input.SessionId, expectedTurn, SessionStatus.Cancelled).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Background execution must never fault the tracked task; the activities already settled above.
        catch (Exception ex)
        {
            PromptRunFailed(_logger, input.SessionId, ex.GetType().FullName ?? "Unknown", null);
            await SettleIfRunningAsync(input.SessionId, expectedTurn, SessionStatus.Failed).ConfigureAwait(false);
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
            await SettleIfRunningAsync(input.SessionId, expectedTurn, SessionStatus.Failed).ConfigureAwait(false);
        }
        else if (result.Status == WorkflowRunStatus.Cancelled)
        {
            await SettleIfRunningAsync(input.SessionId, expectedTurn, SessionStatus.Cancelled).ConfigureAwait(false);
        }
    }

    private async Task SettleAsync(string sessionId, SessionStatus final)
    {
        try
        {
            await _sessions.RecordCompletionAsync(sessionId, final, CancellationToken.None).ConfigureAwait(false);
            _streamer.Notify(sessionId);
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
    private async Task SettleIfRunningAsync(string sessionId, int expectedTurn, SessionStatus final)
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

        await SettleAsync(sessionId, final).ConfigureAwait(false);
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
