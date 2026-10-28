namespace OmpOS.Presentation.Settings.Ignore;

/// <summary>
/// What the Ignore page edits: the global ignore patterns, whether the built-in list applies, and whether ignore files below the
/// workspace root are read as well. Two values are equal when their lists hold the same patterns in the same order.
/// </summary>
/// <param name="GlobalPatterns">The patterns of the global ignore list, in gitignore syntax.</param>
/// <param name="UseDefaultList">True when the built-in list of ignored files applies.</param>
/// <param name="HierarchicalIgnoreFiles">True when ignore files in folders below the root are read as well.</param>
public sealed record IgnoreSettingsValues(IReadOnlyList<string> GlobalPatterns, bool UseDefaultList, bool HierarchicalIgnoreFiles)
{
    /// <summary>Gets the defaults: no global patterns, the built-in list on, folders below the root not read.</summary>
    public static IgnoreSettingsValues Defaults { get; } = new([], UseDefaultList: true, HierarchicalIgnoreFiles: false);

    /// <inheritdoc/>
    public bool Equals(IgnoreSettingsValues? other) =>
        other is not null
        && UseDefaultList == other.UseDefaultList
        && HierarchicalIgnoreFiles == other.HierarchicalIgnoreFiles
        && GlobalPatterns.SequenceEqual(other.GlobalPatterns, StringComparer.Ordinal);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(UseDefaultList, HierarchicalIgnoreFiles, GlobalPatterns.Count);
}
