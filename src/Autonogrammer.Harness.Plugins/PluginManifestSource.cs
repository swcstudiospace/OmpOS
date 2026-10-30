namespace Autonogrammer.Harness.Plugins;

/// <summary>
/// One manifest offered to a host, with the location it came from for error reports.
/// </summary>
/// <param name="Location">Where the manifest came from, such as its file path.</param>
/// <param name="Content">The manifest bytes.</param>
public sealed record PluginManifestSource(string Location, ReadOnlyMemory<byte> Content)
{
    /// <summary>
    /// Reads a manifest file. The file's size is checked before any of it is read, so an oversized file is never loaded
    /// into memory.
    /// </summary>
    /// <param name="path">The manifest file.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The manifest source.</returns>
    /// <exception cref="PluginManifestException">The file is larger than <see cref="PluginManifestParser.MaxManifestBytes"/>.</exception>
    public static async Task<PluginManifestSource> ReadFileAsync(string path, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var length = new FileInfo(path).Length;
        if (length > PluginManifestParser.MaxManifestBytes)
        {
            throw new PluginManifestException(
                PluginRejectionKind.ManifestTooLarge,
                null,
                $"The manifest file is {length} bytes; the limit is {PluginManifestParser.MaxManifestBytes} bytes.");
        }

        var content = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        return new PluginManifestSource(path, content);
    }
}
