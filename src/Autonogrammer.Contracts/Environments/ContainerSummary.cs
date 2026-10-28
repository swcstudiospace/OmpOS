namespace Autonogrammer.Contracts.Environments;

/// <summary>One container as <c>podman ps --format json</c> lists it: what the "add from a running container" picker shows. It holds no environment and no secret.</summary>
/// <param name="Id">The full container id.</param>
/// <param name="Name">The first container name.</param>
/// <param name="Image">The image the container was created from.</param>
/// <param name="State">The state podman reports (<c>running</c>, <c>exited</c>, <c>created</c>, <c>paused</c>), or an empty string.</param>
/// <param name="DevContainerFolder">The project folder on this machine the devcontainer CLI recorded in the <c>devcontainer.local_folder</c> label, or <see langword="null"/> for a container that is not a dev container.</param>
public sealed record ContainerSummary(string Id, string Name, string Image, string State, string? DevContainerFolder)
{
    /// <summary>Gets whether the container is running.</summary>
    public bool IsRunning => string.Equals(State, "running", StringComparison.Ordinal);
}
