using System.IO.Compression;

namespace Autonogrammer.Harness.PluginBundles;

/// <summary>Refuses traversal, symbolic links, oversized files and a malformed manifest before any install is committed.</summary>
public static class BundleValidator
{
    /// <summary>The largest one file may be, in bytes.</summary>
    public const int MaxFileBytes = 2 * 1024 * 1024;

    /// <summary>The most files one bundle may hold.</summary>
    public const int MaxFileCount = 2000;

    /// <summary>The largest a bundle may be, in bytes.</summary>
    public const long MaxBundleBytes = 50L * 1024 * 1024;

    /// <summary>Checks a folder that is already on disk. Symbolic links are not followed.</summary>
    /// <param name="bundleDirectory">The folder.</param>
    /// <param name="reservedSkills">Built-in skill ids the bundle must not reuse.</param>
    /// <param name="reservedCommands">Built-in command ids the bundle must not reuse.</param>
    /// <returns>The id the folder should be installed as.</returns>
    /// <exception cref="BundleValidationException">The folder is hostile or malformed.</exception>
    public static string ValidateDirectory(
        string bundleDirectory,
        IReadOnlySet<string>? reservedSkills = null,
        IReadOnlySet<string>? reservedCommands = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bundleDirectory);
        var files = new List<string>();
        long total = 0;
        Walk(bundleDirectory, bundleDirectory, files, ref total);
        if (files.Count > MaxFileCount)
        {
            throw new BundleValidationException(BundleInvalidCode.BundleTooManyFiles);
        }

        if (total > MaxBundleBytes)
        {
            throw new BundleValidationException(BundleInvalidCode.BundleTooBig);
        }

