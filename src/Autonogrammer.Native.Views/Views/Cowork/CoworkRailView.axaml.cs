using Autonogrammer.Presentation.Cowork;
using Avalonia.Automation;
using Avalonia.Controls;


namespace Autonogrammer.Native.Views.Cowork;

/// <summary>
/// The Cowork rail: the filter, the new-task control and the tasks grouped by project. It binds <see cref="CoworkSessionListModel"/>, never a Code catalog.
/// </summary>
public partial class CoworkRailView : UserControl
{
    private CoworkSessionListModel? _list;

    /// <summary>Creates the rail with no list.</summary>
    public CoworkRailView()
    {
        InitializeComponent();
        CoworkFilter.TextChanged += OnFilterChanged;
        TaskRows.SelectionChanged += OnSelectionChanged;
    }

    /// <summary>Gets the task list.</summary>
    public ListBox Rows => TaskRows;

    /// <summary>Gets the filter box.</summary>
    public TextBox FilterBox => CoworkFilter;

    /// <summary>Gets the new-task button.</summary>
    public Button NewTask => NewTaskButton;
    /// <summary>Gets the Projects section button. It opens the project editor.</summary>
    public Button ProjectsSection => ProjectsButton;

    /// <summary>Gets the host the Projects section fills with the editor.</summary>
    public ContentControl ProjectEditor => ProjectEditorHost;


    /// <summary>Binds the rail to the list model.</summary>
    /// <param name="list">The Cowork list.</param>
    /// <exception cref="ArgumentNullException"><paramref name="list"/> is null.</exception>
    public void Attach(CoworkSessionListModel list)
    {
        ArgumentNullException.ThrowIfNull(list);
        if (_list is not null)
        {
            _list.Changed -= OnChanged;
        }

        _list = list;
        list.Changed += OnChanged;
        Apply();
    }

    private void OnChanged(object? sender, EventArgs e) => Apply();

    private void Apply()
    {
        if (_list is null)
        {
            return;
        }

        TaskRows.ItemsSource = _list.Rows;
        var limit = _list.LimitMessage;
        LimitText.IsVisible = limit is not null;
        LimitText.Text = limit;
        if (limit is not null)
        {
            AutomationProperties.SetName(LimitText, limit);
        }
    }

    private void OnFilterChanged(object? sender, TextChangedEventArgs e)
    {
        if (_list is not null)
        {
            _list.Filter = CoworkFilter.Text ?? string.Empty;
        }
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_list is not null && TaskRows.SelectedItem is CoworkSessionRow row)
        {
            _list.Select(row.Id);
        }
    }
    private void OnProjectsClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var editor = new ProjectsView();
        ProjectEditorHost.Content = editor;
        ProjectEditorHost.IsVisible = true;
    }

}
