namespace OmpOS.Api.Services.WorkspaceSettings;

/// <summary>Keeps absolute paths of the API host out of a response: it shows paths relative to the workspace folder or the user home, and hides the rest.</summary>
internal static class PathRedactor
{
    private const string WorkspaceMarker = "<workspace>";
    private const string HomeMarker = "~";

    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    /// <summary>Replaces every occurrence of the workspace folder and of the home directory in a sentence.</summary>
    /// <param name="text">The sentence, for example a diagnostic reason that quotes an exception message.</param>
    /// <param name="root">The workspace folder, or null.</param>
    /// <param name="home">The home directory, or null.</param>
    /// <returns>The sentence with <c>&lt;workspace&gt;</c> for the folder and <c>~</c> for the home.</returns>
    public static string Redact(string text, string? root, string? home)
    {
        ArgumentNullException.ThrowIfNull(text);
        var replacements = new List<(string Path, string Marker)>();
        if (Trimmed(root) is { } workspace)
        {
            replacements.Add((workspace, WorkspaceMarker));
        }

        if (Trimmed(home) is { } user)
        {
            replacements.Add((user, HomeMarker));
        }

        // The longer path goes first: a workspace inside the home must not be cut short by the home replacement.
        var result = text;
        foreach (var (path, marker) in replacements.OrderByDescending(static item => item.Path.Length))
        {
            result = result.Replace(path, marker, PathComparison);
        }

        return result;
    }

    /// <summary>Shows an absolute path the way a client may see it: relative to the workspace folder, <c>~/</c> relative to the home, or only its file name.</summary>
    /// <param name="path">The absolute path.</param>
    /// <param name="root">The workspace folder, or null.</param>
    /// <param name="home">The home directory, or null.</param>
    /// <returns>The label. The workspace folder itself is <c>.</c> and the home itself is <c>~</c>.</returns>
    public static string Label(string path, string? root, string? home)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (TryRelative(path, root, out var inRoot))
        {
            return inRoot;
        }

        if (TryRelative(path, home, out var inHome))
        {
            return inHome is "." ? HomeMarker : HomeMarker + "/" + inHome;
        }

        return "<elsewhere>/" + Path.GetFileName(path);
    }

    private static string? Trimmed(string? directory) =>
        string.IsNullOrWhiteSpace(directory) ? null : Path.TrimEndingDirectorySeparator(directory);

    private static bool TryRelative(string path, string? directory, out string relative)
    {
        relative = string.Empty;
        var trimmed = Trimmed(directory);
        if (trimmed is null)
        {
            return false;
        }

        if (string.Equals(path, trimmed, PathComparison))
        {
            relative = ".";
            return true;
        }

        var prefix = trimmed + Path.DirectorySeparatorChar;
        if (!path.StartsWith(prefix, PathComparison))
        {
            return false;
        }

        relative = path[prefix.Length..].Replace('\\', '/');
        return true;
    }
}
