using Autonogrammer.Presentation.Cowork.Projects;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Autonogrammer.Native.Views.Cowork;

/// <summary>The project list and editor. Keyboard order follows TabIndex. No colour literal and no transition.</summary>
public partial class ProjectsView : UserControl
{
    /// <summary>The class set when motion is reduced.</summary>
    public const string ReducedMotionClass = "reduced-motion";

    private ProjectListModel? _list;
    private ProjectEditorModel? _editor;

    /// <summary>Creates the view.</summary>
    public ProjectsView()
    {
        InitializeComponent();
        RenameButton.Click += OnRename;
        ArchiveButton.Click += OnArchive;
        PrepareDeleteButton.Click += OnPrepareDelete;
        ConfirmDeleteButton.Click += OnConfirmDelete;
    }

    /// <summary>Gets the named controls in keyboard order.</summary>
    public IReadOnlyList<Control> KeyboardOrder => [ProjectRows, NameBox, RenameButton, ArchiveButton, PrepareDeleteButton, ConfirmDeleteButton];

    /// <summary>Applies reduced motion. The view has no transition to remove.</summary>
    /// <param name="reduced">True when motion is reduced.</param>
    public void ApplyMotion(bool reduced) => Classes.Set(ReducedMotionClass, reduced);

    /// <summary>Binds the list and the editor.</summary>
    /// <param name="list">The list.</param>
    /// <param name="editor">The editor.</param>
    public void Attach(ProjectListModel list, ProjectEditorModel editor)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(editor);
        _list = list;
        _editor = editor;
        ProjectRows.ItemsSource = list.Rows;
        StatusText.Text = list.Sentence ?? list.State.ToString();
        NameBox.Text = editor.Name;
        ConfirmText.Text = editor.DeleteConfirmation;
    }

    /// <summary>Refreshes the bound text after a model change.</summary>
    public void Refresh()
    {
        if (_list is not null)
        {
            ProjectRows.ItemsSource = _list.Rows;
            StatusText.Text = _list.Sentence ?? _list.State.ToString();
        }

        if (_editor is not null)
        {
            NameBox.Text = _editor.Name;
            ConfirmText.Text = _editor.DeleteConfirmation;
        }
    }

    private void OnRename(object? sender, RoutedEventArgs e) => _ = _editor?.RenameAsync(CancellationToken.None);

    private void OnArchive(object? sender, RoutedEventArgs e) => _ = _editor?.ArchiveAsync(CancellationToken.None);

    private void OnPrepareDelete(object? sender, RoutedEventArgs e)
    {
        _editor?.PrepareDelete();
        Refresh();
    }

    private void OnConfirmDelete(object? sender, RoutedEventArgs e) => _ = _editor?.DeleteAsync(CancellationToken.None);
}
