namespace Autonogrammer.Harness.Remote.Containers;

/// <summary>How Autonogrammer runs the user's <c>podman</c> client. The container runtime is podman only.</summary>
public sealed record PodmanClientOptions
{
    /// <summary>Gets the podman executable: a bare name resolved from <c>PATH</c> or an absolute path. Defaults to <c>podman</c>. A <see cref="Autonogrammer.Contracts.Environments.ContainerProfile.PodmanPath"/> overrides it.</summary>
    public string PodmanPath { get; init; } = "podman";

    /// <summary>Gets how long a short non-interactive podman command (an inspect, a listing or a probe) may run. Defaults to 60 seconds.</summary>
    public TimeSpan CommandTimeout { get; init; } = TimeSpan.FromSeconds(60);
}
