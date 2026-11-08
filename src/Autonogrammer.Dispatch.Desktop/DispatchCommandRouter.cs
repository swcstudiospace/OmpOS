using Autonogrammer.Contracts.Dispatch;

namespace Autonogrammer.Dispatch.Desktop;

/// <summary>
/// Applies the desktop policy, the local approval of a Computer Use task, and then the one handler that claims the operation. It runs
/// only for a command that already verified and was admitted by the dedupe store.
/// </summary>
public sealed class DispatchCommandRouter
{
    private readonly IReadOnlyList<IDispatchCommandHandler> _handlers;
    private readonly DispatchPolicyStore _policy;
    private readonly DispatchApprovalFlow _approvals;
    private readonly DispatchComputerUseConfig _computerUse;
    private readonly string _controllerName;

    /// <summary>Creates the router.</summary>
    /// <param name="handlers">The handlers, one per operation family.</param>
    /// <param name="policy">The desktop policy.</param>
    /// <param name="approvals">The local approval flow.</param>
    /// <param name="computerUse">The Computer Use configuration keys.</param>
    /// <param name="controllerName">The name the approval dialog shows for the controller.</param>
    public DispatchCommandRouter(
        IReadOnlyList<IDispatchCommandHandler> handlers,
        DispatchPolicyStore policy,
        DispatchApprovalFlow approvals,
        DispatchComputerUseConfig computerUse,
        string controllerName)
    {
        _handlers = handlers ?? throw new ArgumentNullException(nameof(handlers));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _approvals = approvals ?? throw new ArgumentNullException(nameof(approvals));
        _computerUse = computerUse;
        _controllerName = controllerName ?? throw new ArgumentNullException(nameof(controllerName));
    }

    /// <summary>Routes one admitted command to its outcome.</summary>
    /// <param name="command">The verified command.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The terminal outcome.</returns>
    public ValueTask<DispatchOutcome> RouteAsync(DispatchCommand command, CancellationToken cancellationToken) =>
        RouteAsync(command, null, cancellationToken);

    internal async ValueTask<DispatchOutcome> RouteAsync(
        DispatchCommand command,
        Func<DispatchCommandState, CancellationToken, ValueTask>? reportState,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var decision = DispatchPolicyEvaluator.Evaluate(command, _policy.Current, _computerUse);
        if (!decision.Allowed)
        {
            return DispatchOutcome.Failed(decision.Reason ?? DispatchFailureReason.PolicyDenied);
        }

        var handler = _handlers.FirstOrDefault(candidate => candidate.CanHandle(command.Op));
        if (handler is null)
        {
            return DispatchOutcome.Failed(DispatchFailureReason.UnsupportedOperation);
        }

        if (decision.Task is { } task && task.ToolClasses.Contains(DispatchTaskPayload.ComputerUse, StringComparer.Ordinal))
        {
            if (reportState is not null)
            {
                await reportState(DispatchCommandState.AwaitingApproval, cancellationToken).ConfigureAwait(false);
            }

            // Every Computer Use task starts with a local approval. Nothing remote can answer it.
            var approval = await _approvals.ApproveTaskAsync(
                new DispatchApprovalRequest(_controllerName, task.ToolClasses, task.ApprovalText()),
                cancellationToken).ConfigureAwait(false);
            if (approval.PauseRequested)
            {
                _policy.Save(_policy.Current with { Paused = true });
            }

            if (!approval.Approved)
            {
                return DispatchOutcome.Failed(approval.Reason ?? DispatchFailureReason.ApprovalDenied);
            }
        }

        if (reportState is not null)
        {
            await reportState(DispatchCommandState.Accepted, cancellationToken).ConfigureAwait(false);
            await reportState(DispatchCommandState.Running, cancellationToken).ConfigureAwait(false);
        }

        try
        {
            return await handler.HandleAsync(command, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException and not OutOfMemoryException)
        {
            return DispatchOutcome.Failed(DispatchFailureReason.ExecutionError);
        }
    }
}
