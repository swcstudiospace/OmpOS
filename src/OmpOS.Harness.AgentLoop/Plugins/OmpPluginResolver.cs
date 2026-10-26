using System.Globalization;
using System.Text.Json;

namespace OmpOS.Harness.AgentLoop.Plugins;

/// <summary>
/// Resolves a plugin descriptor against the plugins root. The resolver reads directories, links and one small
/// <c>package.json</c>, and runs nothing. It is the guard that omp lacks: omp ignores a nonexistent <c>-e</c> path without
/// a word, so every path given to it is checked here first.
/// </summary>
public static class OmpPluginResolver
{
    private const int MaxManifestBytes = 1024 * 1024;
    private const int MaxJsonDepth = 16;
    private const int MaxLinkDepth = 32;

    /// <summary>
    /// Resolves <paramref name="descriptor"/> under <paramref name="pluginsRoot"/>.
    /// </summary>
    /// <param name="pluginsRoot">The plugins root, usually the <c>plugins</c> directory of the repository.</param>
    /// <param name="descriptor">The plugin.</param>
    /// <returns>
    /// The result. It is never null and the method does not throw for a missing or malformed plugin: the
    /// <see cref="ResolvedPlugin.Health"/> and <see cref="ResolvedPlugin.Message"/> say what is wrong.
    /// </returns>
    /// <exception cref="ArgumentException"><paramref name="pluginsRoot"/> is blank.</exception>
    public static ResolvedPlugin Resolve(string pluginsRoot, OmpPluginDescriptor descriptor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginsRoot);
        ArgumentNullException.ThrowIfNull(descriptor);

