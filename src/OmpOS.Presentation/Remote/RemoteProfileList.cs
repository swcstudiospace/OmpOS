namespace OmpOS.Presentation.Remote;

/// <summary>The saved environments and what could not be read.</summary>
/// <param name="Profiles">The saved environments, in the order they were saved.</param>
/// <param name="Warning">One plain sentence about a damaged file or ignored profiles, or <see langword="null"/>.</param>
public sealed record RemoteProfileList(IReadOnlyList<RemoteStoredProfile> Profiles, string? Warning);
