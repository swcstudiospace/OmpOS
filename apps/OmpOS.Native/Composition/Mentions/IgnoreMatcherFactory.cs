using OmpOS.Workspace.Ignore;

namespace OmpOS.Native.Composition.Mentions;

/// <summary>Builds the ignore matcher a session's mentions share.</summary>
internal static class IgnoreMatcherFactory
{
    /// <summary>
    /// Creates a matcher that holds the built-in list and the user's global list but no ignore file yet. It reads nothing from the disk: <see cref="WorkspaceMentionFiles"/> loads the
    /// ignore files of a directory while it walks the tree, and must finish loading a directory before that directory's entries are queried (the matcher is not safe to load and query at
    /// once; <see cref="WorkspaceMentionFiles"/> and <see cref="IgnoreMatcherFilter"/> both lock it).
    /// </summary>
    /// <param name="globalPatterns">The user's global ignore list in gitignore syntax; empty for none.</param>
    /// <param name="useDefaultList">Whether the built-in list (<see cref="IgnoreDefaults"/>) applies.</param>
    /// <returns>The matcher.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="globalPatterns"/> is null.</exception>
    internal static IgnoreMatcher Create(IReadOnlyList<string> globalPatterns, bool useDefaultList = true)
    {
        ArgumentNullException.ThrowIfNull(globalPatterns);
        return IgnoreMatcher.Create(new IgnoreOptions { GlobalPatterns = globalPatterns, UseDefaultList = useDefaultList });
    }
}
