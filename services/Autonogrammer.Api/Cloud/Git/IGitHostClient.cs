namespace Autonogrammer.Api.Cloud;

/// <summary>
/// The calls the control plane makes to a git host: repository facts, file reads at a ref, branch existence and pull request creation. The control
/// plane never clones or pushes: the worker does, with its own credential. Every call mints or reuses a credential limited to the repository and
/// the permissions it needs.
/// </summary>
internal interface IGitHostClient
{
    /// <summary>Returns the name of the repository's default branch.</summary>
    /// <param name="repository">The repository.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The default branch name.</returns>
    /// <exception cref="GitHostException">The call failed.</exception>
    Task<string> GetDefaultBranchAsync(GitRepository repository, CancellationToken cancellationToken);

    /// <summary>Returns whether a branch exists on the host.</summary>
    /// <param name="repository">The repository.</param>
    /// <param name="branch">The branch name.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>True when the branch exists.</returns>
    /// <exception cref="GitHostException">The call failed for a reason other than the branch being absent.</exception>
    Task<bool> BranchExistsAsync(GitRepository repository, string branch, CancellationToken cancellationToken);

    /// <summary>Reads a UTF-8 text file at a ref.</summary>
    /// <param name="repository">The repository.</param>
    /// <param name="path">The path in the repository.</param>
    /// <param name="reference">The branch, tag or commit; null for the default branch.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The text, or null when the file does not exist or is not a file.</returns>
    /// <exception cref="GitHostException">The call failed for a reason other than the file being absent.</exception>
    Task<string?> ReadFileAsync(GitRepository repository, string path, string? reference, CancellationToken cancellationToken);

    /// <summary>Opens a pull request.</summary>
    /// <param name="repository">The repository.</param>
    /// <param name="request">The pull request to open.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The pull request the host created.</returns>
    /// <exception cref="GitHostException">The host refused or could not be reached.</exception>
    Task<GitPullRequestResult> CreatePullRequestAsync(GitRepository repository, GitPullRequestRequest request, CancellationToken cancellationToken);
}
