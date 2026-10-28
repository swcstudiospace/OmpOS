using Autonogrammer.Presentation.Editor;

namespace Autonogrammer.Presentation.Settings.Workspaces;

/// <summary>
/// One remembered workspace set, as the Workspaces page lists it. The source formats the last-used time, so the page holds no clock or culture.
/// </summary>
/// <param name="Id">The identifier of the set in the store.</param>
/// <param name="Name">The name of the set.</param>
/// <param name="FolderCount">The number of folders.</param>
/// <param name="PrimaryPath">The path of the primary folder.</param>
/// <param name="LastUsedText">When the set was last saved, as a short text for the user.</param>
/// <param name="Roots">The folders, primary first, ready to load into the editor.</param>
public sealed record WorkspaceRecentRow(
    string Id,
    string Name,
    int FolderCount,
    string PrimaryPath,
    string LastUsedText,
    IReadOnlyList<WorkspacePickerRoot> Roots)
{
    /// <summary>Gets "1 folder" or "N folders".</summary>
    public string FoldersText => WorkspacesCopy.FolderCount(FolderCount);

    /// <summary>Gets the sentence that says when the set was last used.</summary>
    public string LastUsedLine => WorkspacesCopy.LastUsed(LastUsedText);

    /// <summary>Gets the whole row as one sentence, its accessible name.</summary>
    public string Summary => WorkspacesCopy.RecentSummary(Name, FolderCount, PrimaryPath, LastUsedLine);

    /// <summary>Gets the accessible name of the Load into editor button of the row.</summary>
    public string LoadName => WorkspacesCopy.LoadName(Name);

    /// <summary>Gets the accessible name of the Forget button of the row.</summary>
    public string ForgetName => WorkspacesCopy.ForgetName(Name);
}
