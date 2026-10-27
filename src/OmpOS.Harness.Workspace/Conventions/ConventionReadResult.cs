namespace OmpOS.Workspace.Conventions;

/// <summary>The outcome of reading one text file through an <see cref="IConventionFileSource"/>.</summary>
/// <param name="Text">The file text without a byte order mark, or <see langword="null"/> when the file could not be read.</param>
/// <param name="Problem">The reason the file could not be read, or <see langword="null"/> when it could.</param>
/// <param name="ProblemKind">The kind of the problem; meaningful only when <paramref name="Problem"/> is not null.</param>
public sealed record ConventionReadResult(string? Text, string? Problem = null, ConventionDiagnosticKind ProblemKind = ConventionDiagnosticKind.Unreadable)
{
    /// <summary>Gets a value indicating whether the file was read.</summary>
    public bool IsRead => Text is not null;

    /// <summary>Creates a successful result.</summary>
    /// <param name="text">The file text.</param>
    /// <returns>The result.</returns>
    public static ConventionReadResult Read(string text) => new(text);

    /// <summary>Creates a failed result.</summary>
    /// <param name="kind">What went wrong.</param>
    /// <param name="problem">One plain sentence that says why.</param>
    /// <returns>The result.</returns>
    public static ConventionReadResult Fail(ConventionDiagnosticKind kind, string problem) => new(null, problem, kind);
}