        var manifest = BundleManifestReader.ReadOptional(bundleDirectory);
        var id = ResolveId(manifest, Path.GetFileName(bundleDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)));
        RejectCollisions(bundleDirectory, reservedSkills, reservedCommands);
        return id;
    }

    /// <summary>Checks a zip archive without extracting it.</summary>
    /// <param name="zipPath">The archive.</param>
    /// <param name="fallbackId">The id to use when the archive has no manifest name. Usually the file name without <c>.zip</c>.</param>
    /// <returns>The id the archive should be installed as.</returns>
    /// <exception cref="BundleValidationException">The archive is hostile or malformed.</exception>
    public static string ValidateZip(string zipPath, string fallbackId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(zipPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(fallbackId);
        using var archive = ZipFile.OpenRead(zipPath);
        var count = 0;
        long total = 0;
        string? manifestJson = null;
        foreach (var entry in archive.Entries)
        {
            if (IsDirectoryEntry(entry))
            {
                RejectPath(entry.FullName);
                continue;
            }

            count++;
            RejectPath(entry.FullName);
            if (IsSymlink(entry))
            {
                throw new BundleValidationException(BundleInvalidCode.BundleSymlink, Normalize(entry.FullName));
            }

            var length = entry.Length;
            if (length > MaxFileBytes)
            {
                throw new BundleValidationException(BundleInvalidCode.BundleTooLarge, Normalize(entry.FullName));
            }

            total += length;
            if (count > MaxFileCount)
            {
                throw new BundleValidationException(BundleInvalidCode.BundleTooManyFiles);
            }

            if (total > MaxBundleBytes)
            {
                throw new BundleValidationException(BundleInvalidCode.BundleTooBig);
            }

            if (string.Equals(Normalize(entry.FullName), BundleManifestReader.RelativePath, StringComparison.Ordinal))
            {
                using var stream = entry.Open();
                using var reader = new StreamReader(stream);
                manifestJson = reader.ReadToEnd();
            }
        }

        PluginBundleManifest? manifest = null;
        if (manifestJson is not null)
        {
            manifest = BundleManifestReader.ReadJson(manifestJson, BundleManifestReader.RelativePath);
        }

        return ResolveId(manifest, fallbackId);
    }

    /// <summary>Returns whether a bundle id is allowed: a letter or digit, then letters, digits and hyphens, at most 64 characters.</summary>
    /// <param name="id">The candidate id.</param>
    /// <returns><see langword="true"/> when the id may be installed.</returns>
    public static bool IsAllowedId(string id)
    {
        if (string.IsNullOrEmpty(id) || id.Length > 64 || !char.IsAsciiLetterOrDigit(id[0]))
        {
            return false;
        }

        foreach (var character in id)
        {
            if (!(char.IsAsciiLetterOrDigit(character) || character == '-'))
            {
                return false;
            }
        }

        return true;
    }

    internal static bool IsSymlink(ZipArchiveEntry entry)
    {
        var mode = (entry.ExternalAttributes >> 16) & 0xF000;
        return mode == 0xA000;
    }

    private static void Walk(string root, string current, List<string> files, ref long total)
    {
        foreach (var path in Directory.EnumerateFileSystemEntries(current))
        {
            var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
            RejectPath(relative);
            if (IsReparse(path))
            {
                throw new BundleValidationException(BundleInvalidCode.BundleSymlink, relative);
            }

            if (Directory.Exists(path))
            {
                Walk(root, path, files, ref total);
                continue;
            }

            var length = new FileInfo(path).Length;
            if (length > MaxFileBytes)
            {
                throw new BundleValidationException(BundleInvalidCode.BundleTooLarge, relative);
            }

            total += length;
            files.Add(relative);
            if (files.Count > MaxFileCount)
            {
                throw new BundleValidationException(BundleInvalidCode.BundleTooManyFiles);
            }

            if (total > MaxBundleBytes)
            {
                throw new BundleValidationException(BundleInvalidCode.BundleTooBig);
            }
        }
    }

    private static void RejectCollisions(string bundleDirectory, IReadOnlySet<string>? skills, IReadOnlySet<string>? commands)
    {
        var skillsDir = Path.Combine(bundleDirectory, "skills");
        if (skills is { Count: > 0 } && Directory.Exists(skillsDir))
        {
            foreach (var folder in Directory.EnumerateDirectories(skillsDir))
            {
                var name = Path.GetFileName(folder);
                if (skills.Contains(name))
                {
                    throw new BundleValidationException(BundleInvalidCode.BundleNameCollision, "skills/" + name);
                }
            }
        }

        var commandsDir = Path.Combine(bundleDirectory, "commands");
        if (commands is { Count: > 0 } && Directory.Exists(commandsDir))
        {
            foreach (var file in Directory.EnumerateFiles(commandsDir, "*.md"))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                if (commands.Contains(name))
                {
                    throw new BundleValidationException(BundleInvalidCode.BundleNameCollision, "commands/" + name + ".md");
                }
            }
        }
    }

    private static string ResolveId(PluginBundleManifest? manifest, string fallback)
    {
        var id = string.IsNullOrWhiteSpace(manifest?.Name) ? fallback : manifest!.Name!;
        if (!IsAllowedId(id))
        {
            throw new BundleValidationException(BundleInvalidCode.BundleBadName, id);
        }

        return id;
    }

    private static void RejectPath(string entryName)
    {
        var normalized = Normalize(entryName);
        if (normalized.Length == 0 || normalized.StartsWith('/') || normalized.Contains(":/", StringComparison.Ordinal) || normalized.StartsWith('\\'))
        {
            throw new BundleValidationException(BundleInvalidCode.BundleTraversal, normalized);
        }

        foreach (var segment in normalized.Split('/'))
        {
            if (segment.Length == 0 || string.Equals(segment, ".", StringComparison.Ordinal) || string.Equals(segment, "..", StringComparison.Ordinal))
            {
                throw new BundleValidationException(BundleInvalidCode.BundleTraversal, normalized);
            }
        }
    }

    private static string Normalize(string entryName) => entryName.Replace('\\', '/').TrimEnd('/');

    private static bool IsDirectoryEntry(ZipArchiveEntry entry) =>
        entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\') || (entry.Length == 0 && string.IsNullOrEmpty(entry.Name));

    private static bool IsReparse(string path)
    {
        var attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.ReparsePoint) != FileAttributes.None)
        {
            return true;
        }

        return File.Exists(path) && new FileInfo(path).LinkTarget is not null;
    }
}
