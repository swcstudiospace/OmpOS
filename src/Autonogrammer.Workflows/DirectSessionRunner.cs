using Autonogrammer.Contracts.Workflows;

namespace Autonogrammer.Workflows;

/// <summary>
/// Direct in-process implementation of <see cref="ISessionRunWorkflow"/>.
/// </summary>
/// <remarks>
/// Executes the workflow activities sequentially (provision, run prompt, collect) with teardown in a <c>finally</c>
/// block once provisioning has succeeded. When the prompt started, <see cref="SessionRunInput.OnPromptSettled"/> runs in
/// the same <c>finally</c> block after the collect phase and before teardown, including after prompt failures. Each activity is bounded
/// by these attempt counts (ADR-0003, Q-WF-RETRY): provision 3×,
/// run prompt 1× (not idempotent), collect 3×, teardown 5×. Backoff waits go through
/// <see cref="IWorkflowContext.DelayAsync"/>, so a context whose delay completes immediately
/// runs the full policy with zero wall-clock delay. Deterministic primitives (clock, random,
/// delay, heartbeat) arrive per call through <see cref="IWorkflowContext"/>.
/// </remarks>
public sealed class DirectSessionRunner : ISessionRunWorkflow
{
    private static readonly RetryPolicy ProvisionRetry = new(MaximumAttempts: 3, InitialInterval: TimeSpan.FromSeconds(1), BackoffCoefficient: 2d);

    // Not idempotent: the prompt streams events into the session log, so a repeat duplicates work and events.
    private static readonly RetryPolicy RunPromptRetry = new(MaximumAttempts: 1, InitialInterval: TimeSpan.Zero, BackoffCoefficient: 1d);

    private static readonly RetryPolicy CollectRetry = new(MaximumAttempts: 3, InitialInterval: TimeSpan.FromSeconds(1), BackoffCoefficient: 2d);

    private static readonly RetryPolicy TeardownRetry = new(MaximumAttempts: 5, InitialInterval: TimeSpan.FromSeconds(1), BackoffCoefficient: 2d);

    private readonly ISessionRunActivities _activities;

