namespace OmpOS.Workspace;

/// <summary>
/// A search over the workspace.
/// </summary>
/// <param name="Pattern">The literal text or regular expression. It must not be empty.</param>
/// <param name="Kind">How the pattern is interpreted.</param>
/// <param name="IgnoreCase">Whether matching ignores case.</param>
/// <param name="MaxHits">The most hits returned. Further hits set <see cref="SearchResult.Truncated"/>.</param>
public sealed record SearchRequest(
    string Pattern,
    SearchKind Kind,
    bool IgnoreCase = false,
    int MaxHits = WorkspaceSearch.DefaultMaxHits);
