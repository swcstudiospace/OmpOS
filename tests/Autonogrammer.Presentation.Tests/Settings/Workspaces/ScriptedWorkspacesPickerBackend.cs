using Autonogrammer.Presentation.Editor;

namespace Autonogrammer.Presentation.Tests.Settings.Workspaces;

/// <summary>
/// A picker backend the test scripts. A path that repeats an earlier one is an error on the later row; a path containing "notes" is a warning.
/// Imports and exports are held in memory.
/// </summary>
internal sealed class ScriptedWorkspacesPickerBackend : IWorkspaceSetPickerBackend
{
    /// <summary>Gets or sets what an import returns, or null to fail it.</summary>
    internal WorkspacePickerImport? Import { get; set; }

    /// <summary>Gets the paths written by exports, in order.</summary>
    internal List<string> Exported { get; } = [];

    /// <inheritdoc/>
    public IReadOnlyList<WorkspacePickerIssue> Validate(IReadOnlyList<WorkspacePickerRoot> roots)
    {
        var issues = new List<WorkspacePickerIssue>();
        for (var index = 0; index < roots.Count; index++)
        {
            if (roots.Take(index).Any(earlier => string.Equals(earlier.Path, roots[index].Path, StringComparison.Ordinal)))
            {
                issues.Add(new WorkspacePickerIssue(IsError: true, roots[index].Id, $"'{roots[index].DisplayName}' is the same folder as an earlier root."));
            }

            if (roots[index].Path.Contains("notes", StringComparison.Ordinal))
            {
                issues.Add(new WorkspacePickerIssue(IsError: false, roots[index].Id, $"'{roots[index].DisplayName}' is not a git repository."));
            }
        }

        return issues;
    }

    /// <inheritdoc/>
    public WorkspacePickerImport ImportCodeWorkspace(string filePath) =>
        Import ?? throw new InvalidDataException("nothing scripted");

    /// <inheritdoc/>
    public void ExportCodeWorkspace(string filePath, IReadOnlyList<WorkspacePickerRoot> roots) => Exported.Add(filePath);
}
