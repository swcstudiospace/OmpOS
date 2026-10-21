using System.Diagnostics.CodeAnalysis;
using OmpOS.Contracts.Workflows;
using Temporalio.Common;
using Temporalio.Workflows;

namespace OmpOS.Workflows.Worker;

/// <summary>
/// Temporal workflow for a durable session run (provision → run prompt → collect → teardown).
/// </summary>
/// <remarks>
/// <para>
/// This is the Temporal transport for the same state machine <see cref="DirectSessionRunner"/>
/// executes in-process: activities are the shared <see cref="ISessionRunActivities"/> contract,
/// teardown runs in a <c>finally</c> block once provisioning has succeeded, and failures surface
/// as <see cref="SessionRunResult"/> statuses rather than thrown exceptions.
/// </para>
/// <para>
/// Workflow code is deterministic: no <c>DateTime.Now</c>, <c>Task.Run</c>,
/// <c>Guid.NewGuid</c>, or <c>Random</c> — only <see cref="Workflow.UtcNow"/>,
/// <see cref="Workflow.Random"/>, <see cref="Workflow.NewGuid"/>, and
/// <see cref="Workflow.DelayAsync(TimeSpan, CancellationToken?)"/>.
/// </para>
/// </remarks>
[Workflow]
[SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "The Temporal SDK instantiates the workflow type and the replay tests reference it; it must stay visible outside the worker assembly.")]
public sealed class TemporalSessionRunWorkflow
{
    /// <summary>
    /// Executes the session run workflow.
    /// </summary>
    /// <param name="input">The workflow input (session identifiers, cursors, configuration).</param>
    /// <returns>The workflow run result including status and collected artifacts.</returns>
    [WorkflowRun]
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "The Temporal SDK requires the workflow entry point to be a public instance method.")]
    [SuppressMessage("Usage", "CA2007:Do not directly await a Task", Justification = "Temporal workflow continuations must resume on the workflow scheduler; ConfigureAwait(false) detaches them and wedges the run.")]
    [SuppressMessage("Usage", "MA0004:Use Task.ConfigureAwait", Justification = "Same as CA2007: the workflow scheduler owns the continuation context.")]
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "The workflow contract maps any activity failure to a Failed result so the run stays durable and observable; teardown is still guaranteed by the finally block.")]
    public async Task<SessionRunResult> RunAsync(SessionRunInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var provisioned = false;
        var finalSeq = 0L;
        var artifacts = Array.Empty<string>();
        var status = WorkflowRunStatus.Running;
        string? failureDetail = null;

        try
        {
            // NOTE: no ConfigureAwait(false) anywhere in this method. Workflow continuations
            // must resume on the Temporal workflow scheduler; detaching them wedges the run
            // (the workflow task completes with no commands and the run stalls).
            await Workflow.ExecuteActivityAsync<TemporalSessionRunActivities, OmpOS.Contracts.Sandboxes.SandboxDescriptor>(
                activities => activities.ProvisionSandboxAsync(input),
                ProvisionOptions());
            provisioned = true;

            finalSeq = await Workflow.ExecuteActivityAsync<TemporalSessionRunActivities, long>(
                activities => activities.RunPromptAsync(input),
                RunPromptOptions());

            artifacts = (await Workflow.ExecuteActivityAsync<TemporalSessionRunActivities, IReadOnlyList<string>>(
                activities => activities.CollectArtifactsAsync(input),
                CollectOptions())).ToArray();
            status = WorkflowRunStatus.Completed;
        }
        catch (OperationCanceledException oce) when (Workflow.CancellationToken.IsCancellationRequested)
        {
            status = WorkflowRunStatus.Cancelled;
            failureDetail = oce.Message;
        }
        catch (Exception ex)
        {
            status = WorkflowRunStatus.Failed;
            failureDetail = ex.Message;
        }
        finally
        {
            (status, failureDetail) = await TeardownBestEffortAsync(input, provisioned, status, failureDetail);
        }

        return new SessionRunResult
        {
            Status = status,
            FinalSeq = finalSeq,
            Artifacts = artifacts,
            FailureDetail = failureDetail,
        };
    }

    /// <summary>
    /// Maps a teardown failure onto the workflow result without throwing.
    /// </summary>
    /// <param name="status">The status computed by the main workflow body.</param>
    /// <param name="failureDetail">The failure detail computed by the main workflow body, if any.</param>
    /// <param name="teardownMessage">The teardown exception message.</param>
    /// <returns>The status and failure detail with the teardown failure recorded.</returns>
    internal static (WorkflowRunStatus Status, string? FailureDetail) ApplyTeardownFailure(WorkflowRunStatus status, string? failureDetail, string teardownMessage)
    {
        if (status == WorkflowRunStatus.Completed)
        {
            return (WorkflowRunStatus.Failed, $"Teardown failed: {teardownMessage}");
        }

        return (status, failureDetail);
    }

    /// <summary>
    /// Tears down the sandbox when provisioned, recording failures as a result status.
    /// </summary>
    /// <param name="input">The workflow input.</param>
    /// <param name="provisioned">Whether provisioning succeeded.</param>
    /// <param name="status">The status computed by the main workflow body.</param>
    /// <param name="failureDetail">The failure detail computed by the main workflow body, if any.</param>
    /// <returns>The status and failure detail with any teardown failure recorded. Never throws.</returns>
    [SuppressMessage("Usage", "CA2007:Do not directly await a Task", Justification = "Temporal workflow continuations must resume on the workflow scheduler; ConfigureAwait(false) detaches them and wedges the run.")]
    [SuppressMessage("Usage", "MA0004:Use Task.ConfigureAwait", Justification = "Same as CA2007: the workflow scheduler owns the continuation context.")]
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Teardown failure is recorded as a result status so it can never escape the finally block and mask the computed status.")]
    private static async Task<(WorkflowRunStatus Status, string? FailureDetail)> TeardownBestEffortAsync(SessionRunInput input, bool provisioned, WorkflowRunStatus status, string? failureDetail)
    {
        if (!provisioned)
        {
            return (status, failureDetail);
        }

        try
        {
            await Workflow.ExecuteActivityAsync<TemporalSessionRunActivities, bool>(
                activities => activities.TeardownSandboxAsync(input),
                TeardownOptions());
        }
        catch (Exception teardownEx)
        {
            // A teardown failure is a result status, never a thrown exception: an escaping
            // throw would mask the computed status and fail the workflow task, causing the
            // server to retry the whole run (mirrors DirectSessionRunner.TryTeardownAsync).
            return ApplyTeardownFailure(status, failureDetail, teardownEx.Message);
        }

        return (status, failureDetail);
    }

    private static ActivityOptions ProvisionOptions() => new()
    {
        ScheduleToCloseTimeout = TimeSpan.FromMinutes(10),
        RetryPolicy = new RetryPolicy
        {
            InitialInterval = TimeSpan.FromSeconds(1),
            BackoffCoefficient = 2f,
            MaximumAttempts = 3,
        },
    };

    private static ActivityOptions RunPromptOptions() => new()
    {
        ScheduleToCloseTimeout = TimeSpan.FromHours(1),
        HeartbeatTimeout = TimeSpan.FromMinutes(5),
        RetryPolicy = new RetryPolicy { MaximumAttempts = 1 },
    };

    private static ActivityOptions CollectOptions() => new()
    {
        ScheduleToCloseTimeout = TimeSpan.FromMinutes(5),
        RetryPolicy = new RetryPolicy
        {
            InitialInterval = TimeSpan.FromSeconds(1),
            BackoffCoefficient = 2f,
            MaximumAttempts = 3,
        },
    };

    private static ActivityOptions TeardownOptions() => new()
    {
        ScheduleToCloseTimeout = TimeSpan.FromMinutes(10),
        RetryPolicy = new RetryPolicy
        {
            InitialInterval = TimeSpan.FromSeconds(1),
            BackoffCoefficient = 2f,
            MaximumAttempts = 5,
        },
    };
}
