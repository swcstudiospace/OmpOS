namespace Autonogrammer.Api.Cloud;

/// <summary>Supplies the credential for git host calls and for a worker's checkouts: a GitHub App installation token minted for the given repositories and permissions, or a configured personal access token.</summary>
internal interface IGitCredentialSource
{
    /// <summary>Gets whether a credential is configured. When false, <see cref="GetAsync"/> throws <see cref="GitHostException"/> with <see cref="GitHostFailure.NotConfigured"/>.</summary>
    bool IsConfigured { get; }

    /// <summary>Returns a credential valid for the repositories with the permissions, refreshing it before it expires.</summary>
    /// <param name="repositories">The repositories the credential must cover.</param>
    /// <param name="permissions">The permissions it needs.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The credential.</returns>
    Task<GitCredential> GetAsync(IReadOnlyList<GitRepository> repositories, GitPermissions permissions, CancellationToken cancellationToken);
}
