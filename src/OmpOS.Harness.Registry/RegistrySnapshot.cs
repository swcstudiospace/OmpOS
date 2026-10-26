using System.Collections.Immutable;

namespace OmpOS.Harness.Registry;

/// <summary>
/// An immutable view of a registry at one generation.
/// </summary>
/// <param name="Generation">The number of successful mutations applied to the registry. An empty registry is generation 0.</param>
/// <param name="Entries">The entries, ordered by identifier using ordinal comparison.</param>
public sealed record RegistrySnapshot(long Generation, ImmutableArray<RegistryEntry> Entries);
