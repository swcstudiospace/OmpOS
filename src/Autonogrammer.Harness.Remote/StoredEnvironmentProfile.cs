using Autonogrammer.Contracts.Environments;

namespace Autonogrammer.Harness.Remote;

/// <summary>One stored profile with the time it was last used for a session.</summary>
/// <param name="Profile">The profile. It passed <see cref="EnvironmentProfileValidator"/> when it was read.</param>
/// <param name="LastUsedUtc">When a session last started on it, or <see langword="null"/> when none has.</param>
public sealed record StoredEnvironmentProfile(EnvironmentProfile Profile, DateTimeOffset? LastUsedUtc);
