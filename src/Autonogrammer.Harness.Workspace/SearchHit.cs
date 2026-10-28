namespace Autonogrammer.Workspace;

/// <summary>
/// One match of a search pattern.
/// </summary>
/// <param name="RelativePath">The file path relative to the workspace root, with '/' separators.</param>
/// <param name="LineNumber">The one-based line number of the match.</param>
/// <param name="MatchText">The matched text.</param>
public sealed record SearchHit(string RelativePath, int LineNumber, string MatchText);
