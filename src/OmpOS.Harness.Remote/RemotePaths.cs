namespace OmpOS.Harness.Remote;

/// <summary>Path arithmetic for POSIX paths on a remote machine. It is pure text: it never touches a disk.</summary>
internal static class RemotePaths
{
    /// <summary>Removes the trailing slashes of an absolute path; the root <c>/</c> stays.</summary>
    /// <param name="path">The absolute path.</param>
    /// <returns>The path without a trailing slash.</returns>
    public static string TrimTrailingSlash(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var trimmed = path.TrimEnd('/');
        return trimmed.Length == 0 && path.Length > 0 ? "/" : trimmed;
    }

    /// <summary>Joins an absolute root and a normalised relative path.</summary>
    /// <param name="root">The absolute root without a trailing slash.</param>
    /// <param name="relative">The relative path; empty for the root itself.</param>
    /// <returns>The absolute path.</returns>
    public static string Join(string root, string relative)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(relative);
        if (relative.Length == 0)
        {
            return root;
        }

        return string.Equals(root, "/", StringComparison.Ordinal) ? "/" + relative : root + "/" + relative;
    }

    /// <summary>Normalises an absolute POSIX path lexically: repeated slashes and <c>.</c> segments go, a trailing slash goes.</summary>
    /// <param name="path">The candidate.</param>
    /// <param name="normalized">The normalised path.</param>
    /// <returns><see langword="false"/> when the path is not absolute, holds a control character or a <c>..</c> segment.</returns>
    public static bool TryNormalize(string path, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrEmpty(path) || path[0] != '/' || path.Any(char.IsControl))
        {
            return false;
        }

        var segments = new List<string>();
        foreach (var segment in path.Split('/'))
        {
            if (segment.Length == 0 || string.Equals(segment, ".", StringComparison.Ordinal))
            {
                continue;
            }

            if (string.Equals(segment, "..", StringComparison.Ordinal))
            {
                return false;
            }

            segments.Add(segment);
        }

        normalized = "/" + string.Join('/', segments);
        return true;
    }

    /// <summary>Finds the longest of the roots that holds a normalised path.</summary>
    /// <param name="path">The normalised absolute path.</param>
    /// <param name="roots">The normalised absolute roots.</param>
    /// <returns>The root that holds the path, or <see langword="null"/>.</returns>
    public static string? FindContainingRoot(string path, IReadOnlyList<string> roots)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(roots);
        string? best = null;
        foreach (var root in roots)
        {
            var holds = string.Equals(path, root, StringComparison.Ordinal)
                || string.Equals(root, "/", StringComparison.Ordinal)
                || path.StartsWith(root + "/", StringComparison.Ordinal);
            if (holds && (best is null || root.Length > best.Length))
            {
                best = root;
            }
        }

        return best;
    }
}
