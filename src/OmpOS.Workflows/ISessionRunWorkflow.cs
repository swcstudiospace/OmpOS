namespace OmpOS.Workflows;

/// <summary>
/// Workflow contract for a durable session run (provision → run prompt → collect → teardown).
/// </summary>
/// <remarks>
/// The direct runner (<see cref="DirectSessionRunner"/>) implements this interface.
/// The Temporal worker reuses the same state machine and the same
/// <see cref="ISessionRunActivities"/> contract through its own
/// <c>[Workflow]</c> entry point, so the definition exists once (WFLW-01).
/// This assembly (L2) carries no Temporal dependency.
/// </remarks>
public interface ISessionRunWorkflow
{
    /// <summary>
    /// Executes the session run workflow.
    /// </summary>
    /// <param name="input">The workflow input (session identifiers, cursors, configuration).</param>
    /// <param name="ctx">The workflow context providing deterministic time, random, and delay primitives.</param>
    /// <param name="cancellationToken">Cancels the workflow execution.</param>
    /// <returns>The workflow run result including status and collected artifacts.</returns>
    Task<SessionRunResult> RunAsync(SessionRunInput input, IWorkflowContext ctx, CancellationToken cancellationToken);
}
