namespace Autonogrammer.Presentation.Editor;

/// <summary>
/// What a backend read from a <c>.code-workspace</c> file.
/// </summary>
/// <param name="Name">The name of the set, from the file name.</param>
/// <param name="Roots">The folders, the first being the primary.</param>
/// <param name="Warnings">Entries of the file that could not become roots, as sentences for the user.</param>
public sealed record WorkspacePickerImport(string Name, IReadOnlyList<WorkspacePickerRoot> Roots, IReadOnlyList<string> Warnings);
