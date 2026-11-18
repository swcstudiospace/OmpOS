using System.ComponentModel;
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
    private PropertyChangedEventHandler? _listChanged;
    private PropertyChangedEventHandler? _editorChanged;

    /// <summary>Creates the view.</summary>
    public ProjectsView()
    {
        InitializeComponent();
        RenameButton.Click += OnRename;
        ArchiveButton.Click += OnArchive;
        PrepareDeleteButton.Click += OnPrepareDelete;
        ConfirmDeleteButton.Click += OnConfirmDelete;
        NameBox.TextChanged += OnNameChanged;
    }

    /// <summary>Gets the named controls in keyboard order.</summary>
    public IReadOnlyList<Control> KeyboardOrder => [ProjectRows, NameBox, RenameButton, ArchiveButton, PrepareDeleteButton, ConfirmDeleteButton];

    /// <summary>Applies reduced motion. The view has no transition to remove.</summary>
    /// <param name="reduced">True when motion is reduced.</param>
    public void ApplyMotion(bool reduced) => Classes.Set(ReducedMotionClass, reduced);

    /// <summary>Binds the list and the editor. The view then refreshes itself when either model changes.</summary>
    /// <param name="list">The list.</param>
    /// <param name="editor">The editor.</param>
    public void Attach(ProjectListModel list, ProjectEditorModel editor)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(editor);
        if (_list is not null && _listChanged is not null)
        {
            _list.PropertyChanged -= _listChanged;
        }

        if (_editor is not null && _editorChanged is not null)
        {
            _editor.PropertyChanged -= _editorChanged;
        }

        _list = list;
        _editor = editor;
        _listChanged = OnListChanged;
        _editorChanged = OnEditorChanged;
        list.PropertyChanged += _listChanged;
        editor.PropertyChanged += _editorChanged;
        Refresh();
    }

    /// <summary>Refreshes the bound text after a model change.</summary>
    public void Refresh()
    {
        ShowList();
        ShowEditor();
    }

    private void ShowList()
    {
        if (_list is not null)
        {
            ProjectRows.ItemsSource = _list.Rows;
            StatusText.Text = _list.Sentence ?? _list.State.ToString();
        }
    }

    /// <summary>Shows the editor's name and delete confirmation. The name box is written only when its text differs, so typing is never overwritten.</summary>
    private void ShowEditor()
    {
        if (_editor is not null)
        {
            if (!string.Equals(NameBox.Text, _editor.Name, StringComparison.Ordinal))
            {
                NameBox.Text = _editor.Name;
            }

            ConfirmText.Text = _editor.DeleteConfirmation;
        }
    }

    private void OnListChanged(object? sender, PropertyChangedEventArgs e) => CoworkUiThread.Run(ShowList);

    private void OnEditorChanged(object? sender, PropertyChangedEventArgs e) => CoworkUiThread.Run(ShowEditor);

    /// <summary>Copies the name box text into the editor, so a rename sends the name that was typed.</summary>
    private void CopyNameFromBox()
    {
        if (_editor is not null)
        {
            _editor.Name = NameBox.Text ?? string.Empty;
        }
    }

    private void OnNameChanged(object? sender, TextChangedEventArgs e) => CopyNameFromBox();

    private void OnRename(object? sender, RoutedEventArgs e)
    {
        CopyNameFromBox();
        _ = _editor?.RenameAsync(CancellationToken.None);
    }

    private void OnArchive(object? sender, RoutedEventArgs e) => _ = _editor?.ArchiveAsync(CancellationToken.None);

    private void OnPrepareDelete(object? sender, RoutedEventArgs e) => _editor?.PrepareDelete();

    private void OnConfirmDelete(object? sender, RoutedEventArgs e) => _ = _editor?.DeleteAsync(CancellationToken.None);
}
