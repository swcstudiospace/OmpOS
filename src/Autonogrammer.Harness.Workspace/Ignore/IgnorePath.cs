namespace Autonogrammer.Workspace.Ignore;

/// <summary>Turns a caller-supplied relative path into safe segments.</summary>
internal static class IgnorePath
{
    /// <summary>
    /// Splits a workspace-relative path into segments. <c>\</c> counts as a separator, empty and <c>.</c> segments are dropped, and the empty path is the workspace root (no segments).
    /// </summary>
    /// <param name="path">The path to split.</param>
    /// <param name="maxDepth">The most segments allowed.</param>
    /// <param name="segments">The segments, or an empty array when the path is unsafe.</param>
    /// <returns><see langword="false"/> for a null path, a rooted path (<c>/x</c>, <c>\x</c>, <c>C:x</c>), a path with a <c>..</c> segment or a NUL character, or a path deeper than <paramref name="maxDepth"/>.</returns>
    public static bool TryNormalize(string? path, int maxDepth, out string[] segments)
    {
        segments = [];
        if (path is null)
        {
            return false;
        }

        if (path.Length == 0)
        {
            return true;
        }

        if (path[0] is '/' or '\\' || (path.Length >= 2 && path[1] == ':' && char.IsAsciiLetter(path[0])) || path.Contains('\0', StringComparison.Ordinal))
        {
            return false;
        }

        var parts = path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var kept = new List<string>(parts.Length);
        foreach (var part in parts)
        {
            if (string.Equals(part, "..", StringComparison.Ordinal))
            {
                return false;
            }

            if (!string.Equals(part, ".", StringComparison.Ordinal))
            {
                kept.Add(part);
            }
        }

        if (kept.Count > maxDepth)
        {
            return false;
        }

        segments = [.. kept];
        return true;
    }
}
