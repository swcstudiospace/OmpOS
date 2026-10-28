using OmpOS.Contracts.Environments;

namespace OmpOS.Presentation.Remote;

/// <summary>
/// What the container and WSL part of the environments page needs from the head besides the profile store (<see cref="IRemoteEnvironmentsHost"/> loads, saves and deletes
/// profiles of every kind): the connection tests and the start of a stopped container. The native head implements it over the podman and WSL harness; the web head has
/// neither and hides the section. A failing environment is a report, never an exception, but a method may still fail with an <see cref="IOException"/>.
/// </summary>
public interface IContainerEnvironmentsHost
{
    /// <summary>Gets whether WSL environments can run on this computer (Windows with wsl.exe). When false the page keeps stored WSL environments but offers no way to add or use one.</summary>
    bool SupportsWsl { get; }

    /// <summary>Tests a container environment: podman, the container and its state, the user, the folder, omp and Bun. Nothing is installed, changed or started.</summary>
    /// <param name="profile">The container.</param>
    /// <param name="cancellationToken">Cancels the test and stops podman.</param>
    /// <returns>The report: what was found and what must be fixed.</returns>
    Task<RemotePreflightReport> TestContainerAsync(ContainerProfile profile, CancellationToken cancellationToken);

    /// <summary>Tests a WSL environment: wsl.exe, the distribution, the user, the folder, omp and Bun. Nothing is installed or changed.</summary>
    /// <param name="profile">The distribution.</param>
    /// <param name="cancellationToken">Cancels the test and stops wsl.exe.</param>
    /// <returns>The report: what was found and what must be fixed.</returns>
    Task<RemotePreflightReport> TestWslAsync(WslProfile profile, CancellationToken cancellationToken);

    /// <summary>Starts a stopped container with <c>podman start</c>. The page calls it only when the user pressed Start container.</summary>
    /// <param name="profile">The container.</param>
    /// <param name="cancellationToken">Cancels the start and stops podman.</param>
    /// <returns><see langword="null"/> when podman started the container; otherwise the diagnosis of why it did not, with the fix.</returns>
    Task<RemoteDiagnosis?> StartContainerAsync(ContainerProfile profile, CancellationToken cancellationToken);
}
