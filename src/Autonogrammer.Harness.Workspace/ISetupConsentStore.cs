namespace Autonogrammer.Workspace;

/// <summary>
/// Remembers which repository setup files the user has approved to run. An answer is keyed by repository path and the
/// hash of the setup file, so editing the file withdraws the approval.
/// </summary>
public interface ISetupConsentStore
{
    /// <summary>
    /// Returns whether consent is recorded for the key. A store that cannot read its state answers false, so a damaged
    /// store never grants a run.
    /// </summary>
    /// <param name="key">The repository and setup file hash.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>True when the user approved exactly this setup file for this repository.</returns>
    Task<bool> IsGrantedAsync(SetupConsentKey key, CancellationToken cancellationToken);

    /// <summary>
    /// Records consent for the key. It replaces any earlier approval of the same repository, so only the most recently
    /// approved setup file of a repository stays approved.
    /// </summary>
    /// <param name="key">The repository and setup file hash.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <exception cref="IOException">The consent could not be saved.</exception>
    /// <exception cref="UnauthorizedAccessException">The consent could not be saved.</exception>
    Task RecordGrantAsync(SetupConsentKey key, CancellationToken cancellationToken);
}
