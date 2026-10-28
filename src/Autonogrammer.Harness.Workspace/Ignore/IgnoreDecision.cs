namespace Autonogrammer.Workspace.Ignore;

/// <summary>The answer of <see cref="IgnoreMatcher.Explain"/>: whether a path is ignored and which rule decided it.</summary>
/// <param name="Ignored">Whether the path is hidden from the agent, from Tab and from mentions.</param>
/// <param name="Rule">The rule line that decided, as written (a re-including rule starts with <c>!</c>); for a path under an ignored folder, the rule that ignored the folder. <see langword="null"/> when no rule matched.</param>
/// <param name="Source">The ignore file path (or list name) that holds <paramref name="Rule"/>, or <see langword="null"/>.</param>
/// <param name="Line">The 1-based line of <paramref name="Rule"/> in <paramref name="Source"/>, or 0.</param>
public readonly record struct IgnoreDecision(bool Ignored, string? Rule, string? Source, int Line)
{
    /// <summary>Gets the decision for a path that no rule matched.</summary>
    internal static IgnoreDecision NotIgnored { get; } = new(false, null, null, 0);

    /// <summary>Gets the decision for a path that is rooted, climbs out of the workspace with <c>..</c>, is too deep or holds a control character.</summary>
    internal static IgnoreDecision UnsafePath { get; } = new(true, "(unsafe path)", null, 0);
}
