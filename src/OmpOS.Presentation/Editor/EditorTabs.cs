using System.Collections.ObjectModel;
using OmpOS.Presentation.Observable;

namespace OmpOS.Presentation.Editor;

/// <summary>
/// The open files of the editor, one tab per file, with one selected tab (ADE-09). A file is identified by its normalised
/// workspace path, so <c>src//a.cs</c> and <c>src/a.cs</c> name the same tab.
/// </summary>
/// <remarks>
/// Opening a file that already has a tab selects that tab and keeps its buffer, so unsaved edits are not replaced by a
/// second read. Closing the selected tab selects the tab that takes its place, or the last tab when it was the last one.
/// </remarks>
public sealed class EditorTabs : ObservableModel
{
    private readonly ObservableCollection<EditorTab> _tabs = [];
    private EditorTab? _selectedTab;

    /// <summary>Creates an empty tab set.</summary>
    public EditorTabs()
    {
        Tabs = new ReadOnlyObservableCollection<EditorTab>(_tabs);
    }

    /// <summary>Gets the open tabs in the order they were opened.</summary>
    public ReadOnlyObservableCollection<EditorTab> Tabs { get; }

    /// <summary>Gets the selected tab, or null when no tab is open.</summary>
    public EditorTab? SelectedTab
    {
        get => _selectedTab;
        private set => SetProperty(ref _selectedTab, value);
    }

    /// <summary>Opens a file in a new tab and selects it, or selects the file's existing tab.</summary>
    /// <param name="relativePath">The workspace-relative path of the file.</param>
    /// <param name="text">The file's text. It is ignored when the file already has a tab.</param>
    /// <returns>The tab for the file.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="relativePath"/> or <paramref name="text"/> is null.</exception>
    /// <exception cref="ArgumentException">The path is not a valid workspace path, or names the root.</exception>
    public EditorTab Open(string relativePath, string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var path = WorkspacePath.Normalize(relativePath);
        if (path.Length == 0)
        {
            throw new ArgumentException("A tab needs a file path, not the workspace root.", nameof(relativePath));
        }

        var existing = FindTab(path);
        if (existing is not null)
        {
            SelectedTab = existing;
            return existing;
        }

        var tab = new EditorTab(path, new TextDocument(text));
        _tabs.Add(tab);
        SelectedTab = tab;
        return tab;
    }

    /// <summary>Selects the open tab for a file.</summary>
    /// <param name="relativePath">The workspace-relative path of the file.</param>
    /// <exception cref="ArgumentException">The path is not a valid workspace path, or no tab is open for it.</exception>
    public void Select(string relativePath)
    {
        var tab = FindTab(WorkspacePath.Normalize(relativePath))
            ?? throw new ArgumentException("No tab is open for this path.", nameof(relativePath));
        SelectedTab = tab;
    }

    /// <summary>Closes the tab for a file. Closing the selected tab selects its successor, or the last tab when it was the last one.</summary>
    /// <param name="relativePath">The workspace-relative path of the file.</param>
    /// <returns>True when a tab was closed; false when no tab is open for the path.</returns>
    /// <exception cref="ArgumentException">The path is not a valid workspace path.</exception>
    public bool Close(string relativePath)
    {
        var path = WorkspacePath.Normalize(relativePath);
        var index = Enumerable.Range(0, _tabs.Count).FirstOrDefault(i => string.Equals(_tabs[i].RelativePath, path, StringComparison.Ordinal), -1);
        if (index < 0)
        {
            return false;
        }

        var wasSelected = ReferenceEquals(_selectedTab, _tabs[index]);
        _tabs.RemoveAt(index);
        if (wasSelected)
        {
            SelectedTab = _tabs.Count == 0 ? null : _tabs[Math.Min(index, _tabs.Count - 1)];
        }

        return true;
    }

    private EditorTab? FindTab(string normalizedPath)
    {
        foreach (var tab in _tabs)
        {
            if (string.Equals(tab.RelativePath, normalizedPath, StringComparison.Ordinal))
            {
                return tab;
            }
        }

        return null;
    }
}
