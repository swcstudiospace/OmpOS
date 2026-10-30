namespace Autonogrammer.Workspace.Ignore;

/// <summary>A problem found while loading ignore rules. Loading never throws for a bad file or line: it records one of these and goes on.</summary>
/// <param name="Source">The ignore file path relative to the workspace root, or the name of the built-in or global list.</param>
/// <param name="Line">The 1-based line the problem belongs to, or 0 when it concerns the whole file.</param>
/// <param name="Message">One plain sentence that says what was skipped and why.</param>
public sealed record IgnoreDiagnostic(string Source, int Line, string Message);
