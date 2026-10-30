namespace Autonogrammer.Workspace.Sets;

/// <summary>
/// The result of reading a <c>.code-workspace</c> file into a <see cref="WorkspaceSet"/>.
/// </summary>
/// <param name="Set">The set. The first folder of the file is the primary root.</param>
/// <param name="Warnings">Entries of the file that could not become roots, each as a sentence for the user.</param>
public sealed record CodeWorkspaceImport(WorkspaceSet Set, IReadOnlyList<string> Warnings);
