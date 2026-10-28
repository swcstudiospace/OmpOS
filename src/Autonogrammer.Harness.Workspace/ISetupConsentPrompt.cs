namespace Autonogrammer.Workspace;

/// <summary>
/// Asks the user whether a repository's setup steps may run. Each head implements it in its own way (a dialog, a
/// terminal question, a web form); the workspace library only needs the answer.
/// </summary>
public interface ISetupConsentPrompt
{
    /// <summary>
    /// Asks once for the setup described by <paramref name="request"/>.
    /// </summary>
    /// <param name="request">The repository, setup file hash, commands and environment to show.</param>
    /// <param name="cancellationToken">Cancels the question. A cancelled question is not an approval.</param>
    /// <returns>The user's answer.</returns>
    Task<SetupConsentDecision> AskAsync(SetupConsentRequest request, CancellationToken cancellationToken);
}
