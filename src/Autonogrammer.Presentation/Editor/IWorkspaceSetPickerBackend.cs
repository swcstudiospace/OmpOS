namespace Autonogrammer.Presentation.Editor;

/// <summary>
/// What the set picker needs from the host: the validation rules, and reading and writing <c>.code-workspace</c> files. The desktop
/// head implements it over the workspace library (<c>WorkspaceSetValidator</c> and <c>CodeWorkspaceFile</c>); tests use a script.
/// </summary>
public interface IWorkspaceSetPickerBackend
{
    /// <summary>Checks a set of roots.</summary>
    /// <param name="roots">The roots, primary first.</param>
    /// <returns>Every finding, errors and warnings.</returns>
    IReadOnlyList<WorkspacePickerIssue> Validate(IReadOnlyList<WorkspacePickerRoot> roots);

    /// <summary>Reads a <c>.code-workspace</c> file.</summary>
    /// <param name="filePath">The path of the file.</param>
    /// <returns>The folders and warnings.</returns>
    /// <exception cref="FileNotFoundException">The file does not exist.</exception>
    /// <exception cref="InvalidDataException">The file is not a usable workspace file.</exception>
    /// <exception cref="IOException">The file cannot be read.</exception>
    WorkspacePickerImport ImportCodeWorkspace(string filePath);

    /// <summary>Writes the roots to a <c>.code-workspace</c> file, keeping what an existing file holds besides its folders.</summary>
    /// <param name="filePath">The path of the file.</param>
    /// <param name="roots">The roots, primary first.</param>
    /// <exception cref="InvalidOperationException">A root cannot be written, for example a remote one.</exception>
    /// <exception cref="InvalidDataException">The existing file is not a usable workspace file.</exception>
    /// <exception cref="IOException">The file cannot be written.</exception>
    void ExportCodeWorkspace(string filePath, IReadOnlyList<WorkspacePickerRoot> roots);
}
