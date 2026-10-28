namespace Autonogrammer.Harness.Registry;

/// <summary>
/// Reads an export without changing it. The export is verified before it is returned: a malformed or checksum-mismatched
/// export is reported as rejected, and an export whose generation differs from the caller's expected generation is reported
/// as drift together with the export's own snapshot.
/// </summary>
public static class RegistryReader
{
    /// <summary>
    /// Reads and verifies the export at <paramref name="path"/>.
    /// </summary>
    /// <param name="path">The export file.</param>
    /// <param name="expectedGeneration">The generation the caller last observed.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The read result. I/O failures are thrown; content failures are reported in the result.</returns>
    public static async Task<RegistryReadResult> ReadAsync(string path, long expectedGeneration, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentOutOfRangeException.ThrowIfNegative(expectedGeneration);

        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            return new RegistryReadResult(RegistryReadStatus.Missing, null, null);
        }

        var bytes = await File.ReadAllBytesAsync(fullPath, cancellationToken).ConfigureAwait(false);
        if (!RegistryExport.TryParse(bytes, out var snapshot, out var rejection))
        {
            return new RegistryReadResult(RegistryReadStatus.Rejected, null, rejection);
        }

        var status = snapshot.Generation == expectedGeneration ? RegistryReadStatus.Current : RegistryReadStatus.Drift;
        return new RegistryReadResult(status, snapshot, null);
    }
}
