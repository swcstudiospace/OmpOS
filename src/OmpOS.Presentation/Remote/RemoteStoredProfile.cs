using OmpOS.Contracts.Environments;

namespace OmpOS.Presentation.Remote;

/// <summary>One saved environment as the host returns it.</summary>
/// <param name="Profile">The profile. Only profiles of kind <see cref="EnvironmentKind.Ssh"/> are shown on the remote page.</param>
/// <param name="LastUsedUtc">When a session last started on it, or <see langword="null"/>.</param>
public sealed record RemoteStoredProfile(EnvironmentProfile Profile, DateTimeOffset? LastUsedUtc);
