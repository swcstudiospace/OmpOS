namespace OmpOS.Harness.Registry;

/// <summary>
/// The shape of an export file. <see cref="Checksum"/> is the lowercase hexadecimal SHA-256 of the canonical JSON of
/// <see cref="Entries"/>. The generation is recorded but not covered by the checksum.
/// </summary>
/// <param name="FormatVersion">The export format version.</param>
/// <param name="Generation">The generation the entries were captured at.</param>
/// <param name="Entries">The entries, ordered by identifier.</param>
/// <param name="Checksum">The checksum of the entries.</param>
internal sealed record RegistryExportDocument(int FormatVersion, long Generation, RegistryEntry[] Entries, string Checksum);
