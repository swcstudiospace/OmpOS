namespace Autonogrammer.Workspace;

/// <summary>
/// The default prompt for a head that cannot ask the user: it denies every request and says how to approve the setup.
/// </summary>
public sealed class DenyingSetupConsentPrompt : ISetupConsentPrompt
{
    /// <inheritdoc />
    public Task<SetupConsentDecision> AskAsync(SetupConsentRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Task.FromResult(SetupConsentDecision.Denied(
            $"no approval is recorded for repository '{request.Key.RepositoryPath}' and setup file hash {request.Key.SetupSha256}, " +
            "and this head cannot ask for one, so the setup steps did not run"));
    }
}
