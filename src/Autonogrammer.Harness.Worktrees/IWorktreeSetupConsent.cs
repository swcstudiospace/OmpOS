namespace Autonogrammer.Worktrees;

/// <summary>
/// The consent a worktree's setup needs before anything runs. The host implements it over the setup consent store and prompt it already has for <c>.autonogrammer/setup.json</c>
/// (<c>ISetupConsentStore</c> and <c>ISetupConsentPrompt</c> of <c>Autonogrammer.Harness.Workspace</c>): it answers <see langword="true"/> at once when the store holds the identity, otherwise it asks the user
/// and records a granted answer. A store or prompt that cannot answer is a refusal.
/// </summary>
public interface IWorktreeSetupConsent
{
    /// <summary>Tells whether the setup may run.</summary>
    /// <param name="request">The repository, setup and file to show.</param>
    /// <param name="cancellationToken">Cancels the question. A cancelled question is not an approval.</param>
    /// <returns><see langword="true"/> when the user approved exactly this setup for this repository, now or earlier.</returns>
    Task<bool> IsGrantedAsync(WorktreeSetupConsentRequest request, CancellationToken cancellationToken);
}