        var root = Path.GetFullPath(pluginsRoot);
        var directory = PluginDirectory(root, descriptor);
        try
        {
            return ResolveCore(root, directory, descriptor);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Create(descriptor, directory, [], PluginHealth.EntryMissing, $"{descriptor.DisplayName} could not be read: {exception.Message}");
        }
    }

    /// <summary>
    /// Returns the absolute directory of a plugin under the plugins root. The method does path math only.
    /// </summary>
    /// <param name="pluginsRoot">The plugins root.</param>
    /// <param name="descriptor">The plugin.</param>
    internal static string PluginDirectory(string pluginsRoot, OmpPluginDescriptor descriptor) =>
        Path.GetFullPath(Path.Combine(pluginsRoot, ToNativeSeparators(descriptor.RelativeDirectory)));

    private static ResolvedPlugin ResolveCore(string root, string directory, OmpPluginDescriptor descriptor)
    {
        if (descriptor.SubmoduleName is { } submodule)
        {
            var submodulePath = Path.GetFullPath(Path.Combine(root, submodule));
            if (!Directory.Exists(submodulePath) || !Directory.EnumerateFileSystemEntries(submodulePath).Any())
            {
                return Create(
                    descriptor,
                    directory,
                    [],
                    PluginHealth.SubmoduleNotInitialized,
                    SubmoduleNotInitializedException.FormatMessage(descriptor.Id, submodule, submodulePath));
            }
        }

        if (!Directory.Exists(directory))
        {
            return Missing(descriptor, directory, $"the plugin directory '{directory}' does not exist");
        }

        if (!Directory.EnumerateFileSystemEntries(directory).Any())
        {
            return Missing(descriptor, directory, $"the plugin directory '{directory}' is empty");
        }

        var realDirectory = RealPath(directory);
        return descriptor.EntryRelativePaths.Count > 0
            ? ResolveFiles(descriptor, directory, realDirectory)
            : ResolvePackage(descriptor, directory, realDirectory);
    }

    private static ResolvedPlugin ResolveFiles(OmpPluginDescriptor descriptor, string directory, string realDirectory)
    {
        var paths = new List<string>(descriptor.EntryRelativePaths.Count);
        foreach (var entry in descriptor.EntryRelativePaths)
        {
            if (!TryLocate(directory, realDirectory, entry, out var path, out var problem))
            {
                return Create(descriptor, directory, [], PluginHealth.ManifestInvalid, $"{descriptor.DisplayName}: {problem}");
            }

            paths.Add(path);
        }

        foreach (var path in paths)
        {
            if (!File.Exists(path))
            {
                return Missing(descriptor, directory, $"the entry '{path}' does not exist");
            }
        }

        return Create(descriptor, directory, paths, PluginHealth.Ready, $"{descriptor.DisplayName} is ready.");
    }

    private static ResolvedPlugin ResolvePackage(OmpPluginDescriptor descriptor, string directory, string realDirectory)
    {
        var manifestPath = Path.Combine(directory, "package.json");
        if (!File.Exists(manifestPath))
        {
            return Missing(descriptor, directory, $"'{manifestPath}' does not exist");
        }

        if (!TryReadExtensions(manifestPath, out var extensions, out var problem))
        {
            return Create(descriptor, directory, [], PluginHealth.ManifestInvalid, $"{descriptor.DisplayName}: {problem}");
        }

        var declared = new List<string>(extensions.Count);
        foreach (var extension in extensions)
        {
            if (!TryLocate(directory, realDirectory, extension, out var path, out problem))
            {
                return Create(descriptor, directory, [], PluginHealth.ManifestInvalid, $"{descriptor.DisplayName}: {problem}");
            }

            declared.Add(path);
        }

        foreach (var path in declared)
        {
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                return Missing(descriptor, directory, $"the extension '{path}' declared by package.json does not exist");
            }
        }

        return Create(descriptor, directory, [directory], PluginHealth.Ready, $"{descriptor.DisplayName} is ready.");
    }

    private static bool TryReadExtensions(string manifestPath, out List<string> extensions, out string problem)
    {
        extensions = [];
        problem = string.Empty;
        byte[] bytes;
        using (var stream = new FileStream(manifestPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            if (stream.Length > MaxManifestBytes)
            {
                problem = $"package.json is larger than {MaxManifestBytes} bytes.";
                return false;
            }

            bytes = new byte[(int)stream.Length];
            stream.ReadExactly(bytes);
        }

        try
        {
            using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = MaxJsonDepth });
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                problem = "package.json is not a JSON object.";
                return false;
            }

            foreach (var key in new[] { "omp", "pi" })
            {
                if (document.RootElement.TryGetProperty(key, out var section)
                    && section.ValueKind == JsonValueKind.Object
                    && section.TryGetProperty("extensions", out var list))
                {
                    return TryReadStrings(list, key, out extensions, out problem);
                }
            }

            problem = "package.json declares no 'omp.extensions' or 'pi.extensions'.";
            return false;
        }
        catch (JsonException exception)
        {
            problem = string.Create(CultureInfo.InvariantCulture, $"package.json is not valid JSON: {exception.Message}");
            return false;
        }
    }

    private static bool TryReadStrings(JsonElement list, string key, out List<string> extensions, out string problem)
    {
        extensions = [];
        problem = string.Empty;
        if (list.ValueKind != JsonValueKind.Array || list.GetArrayLength() == 0)
        {
            problem = $"'{key}.extensions' must be a non-empty array of paths.";
            return false;
        }

        foreach (var item in list.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetString()))
            {
                problem = $"'{key}.extensions' must hold only non-empty strings.";
                return false;
            }

            extensions.Add(item.GetString()!);
        }

        return true;
    }

    /// <summary>
    /// Joins a relative entry to the plugin directory and checks, with every link resolved, that the result stays inside the
    /// plugin directory. A rooted entry, or one that climbs out through <c>..</c> or a link, is rejected.
    /// </summary>
    private static bool TryLocate(string directory, string realDirectory, string entry, out string path, out string problem)
    {
        path = string.Empty;
        problem = string.Empty;
        var native = ToNativeSeparators(entry);
        if (Path.IsPathRooted(native))
        {
            problem = $"the entry '{entry}' is an absolute path; entries must be relative to the plugin directory.";
            return false;
        }

        var candidate = Path.GetFullPath(Path.Combine(directory, native));
        string real;
        try
        {
            real = RealPath(candidate);
        }
        catch (IOException)
        {
            problem = $"the entry '{entry}' cannot be resolved (too many links).";
            return false;
        }

        if (!IsInside(realDirectory, real))
        {
            problem = $"the entry '{entry}' leaves the plugin directory.";
            return false;
        }

        path = candidate;
        return true;
    }

    private static bool IsInside(string directory, string path)
    {
        var relative = Path.GetRelativePath(directory, path);
        return !relative.Equals("..", StringComparison.Ordinal)
            && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            && !Path.IsPathRooted(relative);
    }

    /// <summary>
    /// Returns <paramref name="fullPath"/> with every link in it replaced by its final target. Segments that do not exist are
    /// kept as written, so the result is defined for a path that is not there yet.
    /// </summary>
    private static string RealPath(string fullPath, int depth = 0)
    {
        if (depth > MaxLinkDepth)
        {
            throw new IOException("Too many levels of symbolic links.");
        }

        var root = Path.GetPathRoot(fullPath) ?? string.Empty;
        var current = root;
        var remainder = fullPath[root.Length..];
        foreach (var segment in remainder.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(current, segment);
            FileSystemInfo info = Directory.Exists(candidate) ? new DirectoryInfo(candidate) : new FileInfo(candidate);
            var target = info.LinkTarget is null ? null : info.ResolveLinkTarget(returnFinalTarget: true);
            current = target is null ? candidate : RealPath(Path.GetFullPath(target.FullName), depth + 1);
        }

        return current;
    }

    private static string ToNativeSeparators(string path) => path.Replace('/', Path.DirectorySeparatorChar);

    private static ResolvedPlugin Missing(OmpPluginDescriptor descriptor, string directory, string detail)
    {
        var fix = descriptor.OptionalFixCommand is { } command ? $" Run: {command}" : string.Empty;
        return Create(descriptor, directory, [], PluginHealth.EntryMissing, $"{descriptor.DisplayName} is not available: {detail}.{fix}");
    }

    private static ResolvedPlugin Create(
        OmpPluginDescriptor descriptor,
        string directory,
        IReadOnlyList<string> entries,
        PluginHealth health,
        string message) =>
        new()
        {
            Descriptor = descriptor,
            AbsoluteDirectory = directory,
            EntryPaths = entries,
            Health = health,
            Message = message,
        };
}
