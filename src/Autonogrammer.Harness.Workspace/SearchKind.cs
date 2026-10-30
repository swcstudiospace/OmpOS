namespace Autonogrammer.Workspace;

/// <summary>
/// How a search pattern is interpreted.
/// </summary>
public enum SearchKind
{
    /// <summary>The pattern is matched as a literal substring.</summary>
    Text,

    /// <summary>The pattern is a regular expression.</summary>
    Regex,
}
