using OmpOS.Contracts.Environments;

namespace OmpOS.Presentation.Remote;

/// <summary>The choice the user made in the New session dialog: where the agent runs.</summary>
/// <param name="Kind">Local, an ssh host, a container, a WSL distribution or the cloud.</param>
/// <param name="Label">The name shown to the user.</param>
/// <param name="Profile">The environment of an ssh, container or WSL target, or <see langword="null"/> for the others.</param>
public sealed record SessionTarget(SessionTargetKind Kind, string Label, EnvironmentProfile? Profile)
{
    /// <summary>Gets the identifier of the environment of an ssh, container or WSL target, or <see langword="null"/>.</summary>
    public string? ProfileId => Profile?.Id;

    /// <summary>
    /// Gets the working directory of the agent on the remote machine (ssh), in the container or in the distribution, or <see langword="null"/>: a local session uses
    /// the folder the user opened.
    /// </summary>
    public string? RemoteWorkspaceRoot => Profile?.Ssh?.RemoteWorkspaceRoot ?? Profile?.Container?.WorkspaceFolder ?? Profile?.Wsl?.WorkspacePath;
}
