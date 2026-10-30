namespace Autonogrammer.Contracts.Environments;

/// <summary>
/// One place a session can run: this machine, an ssh host, a podman container, a WSL distribution or a cloud worker. Stored in the user's settings. It holds references and
/// never a secret (see <see cref="SshProfile"/>).
/// </summary>
public sealed record EnvironmentProfile
{
    /// <summary>Gets the stable identifier of the profile. It matches <see cref="SshProfileRules.IsValidProfileId"/>.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the name shown to the user.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Gets where the profile runs sessions.</summary>
    public required EnvironmentKind Kind { get; init; }

    /// <summary>Gets the ssh details. Required when <see cref="Kind"/> is <see cref="EnvironmentKind.Ssh"/> and absent otherwise.</summary>
    public SshProfile? Ssh { get; init; }

    /// <summary>Gets the podman container details. Required when <see cref="Kind"/> is <see cref="EnvironmentKind.Container"/> and absent otherwise.</summary>
    public ContainerProfile? Container { get; init; }

    /// <summary>Gets the WSL distribution details. Required when <see cref="Kind"/> is <see cref="EnvironmentKind.Wsl"/> and absent otherwise.</summary>
    public WslProfile? Wsl { get; init; }
}
