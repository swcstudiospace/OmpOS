using System.Formats.Tar;
using System.IO.Compression;

namespace Autonogrammer.Harness.OmpLauncher;

/// <summary>
/// Extracts an npm package tarball into a staging directory. Every header is checked before the first byte is written, so a
/// refused archive leaves the staging directory empty. Only regular files and directories are allowed; links, devices and
/// absolute or escaping paths are refused. The leading <c>package/</c> folder is stripped.
/// </summary>
internal static class OmpTarballExtractor
{
    private const string PackagePrefix = "package/";

    /// <summary>Extracts <paramref name="archivePath"/> into <paramref name="destinationDirectory"/>.</summary>
    /// <param name="archivePath">A gzip-compressed tar file that has already been verified.</param>
    /// <param name="destinationDirectory">An existing, empty directory.</param>
    /// <exception cref="OmpArchiveRejectedException">An entry is not allowed. Nothing was written.</exception>
    /// <exception cref="InvalidDataException">The archive is not a readable tar file.</exception>
    public static void Extract(string archivePath, string destinationDirectory)
    {
        var destination = Path.GetFullPath(destinationDirectory);
        foreach (var entry in ReadEntries(archivePath))
        {
            _ = ResolveTarget(entry, destination);
        }

        foreach (var entry in ReadEntries(archivePath))
        {
            var target = ResolveTarget(entry, destination);
            if (target is null)
            {
                continue;
            }

            if (entry.EntryType == TarEntryType.Directory || entry.Name.EndsWith('/'))
            {
                Directory.CreateDirectory(target);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(target) ?? destination);
            entry.ExtractToFile(target, overwrite: true);
        }
    }

    private static IEnumerable<TarEntry> ReadEntries(string archivePath)
    {
        using var file = File.OpenRead(archivePath);
        using var gzip = new GZipStream(file, CompressionMode.Decompress);
        using var reader = new TarReader(gzip);
        while (reader.GetNextEntry() is { } entry)
        {
            yield return entry;
        }
    }

    private static string? ResolveTarget(TarEntry entry, string destination)
    {
        switch (entry.EntryType)
        {
            case TarEntryType.GlobalExtendedAttributes:
                return null;
            case TarEntryType.RegularFile:
            case TarEntryType.V7RegularFile:
            case TarEntryType.Directory:
                break;
            case TarEntryType.SymbolicLink:
            case TarEntryType.HardLink:
                throw Reject("links are not allowed: " + entry.Name);
            default:
                throw Reject("the entry type is not allowed: " + entry.Name);
        }

        var name = entry.Name.Replace('\\', '/');
        if (name.Length == 0 || name[0] == '/' || Path.IsPathRooted(name) || name.Contains(':', StringComparison.Ordinal))
        {
            throw Reject("the path is absolute: " + entry.Name);
        }

        var relative = name.TrimEnd('/');
        if (string.Equals(relative, "package", StringComparison.Ordinal))
        {
            return null;
        }

        if (!relative.StartsWith(PackagePrefix, StringComparison.Ordinal))
        {
            throw Reject("the path is outside package/: " + entry.Name);
        }

        relative = relative[PackagePrefix.Length..];
        foreach (var segment in relative.Split('/'))
        {
            if (segment.Length == 0 || string.Equals(segment, ".", StringComparison.Ordinal) || string.Equals(segment, "..", StringComparison.Ordinal))
            {
                throw Reject("the path has an unsafe segment: " + entry.Name);
            }
        }

        var target = Path.GetFullPath(Path.Combine(destination, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!target.StartsWith(destination + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw Reject("the path leaves the staging directory: " + entry.Name);
        }

        return target;
    }

    private static OmpArchiveRejectedException Reject(string reason) => new(reason);
}
