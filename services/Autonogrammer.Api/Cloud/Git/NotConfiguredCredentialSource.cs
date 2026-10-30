namespace Autonogrammer.Api.Cloud;

/// <summary>The credential source of a host with neither a GitHub App nor a personal access token: agents can work on repositories the worker can read without credentials, but no pull request can be opened.</summary>
internal sealed class NotConfiguredCredentialSource : IGitCredentialSource
{
    /// <inheritdoc />
    public bool IsConfigured => false;

    /// <inheritdoc />
    public Task<GitCredential> GetAsync(IReadOnlyList<GitRepository> repositories, GitPermissions permissions, CancellationToken cancellationToken) =>
        throw new GitHostException(GitHostFailure.NotConfigured, "No GitHub App and no personal access token is configured (Cloud:Git).");
}
