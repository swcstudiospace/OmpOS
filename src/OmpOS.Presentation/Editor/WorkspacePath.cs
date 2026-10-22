namespace OmpOS.Presentation.Editor;

/// <summary>
/// The rules for workspace-relative paths (ADE-10). A workspace path is relative to the workspace root, uses <c>/</c>
/// separators, and is normal: no empty, <c>.</c> or <c>..</c> segments. The empty path names the root itself.
/// </summary>
public static class WorkspacePath
{
    /// <summary>Normalises a relative path to its canonical form, resolving <c>.</c> and <c>..</c> segments.</summary>
    /// <param name="relativePath">The path to normalise. Both <c>/</c> and <c>\</c> separate segments.</param>
    /// <returns>The canonical path with <c>/</c> separators. It is empty for the root.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="relativePath"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// The path is rooted (for example <c>/etc</c> or a drive letter), contains a NUL character, or climbs above the root with <c>..</c>.
    /// </exception>
    public static string Normalize(string relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);

        if (relativePath.Contains('\0', StringComparison.Ordinal))
        {
            throw new ArgumentException("A workspace path cannot contain a NUL character.", nameof(relativePath));
        }

        if (relativePath.StartsWith('/') || relativePath.StartsWith('\\') || Path.IsPathRooted(relativePath))
        {
            throw new ArgumentException("A workspace path must be relative to the workspace root.", nameof(relativePath));
        }

        var segments = new List<string>();
        foreach (var segment in relativePath.Split(['/', '\\']))
        {
            if (string.IsNullOrEmpty(segment) || string.Equals(segment, ".", StringComparison.Ordinal))
            {
                continue;
            }

            if (!string.Equals(segment, "..", StringComparison.Ordinal))
            {
                segments.Add(segment);
                continue;
            }

            if (segments.Count == 0)
            {
                throw new ArgumentException("The path leaves the workspace root.", nameof(relativePath));
            }

            segments.RemoveAt(segments.Count - 1);
        }

        return string.Join('/', segments);
    }

    /// <summary>Joins a normalised parent path and a child name.</summary>
    /// <param name="parent">The normalised parent path. Empty for the root.</param>
    /// <param name="name">The child name, which must be a plain name.</param>
    /// <returns>The child's workspace path.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="parent"/> or <paramref name="name"/> is null.</exception>
    public static string Combine(string parent, string name)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(name);

        return parent.Length == 0 ? name : parent + "/" + name;
    }

    /// <summary>Tells whether a name is a single path segment: not empty, not <c>.</c> or <c>..</c>, and without separators.</summary>
    /// <param name="name">The name to check.</param>
    /// <returns>True when the name is a plain segment.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    public static bool IsPlainName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        return name.Length > 0
            && !string.Equals(name, ".", StringComparison.Ordinal)
            && !string.Equals(name, "..", StringComparison.Ordinal)
            && name.IndexOfAny(['/', '\\', '\0']) < 0;
    }
}
