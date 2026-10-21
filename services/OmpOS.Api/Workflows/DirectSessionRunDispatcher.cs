using OmpOS.Workflows;

namespace OmpOS.Api.Workflows;

/// <summary>
/// Runs the session in-process through <see cref="ISessionRunWorkflow"/>. This is the default path
/// (<c>Workflows:Enabled=false</c>); the run starts synchronously and its completion is returned on the handle.
/// </summary>
internal sealed class DirectSessionRunDispatcher : ISessionRunDispatcher
{
    private readonly ISessionRunWorkflow _workflow;

    /// <summary>Initializes a new instance of the <see cref="DirectSessionRunDispatcher"/> class.</summary>
    /// <param name="workflow">The in-process session-run workflow.</param>
    public DirectSessionRunDispatcher(ISessionRunWorkflow workflow)
    {
        _workflow = workflow;
    }

    /// <inheritdoc />
    public Task<SessionRunHandle> StartAsync(SessionRunInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        var completion = _workflow.RunAsync(input, new DirectWorkflowContext(), cancellationToken);
        return Task.FromResult(new SessionRunHandle
        {
            WorkflowId = SessionRunWorkflowIds.For(input),
            RunId = input.RunId,
            Completion = completion,
        });
    }
}
