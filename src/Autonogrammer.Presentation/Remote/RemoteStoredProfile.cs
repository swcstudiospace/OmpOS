using Autonogrammer.Contracts.Environments;

namespace Autonogrammer.Presentation.Remote;

/// <summary>One saved environment as the host returns it.</summary>
/// <param name="Profile">The profile of any kind. The ssh page shows the profiles of kind <see cref="EnvironmentKind.Ssh"/>; the container and WSL section shows <see cref="EnvironmentKind.Container"/> and <see cref="EnvironmentKind.Wsl"/>.</param>
/// <param name="LastUsedUtc">When a session last started on it, or <see langword="null"/>.</param>
public sealed record RemoteStoredProfile(EnvironmentProfile Profile, DateTimeOffset? LastUsedUtc);
