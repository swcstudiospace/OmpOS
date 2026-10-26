using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text.Json;

namespace OmpOS.Harness.Registry;

/// <summary>
/// Serializes a snapshot to the export format and parses an export back into a snapshot, verifying its checksum. Both the
/// writer and the reader use this type, so the checksum is computed the same way on each side.
/// </summary>
internal static class RegistryExport
{
    private const int CurrentFormatVersion = 1;

    /// <summary>
    /// Serializes a snapshot to export bytes.
    /// </summary>
    /// <param name="snapshot">The snapshot. Its entries must be valid and ordered by identifier.</param>
    /// <returns>The UTF-8 export document.</returns>
    public static byte[] Serialize(RegistrySnapshot snapshot)
    {
        var entries = snapshot.Entries.ToArray();
        var document = new RegistryExportDocument(CurrentFormatVersion, snapshot.Generation, entries, ComputeChecksum(entries));
        return JsonSerializer.SerializeToUtf8Bytes(document, RegistryJsonContext.Default.RegistryExportDocument);
    }

    /// <summary>
    /// Parses export bytes. Malformed or checksum-mismatched input is refused rather than thrown.
    /// </summary>
    /// <param name="bytes">The export bytes.</param>
    /// <param name="snapshot">The accepted snapshot, when the method returns true.</param>
    /// <param name="rejection">Why the export was refused, when the method returns false.</param>
    /// <returns>True when the export is accepted.</returns>
    public static bool TryParse(byte[] bytes, [NotNullWhen(true)] out RegistrySnapshot? snapshot, out RegistryRejectionKind rejection)
    {
        snapshot = null;
        rejection = RegistryRejectionKind.Malformed;

        RegistryExportDocument? document;
        try
        {
            document = JsonSerializer.Deserialize(bytes, RegistryJsonContext.Default.RegistryExportDocument);
        }
        catch (JsonException)
        {
            return false;
        }

        if (document is null
            || document.FormatVersion != CurrentFormatVersion
            || document.Generation < 0
            || document.Entries is null
            || document.Checksum is null
            || !AreWellFormed(document.Entries))
        {
            return false;
        }

        if (!string.Equals(ComputeChecksum(document.Entries), document.Checksum, StringComparison.Ordinal))
        {
            rejection = RegistryRejectionKind.ChecksumMismatch;
            return false;
        }

        snapshot = new RegistrySnapshot(document.Generation, [.. document.Entries]);
        return true;
    }

    /// <summary>
    /// Computes the checksum over the canonical JSON of the entries.
    /// </summary>
    /// <param name="entries">The entries, ordered by identifier.</param>
    /// <returns>The lowercase hexadecimal SHA-256.</returns>
    public static string ComputeChecksum(RegistryEntry[] entries)
    {
        var canonical = JsonSerializer.SerializeToUtf8Bytes(entries, RegistryJsonContext.Default.RegistryEntryArray);
        return Convert.ToHexStringLower(SHA256.HashData(canonical));
    }

    private static bool AreWellFormed(RegistryEntry[] entries)
    {
        for (var index = 0; index < entries.Length; index++)
        {
            var entry = entries[index];
            if (entry is null || string.IsNullOrWhiteSpace(entry.Id) || entry.Name is null)
            {
                return false;
            }

            if (index > 0 && string.CompareOrdinal(entries[index - 1].Id, entry.Id) >= 0)
            {
                return false;
            }
        }

        return true;
    }
}
