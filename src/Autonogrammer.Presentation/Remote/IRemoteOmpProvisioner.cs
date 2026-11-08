namespace Autonogrammer.Presentation.Remote;

/// <summary>
/// Checks and installs Omp on one saved ssh environment. The head implements it over the provisioning harness; a head without it hides the Check Omp and
/// Install Omp buttons. A check reads the machine and writes nothing. An install applies exactly the lines a check named, and only when the machine still
/// needs exactly those lines; otherwise it writes nothing and reports that a new check is needed. Every outcome is a report: none is thrown.
/// </summary>
public interface IRemoteOmpProvisioner
{
    /// <summary>Reads the environment and says what an install would apply. It writes nothing.</summary>
    /// <param name="environmentId">The identifier of the saved environment.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>The findings and the planned lines, or why the check could not be made.</returns>
    Task<RemoteOmpReport> CheckAsync(string environmentId, CancellationToken cancellationToken);

    /// <summary>Applies the lines of the last check of the environment, once.</summary>
    /// <param name="environmentId">The identifier of the saved environment.</param>
    /// <param name="checkedActions">The lines the page showed from the last check, in order.</param>
    /// <param name="cancellationToken">Cancels the install.</param>
    /// <returns>What was applied, or why nothing was written.</returns>
    Task<RemoteOmpReport> InstallAsync(string environmentId, IReadOnlyList<string> checkedActions, CancellationToken cancellationToken);
}
