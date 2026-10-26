using OmpOS.Contracts.Workflows;
using OmpOS.Coordination;
using OmpOS.Workflows;

namespace OmpOS.Api.Workflows;

/// <summary>
/// Runs a session prompt in this process through <see cref="ISessionRunWorkflow"/>. The caller acquires the session's run lease before
/// admitting the prompt and hands it over here; the run takes ownership of it. The run keeps the lease alive through <see cref="LeaseRenewal"/>.
/// The workflow invokes <see cref="SessionRunInput.OnPromptSettled"/> after the collect phase, in its finally block, whenever the prompt started,
/// including after a prompt failure. That callback releases the lease.
/// </summary>
/// <remarks>
/// Releasing in the callback rather than at the end of the run lets the next turn start without waiting for the run's remaining cleanup. The
/// callback is never invoked when the prompt did not start; in that case this method's <c>finally</c> block releases the lease. Either way the
/// lease is released exactly once. If the lease is lost, the run is cancelled and its result reports <see cref="WorkflowRunStatus.Failed"/> with
/// the detail <c>run lease lost</c>.
/// </remarks>
internal sealed class DirectSessionRunDispatcher
{
    /// <summary>Gets the failure detail reported when the run's lease is lost.</summary>
    internal const string LeaseLostDetail = "run lease lost";

    private readonly ISessionRunWorkflow _workflow;
    private readonly ILogger<DirectSessionRunDispatcher> _logger;

    /// <summary>Initializes a new instance of the <see cref="DirectSessionRunDispatcher"/> class.</summary>
    /// <param name="workflow">The in-process session-run workflow.</param>
    /// <param name="logger">The logger.</param>
    public DirectSessionRunDispatcher(ISessionRunWorkflow workflow, ILogger<DirectSessionRunDispatcher> logger)
    {
        _workflow = workflow;
        _logger = logger;
    }

    /// <summary>
    /// Runs the admitted turn under the lease the caller already holds. The renewal task is created and awaited in this method. The
    /// settled-prompt callback stops renewal and then disposes the lease; the <c>finally</c> block stops renewal, awaits its exit, and
    /// disposes the lease only if the callback did not complete the release.
    /// </summary>
    /// <param name="input">The run input.</param>
    /// <param name="lease">The held run lease; renewed and released by the run.</param>
    /// <param name="cancellationToken">The caller's cancellation (host shutdown).</param>
    /// <returns>The run result, or a failed result when the lease was lost.</returns>
    public async Task<SessionRunResult> RunAsync(SessionRunInput input, ISessionRunLease lease, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(lease);
        using var run = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var renewalStop = new CancellationTokenSource();

        var renewalTask = new LeaseRenewal(input.SessionId, lease, run, _logger).RunUntilStoppedAsync(renewalStop.Token);
        var leaseDisposed = false;

        // Invoked by the workflow after the collect phase. It does not await the renewal task: the finally block awaits it where it was created.
        // leaseDisposed is set only after DisposeAsync returns, so a dispose that fails is retried by the finally block.
        async ValueTask ReleaseLeaseOnSettledAsync()
        {
            if (leaseDisposed)
            {
                return;
            }

            await renewalStop.CancelAsync().ConfigureAwait(false);
            await lease.DisposeAsync().ConfigureAwait(false);
            leaseDisposed = true;
        }

        var workflowInput = input with { OnPromptSettled = _ => ReleaseLeaseOnSettledAsync() };
        SessionRunResult result;
        bool leaseLost;
        try
        {
            result = await _workflow.RunAsync(workflowInput, new DirectWorkflowContext(), run.Token).ConfigureAwait(false);
        }
        finally
        {
            // Fallback for a prompt that never started, or whose release did not complete. Otherwise this only awaits the renewal.
            await renewalStop.CancelAsync().ConfigureAwait(false);
            try
            {
                leaseLost = await renewalTask.ConfigureAwait(false);
            }
            finally
            {
                if (!leaseDisposed)
                {
                    await lease.DisposeAsync().ConfigureAwait(false);
                    leaseDisposed = true;
                }
            }
        }

        return ToResult(result, leaseLost);
    }

    /// <summary>Maps the run result to the result the caller sees, replacing it with a failure when the lease was lost.</summary>
    /// <param name="result">The workflow's result.</param>
    /// <param name="leaseLost">Whether the lease was lost during the run.</param>
    /// <returns>The result to return.</returns>
    private static SessionRunResult ToResult(SessionRunResult result, bool leaseLost) =>
        leaseLost
            ? new SessionRunResult
            {
                Status = WorkflowRunStatus.Failed,
                FinalSeq = result.FinalSeq,
                Artifacts = result.Artifacts,
                FailureDetail = LeaseLostDetail,
            }
            : result;
}
