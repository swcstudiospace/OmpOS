namespace OmpOS.Harness.AgentLoop.Hooks;

/// <summary>
/// The code behind a <see cref="HookBridgeServer"/>: it runs the configured hooks for one request and returns the merged
/// decision. The bridge lives in this assembly and the hook dispatcher elsewhere, so the host adapts the dispatcher to this
/// interface.
/// </summary>
public interface IHookBridgeHandler
{
    /// <summary>
    /// Runs the hooks for one request. The method must honour the token: the server cancels it when the handler timeout
    /// elapses and when the server stops. A handler that throws, returns null or times out makes the server answer by the
    /// request's fail-open or fail-closed rule.
    /// </summary>
    /// <param name="request">The request the extension posted. Its payload is never undefined.</param>
    /// <param name="cancellationToken">Cancelled on handler timeout and on server shutdown.</param>
    /// <returns>The merged decision.</returns>
    ValueTask<BridgeDecision> HandleAsync(BridgeRequest request, CancellationToken cancellationToken);
}
