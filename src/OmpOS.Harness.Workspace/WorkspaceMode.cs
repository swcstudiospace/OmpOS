namespace OmpOS.Workspace;

/// <summary>
/// A custom agent mode loaded from a JSON file. Its rule and skill ids all name loaded documents.
/// </summary>
/// <param name="Id">The mode id.</param>
/// <param name="DisplayName">The name shown in the mode picker.</param>
/// <param name="RelativePath">The file path relative to the workspace root, with '/' separators.</param>
/// <param name="Rules">The ids of the rules the mode uses, in file order without duplicates.</param>
/// <param name="Skills">The ids of the skills the mode uses, in file order without duplicates.</param>
public sealed record WorkspaceMode(
    string Id,
    string DisplayName,
    string RelativePath,
    IReadOnlyList<string> Rules,
    IReadOnlyList<string> Skills);
