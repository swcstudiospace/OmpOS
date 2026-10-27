namespace OmpOS.Workspace.Conventions;

/// <summary>A problem found while discovering conventions. Discovery never throws for a bad file: it records one of these and goes on.</summary>
/// <param name="Item">The kind of convention file the problem belongs to.</param>
/// <param name="Origin">The path to show: relative to the workspace root for project files, <c>~/</c> relative for user files.</param>
/// <param name="Kind">What went wrong.</param>
/// <param name="Reason">One plain sentence that says why.</param>
public sealed record ConventionDiagnostic(ConventionItemKind Item, string Origin, ConventionDiagnosticKind Kind, string Reason);
