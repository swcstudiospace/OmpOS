namespace Autonogrammer.Workspace.Ignore;

/// <summary>The outcome of matching one rule against one path.</summary>
internal enum IgnoreMatchResult
{
    /// <summary>The rule does not match.</summary>
    NoMatch,

    /// <summary>The rule matches.</summary>
    Match,

    /// <summary>The rule spent its whole step budget, so the answer is unknown.</summary>
    CapExceeded,
}
