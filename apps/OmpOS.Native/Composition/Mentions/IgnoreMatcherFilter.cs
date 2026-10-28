using OmpOS.Presentation.Mentions;
using OmpOS.Workspace.Ignore;

namespace OmpOS.Native.Composition.Mentions;

/// <summary>
/// Adapts an <see cref="IgnoreMatcher"/> to the mention picker's <see cref="IMentionIgnoreFilter"/>. A query locks the matcher, because the tree walk of
/// <see cref="WorkspaceMentionFiles"/> adds ignore files to the same matcher while the picker may already be asking it questions.
/// </summary>
internal sealed class IgnoreMatcherFilter : IMentionIgnoreFilter
{
    private readonly IgnoreMatcher _matcher;

    /// <summary>Creates the filter.</summary>
    /// <param name="matcher">The matcher to ask, built by <see cref="IgnoreMatcherFactory.Create"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="matcher"/> is null.</exception>
    internal IgnoreMatcherFilter(IgnoreMatcher matcher)
    {
        ArgumentNullException.ThrowIfNull(matcher);
        _matcher = matcher;
    }

    /// <inheritdoc/>
    public bool IsIgnored(string relativePath, bool isDirectory)
    {
        lock (_matcher)
        {
            return _matcher.IsIgnored(relativePath, isDirectory);
        }
    }
}
