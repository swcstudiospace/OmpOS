namespace OmpOS.Harness.Remote.Containers;

/// <summary>
/// What <c>.devcontainer/devcontainer.json</c> says that OmpOS uses to attach: the name, the workspace folder inside the container, the user, and how the
/// container is made. Nothing from <c>containerEnv</c>, <c>remoteEnv</c> or <c>secrets</c> is read.
/// </summary>
/// <param name="ConfigPath">The absolute path of the definition file on this machine.</param>
/// <param name="Name">The <c>name</c> property, or <see langword="null"/>.</param>
/// <param name="WorkspaceFolder">The workspace folder inside the container: the <c>workspaceFolder</c> property with <c>${localWorkspaceFolderBasename}</c> and <c>${localWorkspaceFolder}</c> filled in, or <c>/workspaces/&lt;folder name&gt;</c> (the devcontainer default) when the property is absent. <see langword="null"/> when the property uses a variable OmpOS does not resolve.</param>
/// <param name="RemoteUser">The <c>remoteUser</c> property, or <see langword="null"/>.</param>
/// <param name="ContainerUser">The <c>containerUser</c> property, or <see langword="null"/>.</param>
/// <param name="BuildKind">How the container is made.</param>
/// <param name="Image">The <c>image</c> property, or <see langword="null"/>.</param>
public sealed record DevContainerDefinition(
    string ConfigPath,
    string? Name,
    string? WorkspaceFolder,
    string? RemoteUser,
    string? ContainerUser,
    DevContainerBuildKind BuildKind,
    string? Image)
{
    /// <summary>Gets the user omp should run as: <see cref="RemoteUser"/>, else <see cref="ContainerUser"/>, else <see langword="null"/> (the container's default user).</summary>
    public string? EffectiveUser => RemoteUser ?? ContainerUser;
}
