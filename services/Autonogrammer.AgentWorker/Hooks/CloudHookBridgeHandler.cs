using Autonogrammer.Harness.AgentLoop.Hooks;
using Autonogrammer.Harness.Plugins.Hooks;

namespace Autonogrammer.AgentWorker.Hooks;

/// <summary>
/// Answers the omp hooks extension for one cloud session: each request runs the hooks of its event with the session's dispatcher, and the merged
/// decision goes back in the bridge's wire form. A request for an event a cloud worker does not run (see <see cref="CloudHookEvents"/>) or for
/// an omp-section event is let through without running anything, so a stray request can never start a hook the worker does not support.
/// </summary>
/// <param name="dispatcher">The session's dispatcher.</param>
/// <param name="conversation">The conversation the hooks run for.</param>
/// <param name="publish">Receives the run records of each dispatch, in order.</param>
internal sealed class CloudHookBridgeHandler(HookDispatcher dispatcher, HookSession conversation, Action<IReadOnlyList<HookRunRecord>> publish) : IHookBridgeHandler
{
    /// <inheritdoc />
    public async ValueTask<BridgeDecision> HandleAsync(BridgeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.Equals(request.Kind, BridgeRequest.OmpKind, StringComparison.Ordinal)
            || !HookEvents.TryParse(request.Event, out var hookEvent)
            || !CloudHookEvents.IsSupported(hookEvent))
        {
            return new BridgeDecision();
        }

        var decision = await dispatcher.DispatchNamedAsync(request.Event, isOmpEvent: false, conversation, request.Payload, cancellationToken).ConfigureAwait(false);
        publish(decision.Records);
        return ToBridge(decision);
    }

    private static BridgeDecision ToBridge(HookDecision decision)
    {
        var wire = decision.ToWire();
        return new BridgeDecision
        {
            Permission = wire.Permission,
            UserMessage = wire.UserMessage,
            AgentMessage = wire.AgentMessage,
            UpdatedInput = wire.UpdatedInput,
            AdditionalContext = wire.AdditionalContext,
            FollowupMessage = wire.FollowupMessage,
            Continue = wire.Continue,
            UpdatedMcpToolOutput = wire.UpdatedMcpToolOutput,
            Env = wire.Env,
            OmpResult = wire.OmpResult,
            HooksRun = wire.HooksRun,
        };
    }
}
