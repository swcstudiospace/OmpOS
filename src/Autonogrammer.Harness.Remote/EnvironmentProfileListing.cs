namespace Autonogrammer.Harness.Remote;

/// <summary>The stored environment profiles and what could not be read.</summary>
/// <param name="Profiles">The valid profiles, in the order they were saved.</param>
/// <param name="Warning">One plain sentence about a damaged file or ignored profiles, or <see langword="null"/> when everything was read.</param>
public sealed record EnvironmentProfileListing(IReadOnlyList<StoredEnvironmentProfile> Profiles, string? Warning);
