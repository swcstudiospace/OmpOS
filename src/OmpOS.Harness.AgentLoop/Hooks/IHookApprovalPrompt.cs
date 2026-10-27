namespace OmpOS.Harness.AgentLoop.Hooks;

/// <summary>
/// Asks the human to decide when a hook answers <c>ask</c>. The host implements it with its own approval surface; the server
/// calls it for every handler decision whose permission is <c>ask</c> and answers the extension with the outcome.
/// </summary>
public interface IHookApprovalPrompt
{
    /// <summary>
    /// Asks the human whether the action of a request may proceed. The method must honour the token: the server cancels it when
    /// the handler timeout elapses and when the server stops. An exception, a timeout and a refusal all deny the action.
    /// </summary>
    /// <param name="request">The request that produced the decision.</param>
    /// <param name="decision">The handler's decision, whose permission is <c>ask</c>; its messages tell the human what is asked.</param>
    /// <param name="cancellationToken">Cancelled on timeout and on server shutdown.</param>
    /// <returns>True when the human approved the action, false when the human declined it.</returns>
    ValueTask<bool> AskAsync(BridgeRequest request, BridgeDecision decision, CancellationToken cancellationToken);
}
