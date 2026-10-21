using Microsoft.Extensions.Options;
using OmpOS.Api.Options;
using OmpOS.Workflows;

namespace OmpOS.Api.Workflows;

/// <summary>
/// Starts a session run as a Temporal workflow on the configured task queue (<c>Workflows:Enabled=true</c>).
/// The worker executes the run; the handle carries no in-process completion.
/// </summary>
internal sealed class TemporalSessionRunDispatcher : ISessionRunDispatcher
{
    private readonly ITemporalWorkflowStarter _starter;
    private readonly IOptions<WorkflowsOptions> _options;

    /// <summary>Initializes a new instance of the <see cref="TemporalSessionRunDispatcher"/> class.</summary>
    /// <param name="starter">The Temporal workflow starter.</param>
    /// <param name="options">The workflows options supplying the task queue.</param>
    public TemporalSessionRunDispatcher(ITemporalWorkflowStarter starter, IOptions<WorkflowsOptions> options)
    {
        _starter = starter;
        _options = options;
    }

    /// <inheritdoc />
    public async Task<SessionRunHandle> StartAsync(SessionRunInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        var workflowId = SessionRunWorkflowIds.For(input);
        var runId = await _starter
            .StartAsync(workflowId, _options.Value.TaskQueue, input, cancellationToken)
            .ConfigureAwait(false);
        return new SessionRunHandle { WorkflowId = workflowId, RunId = runId };
    }
}
