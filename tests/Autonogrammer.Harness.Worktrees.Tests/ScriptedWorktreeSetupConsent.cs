namespace Autonogrammer.Worktrees.Tests;

/// <summary>A consent that answers as scripted and remembers what it was asked.</summary>
internal sealed class ScriptedWorktreeSetupConsent(bool answer) : IWorktreeSetupConsent
{
    /// <summary>Gets the requests it was asked about.</summary>
    public List<WorktreeSetupConsentRequest> Asked { get; } = [];

    /// <inheritdoc/>
    public Task<bool> IsGrantedAsync(WorktreeSetupConsentRequest request, CancellationToken cancellationToken)
    {
        Asked.Add(request);
        return Task.FromResult(answer);
    }
}
