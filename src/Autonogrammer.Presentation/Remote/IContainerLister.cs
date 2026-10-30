using Autonogrammer.Contracts.Environments;

namespace Autonogrammer.Presentation.Remote;

/// <summary>
/// What the "add from a running container" picker needs from the head: the containers podman lists (<c>podman ps --format json</c>). The native head implements it
/// over the podman client (the web head has no podman and hides the picker). A failure is a <see cref="ContainerListing"/> with a problem, never an exception.
/// </summary>
public interface IContainerLister
{
    /// <summary>Lists the running containers.</summary>
    /// <param name="cancellationToken">Cancels the listing and stops podman.</param>
    /// <returns>The containers, or the diagnosis of why they could not be listed.</returns>
    Task<ContainerListing> ListRunningAsync(CancellationToken cancellationToken);
}
