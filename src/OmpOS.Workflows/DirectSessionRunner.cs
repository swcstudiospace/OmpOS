using OmpOS.Contracts.Workflows;

namespace OmpOS.Workflows;

/// <summary>
/// Direct in-process implementation of <see cref="ISessionRunWorkflow"/>.
/// </summary>
/// <remarks>
/// Executes the workflow activities sequentially (provision, run prompt, collect) with
/// teardown in a <c>finally</c> block once provisioning has succeeded. Deterministic
/// primitives (clock, random, delay, heartbeat) arrive per call through
/// <see cref="IWorkflowContext"/>. Used when <c>Workflows:Enabled=false</c> (default).
/// </remarks>
public sealed class DirectSessionRunner : ISessionRunWorkflow
{
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
            await _activities.ProvisionSandboxAsync(input, cancellationToken).ConfigureAwait(false);
            state.Provisioned = true;

            state.FinalSeq = await _activities.RunPromptAsync(
                input,
                details => ctx.Heartbeat(details),
                cancellationToken).ConfigureAwait(false);

            state.Artifacts.AddRange(
                await _activities.CollectArtifactsAsync(input, cancellationToken).ConfigureAwait(false));

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
            if (state.Provisioned)
            {
                await TryTeardownAsync(input, state).ConfigureAwait(false);
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

    private async Task TryTeardownAsync(SessionRunInput input, WorkflowExecutionState state)
    {
        try
        {
            await _activities.TeardownSandboxAsync(input, CancellationToken.None).ConfigureAwait(false);
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

    private sealed class WorkflowExecutionState
    {
        public bool Provisioned { get; set; }

        public long FinalSeq { get; set; }

        public List<string> Artifacts { get; } = [];

        public WorkflowRunStatus Status { get; set; } = WorkflowRunStatus.Running;

        public string? FailureDetail { get; set; }
    }
}
