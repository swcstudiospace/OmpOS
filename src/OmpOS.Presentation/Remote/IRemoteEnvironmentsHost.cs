using OmpOS.Contracts.Environments;

namespace OmpOS.Presentation.Remote;

/// <summary>
/// What the remote environments page needs from the head: the profile store, the host list of the user's ssh configuration, the connection test and the
/// terminal that confirms a host key. The native head implements it over the ssh harness (the web head has no ssh and hides the page). Every method may
/// take time and may fail with an <see cref="IOException"/>; the view model reports a failure and never lets it escape.
/// </summary>
public interface IRemoteEnvironmentsHost
{
    /// <summary>Reads the saved environments.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The environments.</returns>
    Task<RemoteProfileList> LoadProfilesAsync(CancellationToken cancellationToken);

    /// <summary>Lists the host aliases of the user's ssh configuration.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The aliases and warnings.</returns>
    Task<RemoteHostCandidates> ListSshConfigHostsAsync(CancellationToken cancellationToken);

    /// <summary>Saves an environment: adds it, or replaces the one with the same identifier. The profile has passed <see cref="EnvironmentProfileValidator"/>.</summary>
    /// <param name="profile">The environment.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the environment is saved.</returns>
    Task SaveProfileAsync(EnvironmentProfile profile, CancellationToken cancellationToken);

    /// <summary>Deletes an environment.</summary>
    /// <param name="profileId">The identifier.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns><see langword="true"/> when an environment was deleted.</returns>
    Task<bool> DeleteProfileAsync(string profileId, CancellationToken cancellationToken);

    /// <summary>Tests the connection: connects without a prompt, checks omp, Bun, the platform and the folder, and installs nothing.</summary>
    /// <param name="profile">The host to test.</param>
    /// <param name="cancellationToken">Cancels the test and stops ssh.</param>
    /// <returns>The report: what was found and what must be fixed. A failing host is a report, not an exception.</returns>
    Task<RemotePreflightReport> TestConnectionAsync(SshProfile profile, CancellationToken cancellationToken);

    /// <summary>Opens the interactive terminal in which ssh asks the user to confirm the host key of a host seen for the first time.</summary>
    /// <param name="profile">The host.</param>
    /// <param name="cancellationToken">Cancels the opening.</param>
    /// <returns><see langword="true"/> when the terminal was opened; <see langword="false"/> when this head has no terminal to open (the page then tells the user to run ssh once by hand).</returns>
    Task<bool> OpenHostTrustTerminalAsync(SshProfile profile, CancellationToken cancellationToken);
}
