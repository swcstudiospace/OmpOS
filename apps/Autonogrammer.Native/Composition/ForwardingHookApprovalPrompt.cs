using Autonogrammer.Harness.AgentLoop.Hooks;

namespace Autonogrammer.Native.Composition;

/// <summary>
/// The prompt the hooks service asks when a hook answers <c>ask</c>. The service is opened before any window exists, so it is given this forwarder; the shell connects the real prompt (the
/// approval card of the session) to <see cref="Target"/> once it has one. Until then, and when the target is cleared, a request that needs the user is denied, which is what the service does without a prompt.
/// </summary>
internal sealed class ForwardingHookApprovalPrompt : IHookApprovalPrompt
{
    private IHookApprovalPrompt? _target;

    /// <summary>Gets or sets the prompt that asks the user, or null for none.</summary>
    public IHookApprovalPrompt? Target
    {
        get => Volatile.Read(ref _target);
        set => Volatile.Write(ref _target, value);
    }

    /// <inheritdoc/>
    public ValueTask<bool> AskAsync(BridgeRequest request, BridgeDecision decision, CancellationToken cancellationToken) =>
        Target is { } target ? target.AskAsync(request, decision, cancellationToken) : ValueTask.FromResult(false);
}
