namespace Autonogrammer.Harness.Registry;

/// <summary>
/// One registered entry: a stable identifier, a display name, and whether the entry is enabled.
/// </summary>
/// <param name="Id">The identifier. Unique within a registry and compared ordinally.</param>
/// <param name="Name">The display name.</param>
/// <param name="Enabled">Whether the entry is enabled.</param>
public sealed record RegistryEntry(string Id, string Name, bool Enabled);
