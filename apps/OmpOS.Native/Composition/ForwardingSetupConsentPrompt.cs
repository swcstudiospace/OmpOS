using OmpOS.Workspace;

namespace OmpOS.Native.Composition;

/// <summary>
/// The prompt that asks the user to approve the setup commands of a worktree (<c>.cursor/worktrees.json</c>). The shell connects the real dialog to <see cref="Target"/>; until then, and when the
/// target is cleared, the answer is the denying one: no setup command runs without an approval.
/// </summary>
internal sealed class ForwardingSetupConsentPrompt : ISetupConsentPrompt
{
    private static readonly DenyingSetupConsentPrompt Denying = new();

    private ISetupConsentPrompt? _target;

    /// <summary>Gets or sets the prompt that asks the user, or null for none.</summary>
    public ISetupConsentPrompt? Target
    {
        get => Volatile.Read(ref _target);
        set => Volatile.Write(ref _target, value);
    }

    /// <inheritdoc/>
    public Task<SetupConsentDecision> AskAsync(SetupConsentRequest request, CancellationToken cancellationToken) =>
        (Target ?? Denying).AskAsync(request, cancellationToken);
}
