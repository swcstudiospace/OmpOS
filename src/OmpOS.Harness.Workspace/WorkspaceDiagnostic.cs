namespace OmpOS.Workspace;

/// <summary>
/// A problem found in a convention file. The file is skipped; the rest of the workspace still loads.
/// </summary>
/// <param name="RelativePath">The file path relative to the workspace root, with '/' separators.</param>
/// <param name="Reason">Why the file is malformed.</param>
public sealed record WorkspaceDiagnostic(string RelativePath, string Reason);
