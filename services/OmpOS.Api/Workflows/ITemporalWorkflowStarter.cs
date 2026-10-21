using OmpOS.Workflows;

namespace OmpOS.Api.Workflows;

/// <summary>
/// Starts a session-run workflow on the Temporal server. Hides the Temporal client so the dispatcher
/// can be tested without a server.
/// </summary>
internal interface ITemporalWorkflowStarter
{
    /// <summary>Starts the workflow under the given id on the given task queue.</summary>
    /// <param name="workflowId">The workflow identifier; a repeated start with the same id targets the same run.</param>
    /// <param name="taskQueue">The task queue the worker polls.</param>
    /// <param name="input">The session-run input passed as the workflow argument.</param>
    /// <param name="cancellationToken">Cancels the start call.</param>
    /// <returns>The server run identifier, or null when the server did not report one.</returns>
    Task<string?> StartAsync(string workflowId, string taskQueue, SessionRunInput input, CancellationToken cancellationToken);
}
