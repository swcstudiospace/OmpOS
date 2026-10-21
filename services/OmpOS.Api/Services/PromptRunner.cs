using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Options;
using OmpOS.Api.Harness;
using OmpOS.Api.Options;
using OmpOS.Api.Sse;
using OmpOS.Api.Telemetry;
using OmpOS.Contracts.Sessions;
using OmpOS.Contracts.Streaming;
using OmpOS.Contracts.Workflows;
using OmpOS.Harness.Abstractions;
using OmpOS.Sessions;
using OmpOS.Workflows;

namespace OmpOS.Api.Services;

/// <summary>
/// Executes accepted prompts in the background: streams the harness, appends each event to the
/// session log, wakes SSE tails, then settles the session. Failures append an error event and
/// settle as failed. When Workflows:Enabled is false (the default) the run goes through
/// DirectSessionRunner; when true the legacy in-process harness path runs instead. Every background
/// run is observed through a tracked continuation, so no task result is ever dropped and no failure escapes unlogged.
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

    private readonly ConcurrentDictionary<string, Task> _pending = new(StringComparer.Ordinal);
    private readonly SessionService _sessions;
    private readonly ISessionRepository _repository;
    private readonly IHarnessSessionFactory _factory;
    private readonly SessionEventStreamer _streamer;
    private readonly IOptions<DebugOptions> _debug;
    private readonly TimeProvider _time;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly IOptions<WorkflowsOptions> _workflows;
    private readonly IServiceProvider _services;
    private readonly ILogger<PromptRunner> _logger;

    /// <summary>Initializes a new instance of the <see cref="PromptRunner"/> class.</summary>
    /// <param name="sessions">The session service.</param>
    /// <param name="repository">The session store.</param>
    /// <param name="factory">The harness session factory.</param>
    /// <param name="streamer">The SSE broadcaster.</param>
    /// <param name="debug">The debug options (prompt redaction switch).</param>
    /// <param name="timeProvider">The clock.</param>
    /// <param name="lifetime">The host lifetime (shutdown signal).</param>
    /// <param name="workflows">The workflows options (direct runner when disabled).</param>
    /// <param name="services">The service provider (resolves the workflow lazily).</param>
    /// <param name="logger">The logger.</param>
    public PromptRunner(
        SessionService sessions,
        ISessionRepository repository,
        IHarnessSessionFactory factory,
        SessionEventStreamer streamer,
        IOptions<DebugOptions> debug,
        TimeProvider timeProvider,
        IOptions<WorkflowsOptions> workflows,
        IServiceProvider services,
        IHostApplicationLifetime lifetime,
        ILogger<PromptRunner> logger)
    {
        _sessions = sessions;
        _repository = repository;
        _factory = factory;
        _streamer = streamer;
        _debug = debug;
        _time = timeProvider;
        _lifetime = lifetime;
        _workflows = workflows;
        _services = services;
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
        var run = _workflows.Value.Enabled
            ? RunAsync(sessionId, prompt, correlationId, parent, _lifetime.ApplicationStopping)
            : RunWorkflowAsync(BuildInput(sessionId, prompt, correlationId), parent, _lifetime.ApplicationStopping);
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
    /// Executes the prompt through <see cref="ISessionRunWorkflow"/> (provision, run prompt, collect,
    /// teardown in a finally). The endpoint already admitted the prompt synchronously, so this runs in
    /// the background and the 202 response is unaffected; the activities settle the session exactly as
    /// the legacy path does, so the SSE event sequence is unchanged.
    /// </summary>
    /// <param name="input">The workflow input for this run.</param>
    /// <param name="parent">The accepting request's trace context, or null.</param>
    /// <param name="stoppingToken">The host shutdown signal.</param>
    private async Task RunWorkflowAsync(SessionRunInput input, ActivityContext? parent, CancellationToken stoppingToken)
    {
        using var activity = parent.HasValue
            ? OmpTelemetry.Activities.StartActivity("harness.run", ActivityKind.Internal, parent.Value)
            : OmpTelemetry.Activities.StartActivity("harness.run");
        if (_logger.IsEnabled(LogLevel.Information))
        {
            PromptStarted(_logger, input.SessionId, PromptRedactor.Format(input.Prompt, _debug.Value), null);
        }

        try
        {
            var workflow = _services.GetRequiredService<ISessionRunWorkflow>();
            var result = await workflow.RunAsync(input, new DirectWorkflowContext(), stoppingToken).ConfigureAwait(false);
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
                PromptRunFailed(_logger, input.SessionId, result.Status.ToString(), null);
                await SettleIfRunningAsync(input.SessionId, SessionStatus.Failed).ConfigureAwait(false);
            }
            else if (result.Status == WorkflowRunStatus.Cancelled)
            {
                await SettleIfRunningAsync(input.SessionId, SessionStatus.Cancelled).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            await SettleAsync(input.SessionId, SessionStatus.Cancelled).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Background execution must never fault the tracked task; the activities already settled above.
        catch (Exception ex)
        {
            PromptRunFailed(_logger, input.SessionId, ex.GetType().FullName ?? "Unknown", null);
            await SettleAsync(input.SessionId, SessionStatus.Failed).ConfigureAwait(false);
        }
#pragma warning restore CA1031
    }

    private async Task RunAsync(
        string sessionId,
        string prompt,
        string correlationId,
        ActivityContext? parent,
        CancellationToken stoppingToken)
    {
        using var activity = parent.HasValue
            ? OmpTelemetry.Activities.StartActivity("harness.run", ActivityKind.Internal, parent.Value)
            : OmpTelemetry.Activities.StartActivity("harness.run");
        if (_logger.IsEnabled(LogLevel.Information))
        {
            PromptStarted(_logger, sessionId, PromptRedactor.Format(prompt, _debug.Value), null);
        }

        try
        {
            var (stopReason, sawError) = await StreamTurnAsync(sessionId, prompt, correlationId, stoppingToken).ConfigureAwait(false);
            var final = HarnessTurnOutcome.SettledStatus(stopReason, sawError);
            await _sessions.RecordCompletionAsync(sessionId, final, CancellationToken.None, stopReason).ConfigureAwait(false);
            _streamer.Notify(sessionId);
            if (final == SessionStatus.Completed)
            {
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    PromptCompleted(_logger, sessionId, PromptRedactor.Format(prompt, _debug.Value), null);
                }
            }
            else
            {
                PromptRunFailed(_logger, sessionId, stopReason ?? "harness-error", null);
            }
        }
        catch (OperationCanceledException)
        {
            await SettleAsync(sessionId, SessionStatus.Cancelled).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Background execution must never fault the tracked task; failures are logged and settled above.
        catch (Exception ex)
        {
            PromptRunFailed(_logger, sessionId, ex.GetType().FullName ?? "Unknown", null);
            await SettleAsync(sessionId, SessionStatus.Failed).ConfigureAwait(false);
        }
#pragma warning restore CA1031
    }

    /// <summary>Streams one harness turn into the session log until the harness completes or its stream ends.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="prompt">The prompt text.</param>
    /// <param name="correlationId">The end-to-end correlation identifier.</param>
    /// <param name="cancellationToken">Cancels the stream.</param>
    /// <returns>The harness stop reason (null when the stream ended without a completion) and whether an error was streamed.</returns>
    private async Task<(string? StopReason, bool SawError)> StreamTurnAsync(
        string sessionId,
        string prompt,
        string correlationId,
        CancellationToken cancellationToken)
    {
        string? stopReason = null;
        var sawError = false;
        var harness = _factory.CreateSession(prompt);
        await using (harness.ConfigureAwait(false))
        {
            await harness.CreateAsync(cancellationToken).ConfigureAwait(false);
            await foreach (var evt in harness.SubmitPromptAsync(prompt, new PromptOptions { Steer = false }, cancellationToken).ConfigureAwait(false))
            {
                if (evt is CompletedEvent completed)
                {
                    // The completion settles the turn; RecordCompletionAsync writes the turn's terminal event.
                    stopReason = completed.Reason;
                    break;
                }

                sawError |= evt is ErrorEvent;
                await AppendAsync(sessionId, correlationId, HarnessEventMapper.ToPayload(evt), cancellationToken).ConfigureAwait(false);
            }
        }

        return (stopReason, sawError);
    }

    private async Task AppendAsync(string sessionId, string correlationId, StreamEventPayload payload, CancellationToken cancellationToken)
    {
        var evt = new StreamEvent
        {
            Seq = 0,
            SessionId = sessionId,
            CorrelationId = correlationId,
            Timestamp = _time.GetUtcNow(),
            Payload = payload,
        };
        await _repository.AppendAsync(sessionId, evt, cancellationToken).ConfigureAwait(false);
        _streamer.Notify(sessionId);
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

    /// <summary>Settles the session as failed or cancelled only while its prompt is still running, so a run that already settled is not settled twice.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="final">The terminal status to record.</param>
    private async Task SettleIfRunningAsync(string sessionId, SessionStatus final)
    {
        var session = await _repository.GetAsync(sessionId, CancellationToken.None).ConfigureAwait(false);
        if (session is { Status: SessionStatus.Running })
        {
            await SettleAsync(sessionId, final).ConfigureAwait(false);
        }
    }
}
