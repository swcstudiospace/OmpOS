namespace OmpOS.Workspace.Conventions;

/// <summary>The outcome of listing one folder.</summary>
/// <param name="Entries">The entries in ordinal name order. A folder that does not exist lists no entries and no problem.</param>
/// <param name="Problem">The reason the folder could not be listed, or <see langword="null"/> when it could.</param>
/// <param name="ProblemKind">The kind of the problem; meaningful only when <paramref name="Problem"/> is not null.</param>
public sealed record ConventionListResult(IReadOnlyList<ConventionEntry> Entries, string? Problem = null, ConventionDiagnosticKind ProblemKind = ConventionDiagnosticKind.Unreadable)
{
    /// <summary>Gets a result with no entries and no problem.</summary>
    public static ConventionListResult Empty { get; } = new([]);

    /// <summary>Creates a failed result.</summary>
    /// <param name="kind">What went wrong.</param>
    /// <param name="problem">One plain sentence that says why.</param>
    /// <returns>The result.</returns>
    public static ConventionListResult Fail(ConventionDiagnosticKind kind, string problem) => new([], problem, kind);
}