    /// <summary>
    /// Initializes a new instance of the <see cref="DirectSessionRunner"/> class.
    /// </summary>
    /// <param name="activities">The session run activities to execute.</param>
    public DirectSessionRunner(ISessionRunActivities activities)
    {
        ArgumentNullException.ThrowIfNull(activities);
        _activities = activities;
    }

    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "The workflow contract maps any activity failure to a Failed result; teardown is still guaranteed by the finally block.")]
    public async Task<SessionRunResult> RunAsync(SessionRunInput input, IWorkflowContext ctx, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(ctx);

        var state = new WorkflowExecutionState();

        try
        {
            await ProvisionPhaseAsync(input, ctx, state, cancellationToken).ConfigureAwait(false);
            await PromptPhaseAsync(input, ctx, state, cancellationToken).ConfigureAwait(false);
            await CollectPhaseAsync(input, ctx, state, cancellationToken).ConfigureAwait(false);
            state.Status = WorkflowRunStatus.Completed;
        }
        catch (OperationCanceledException oce) when (cancellationToken.IsCancellationRequested)
        {
            state.Status = WorkflowRunStatus.Cancelled;
            state.FailureDetail = oce.Message;
        }
        catch (Exception ex)
        {
            state.Status = WorkflowRunStatus.Failed;
            state.FailureDetail = ex.Message;
        }
        finally
        {
            try
            {
                await NotifyPromptSettledAsync(input, state, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                if (state.Provisioned)
                {
                    await TryTeardownAsync(input, ctx, state).ConfigureAwait(false);
                }
            }
        }

        return new SessionRunResult
        {
            Status = state.Status,
            FinalSeq = state.FinalSeq,
            Artifacts = state.Artifacts,
            FailureDetail = state.FailureDetail,
        };
    }

    /// <summary>
    /// Provisions the sandbox under the provision retry policy and records it as provisioned.
    /// </summary>
    /// <param name="input">The workflow input.</param>
    /// <param name="ctx">The workflow context carrying the backoff delay.</param>
    /// <param name="state">The mutable run state.</param>
    /// <param name="cancellationToken">The run's cancellation token.</param>
    private async Task ProvisionPhaseAsync(SessionRunInput input, IWorkflowContext ctx, WorkflowExecutionState state, CancellationToken cancellationToken)
    {
        await RetryWithoutResultAsync(
            ProvisionRetry,
            ctx,
            token => _activities.ProvisionSandboxAsync(input, token),
            cancellationToken).ConfigureAwait(false);
        state.Provisioned = true;
    }

    /// <summary>
    /// Runs the prompt under the non-retried prompt policy and records the final sequence number.
    /// </summary>
    /// <param name="input">The workflow input.</param>
    /// <param name="ctx">The workflow context carrying heartbeats and the backoff delay.</param>
    /// <param name="state">The mutable run state.</param>
    /// <param name="cancellationToken">The run's cancellation token.</param>
    private async Task PromptPhaseAsync(SessionRunInput input, IWorkflowContext ctx, WorkflowExecutionState state, CancellationToken cancellationToken)
    {
        state.PromptStarted = true;
        state.FinalSeq = await RetryAsync(
            RunPromptRetry,
            ctx,
            token => _activities.RunPromptAsync(input, details => ctx.Heartbeat(details), token),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Collects the run's artifacts under the collect retry policy.
    /// </summary>
    /// <param name="input">The workflow input.</param>
    /// <param name="ctx">The workflow context carrying the backoff delay.</param>
    /// <param name="state">The mutable run state.</param>
    /// <param name="cancellationToken">The run's cancellation token.</param>
    private async Task CollectPhaseAsync(SessionRunInput input, IWorkflowContext ctx, WorkflowExecutionState state, CancellationToken cancellationToken)
    {
        state.Artifacts.AddRange(
            await RetryAsync(
                CollectRetry,
                ctx,
                token => _activities.CollectArtifactsAsync(input, token),
                cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Invokes <see cref="SessionRunInput.OnPromptSettled"/> once when the prompt was started, after the collect phase and
    /// before teardown. It is not invoked when the prompt did not start.
    /// </summary>
    /// <param name="input">The workflow input.</param>
    /// <param name="state">The mutable run state.</param>
    /// <param name="cancellationToken">The run's cancellation token.</param>
    private static async Task NotifyPromptSettledAsync(SessionRunInput input, WorkflowExecutionState state, CancellationToken cancellationToken)
    {
        if (state.PromptStarted && input.OnPromptSettled is { } onPromptSettled)
        {
            await onPromptSettled(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Runs an activity attempt under a retry policy. Cancellation is never retried; the final
    /// failure propagates unchanged once the policy's attempts are exhausted.
    /// </summary>
    /// <typeparam name="T">The activity result type.</typeparam>
    /// <param name="policy">The attempt bound and backoff.</param>
    /// <param name="ctx">The context whose <see cref="IWorkflowContext.DelayAsync"/> carries the backoff.</param>
    /// <param name="attempt">Invokes one attempt of the activity.</param>
    /// <param name="cancellationToken">The run's cancellation token.</param>
    /// <returns>The result of the first successful attempt.</returns>
    private static async Task<T> RetryAsync<T>(
        RetryPolicy policy,
        IWorkflowContext ctx,
        Func<CancellationToken, Task<T>> attempt,
        CancellationToken cancellationToken)
    {
        T? result = default;
        await RetryWithoutResultAsync(
            policy,
            ctx,
            async token =>
            {
                result = await attempt(token).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
        return result!;
    }

    /// <summary>
    /// Runs an activity attempt under a retry policy. Cancellation is never retried; the final
    /// failure propagates unchanged once the policy's attempts are exhausted.
    /// </summary>
    /// <param name="policy">The attempt bound and backoff.</param>
    /// <param name="ctx">The context whose <see cref="IWorkflowContext.DelayAsync"/> carries the backoff.</param>
    /// <param name="attempt">Invokes one attempt of the activity.</param>
    /// <param name="cancellationToken">The run's cancellation token.</param>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Any activity failure is retried until the policy's attempts are exhausted; the last failure is rethrown unchanged.")]
    private static async Task RetryWithoutResultAsync(
        RetryPolicy policy,
        IWorkflowContext ctx,
        Func<CancellationToken, Task> attempt,
        CancellationToken cancellationToken)
    {
        for (var attemptNumber = 1; ; attemptNumber++)
        {
            try
            {
                await attempt(cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (Exception ex) when (ShouldRetry(ex, policy, attemptNumber, cancellationToken))
            {
                await ctx.DelayAsync(policy.BackoffAfter(attemptNumber), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static bool ShouldRetry(Exception ex, RetryPolicy policy, int failedAttempt, CancellationToken cancellationToken)
    {
        if (failedAttempt >= policy.MaximumAttempts)
        {
            return false;
        }

        return ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested;
    }

    private async Task TryTeardownAsync(SessionRunInput input, IWorkflowContext ctx, WorkflowExecutionState state)
    {
        try
        {
            await RetryWithoutResultAsync(
                TeardownRetry,
                ctx,
                token => _activities.TeardownSandboxAsync(input, token),
                CancellationToken.None).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Teardown must never escape the workflow; failures are recorded on the result.
        catch (Exception teardownEx)
        {
            if (state.Status == WorkflowRunStatus.Completed)
            {
                state.Status = WorkflowRunStatus.Failed;
                state.FailureDetail = $"Teardown failed: {teardownEx.Message}";
            }
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Attempt bound and backoff for one activity.
    /// </summary>
    /// <param name="MaximumAttempts">Total attempts including the first; 1 disables retry.</param>
    /// <param name="InitialInterval">The wait after the first failed attempt.</param>
    /// <param name="BackoffCoefficient">The multiplier applied to the wait after each further failure.</param>
    private sealed record RetryPolicy(int MaximumAttempts, TimeSpan InitialInterval, double BackoffCoefficient)
    {
        /// <summary>Gets the backoff to wait after the given failed attempt (1-based).</summary>
        /// <param name="failedAttempt">The attempt number that just failed.</param>
        /// <returns>The delay before the next attempt.</returns>
        public TimeSpan BackoffAfter(int failedAttempt)
        {
            var ticks = InitialInterval.Ticks * Math.Pow(BackoffCoefficient, failedAttempt - 1);
            return TimeSpan.FromTicks((long)ticks);
        }
    }

    private sealed class WorkflowExecutionState
    {
        public bool Provisioned { get; set; }

        public bool PromptStarted { get; set; }

        public long FinalSeq { get; set; }

        public List<string> Artifacts { get; } = [];

        public WorkflowRunStatus Status { get; set; } = WorkflowRunStatus.Running;

        public string? FailureDetail { get; set; }
    }
}
