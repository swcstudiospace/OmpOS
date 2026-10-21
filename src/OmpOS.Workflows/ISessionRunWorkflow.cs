namespace OmpOS.Workflows;

/// <summary>
/// Workflow contract for a durable session run (provision → run prompt → collect → teardown).
/// </summary>
/// <remarks>
/// <see cref="DirectSessionRunner"/> implements this interface and executes the
/// <see cref="ISessionRunActivities"/> contract in-process through the same
/// provision → run prompt → collect → teardown state machine (WFLW-01).
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
