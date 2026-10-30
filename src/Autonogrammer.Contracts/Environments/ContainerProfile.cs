namespace Autonogrammer.Contracts.Environments;

/// <summary>
/// How to reach a running local podman container (a dev container or any other): the container, the user inside it, the workspace folder inside it.
/// The container runtime is podman only: there is no other runtime option. The profile holds references only and has no property that could carry a
/// secret: Autonogrammer passes secrets to the omp inside the container on the stdin of <c>podman exec</c> and never stores them.
/// Use <see cref="EnvironmentProfileValidator"/> before the profile is stored or used to start a process.
/// </summary>
public sealed record ContainerProfile
{
    /// <summary>
    /// Gets the container name or id, as <c>podman ps</c> prints it. It matches <see cref="ContainerProfileRules.IsValidContainerReference"/>; it is
    /// passed to podman as an argument, never through a shell.
    /// </summary>
    public required string Container { get; init; }

    /// <summary>Gets the user omp runs as inside the container (a name or a numeric id), or <see langword="null"/> for the container's own default user.</summary>
    public string? RemoteUser { get; init; }

    /// <summary>Gets the absolute POSIX path of the workspace folder inside the container: the working directory of <c>omp acp</c>.</summary>
    public required string WorkspaceFolder { get; init; }

    /// <summary>Gets the path of the podman executable on this machine, or <see langword="null"/> for <c>podman</c> resolved from <c>PATH</c>.</summary>
    public string? PodmanPath { get; init; }

    /// <summary>
    /// Gets the omp executable inside the container: an absolute path or a bare command name. <see langword="null"/> means <c>omp</c> resolved from the
    /// container's <c>PATH</c> (extended by <c>~/.bun/bin</c>, <c>~/.local/bin</c> and <c>/usr/local/bin</c>).
    /// </summary>
    public string? OmpPath { get; init; }
}
