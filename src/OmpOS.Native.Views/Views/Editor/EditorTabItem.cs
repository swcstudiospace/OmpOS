using System.Windows.Input;
using OmpOS.Presentation.Editor;
using OmpOS.Presentation.Observable;

namespace OmpOS.Native.Views.Editor;

/// <summary>
/// One tab in the editor's tab strip: the open file, whether it is the selected tab, and the commands that select and close it.
/// </summary>
internal sealed class EditorTabItem : ObservableModel
{
    private bool _isSelected;

    /// <summary>Creates the strip item for one open tab.</summary>
    /// <param name="tab">The open tab.</param>
    /// <param name="isSelected">Whether the tab is the selected one.</param>
    /// <param name="owner">The view model that owns the tabs. The commands act through it.</param>
    public EditorTabItem(EditorTab tab, bool isSelected, EditorViewModel owner)
    {
        Tab = tab;
        _isSelected = isSelected;
        SelectCommand = new EditorCommand(() =>
        {
            owner.Select(Path);

            // The toggle flips its own state on click. Raising the property restores it from the model, so a click on the
            // selected tab leaves it checked.
            NotifyPropertyChanged(nameof(IsSelected));
        });
        CloseCommand = new EditorCommand(() => owner.Close(Path));
    }

    /// <summary>Gets the open tab.</summary>
    public EditorTab Tab { get; }

    /// <summary>Gets the file name shown on the tab.</summary>
    public string Name => Tab.Name;

    /// <summary>Gets the normalised workspace path that identifies the tab.</summary>
    public string Path => Tab.RelativePath;

    /// <summary>Gets or sets whether this tab is the selected one.</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    /// <summary>Gets the command that selects the tab.</summary>
    public ICommand SelectCommand { get; }

    /// <summary>Gets the command that closes the tab.</summary>
    public ICommand CloseCommand { get; }
}
