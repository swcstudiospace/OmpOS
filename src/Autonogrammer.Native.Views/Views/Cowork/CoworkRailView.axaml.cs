using System.ComponentModel;
using Autonogrammer.Presentation.Cowork;
using Autonogrammer.Presentation.Cowork.Projects;
using Avalonia.Automation;
using Avalonia.Controls;


namespace Autonogrammer.Native.Views.Cowork;

/// <summary>
/// The Cowork rail: the filter, the project for the next task, the new-task control and the tasks grouped by project.
/// It binds <see cref="CoworkSessionListModel"/>, never a Code catalog. The project picker shows the active projects of the account it is bound to;
/// no project selected means the next task is unlinked.
/// </summary>
public partial class CoworkRailView : UserControl
{
    private CoworkSessionListModel? _list;
    private ProjectListModel? _projects;
    private Func<string?, CancellationToken, Task<(string Id, string? Notice)>>? _createTask;
    private string? _createNotice;
    private bool _syncingPicker;

    /// <summary>Creates the rail with no list.</summary>
    public CoworkRailView()
    {
        InitializeComponent();
        CoworkFilter.TextChanged += OnFilterChanged;
        TaskRows.SelectionChanged += OnSelectionChanged;
        ProjectPicker.SelectionChanged += OnProjectPicked;
        NewTaskButton.Click += OnNewTaskClick;
        NoProjectButton.Click += OnNoProjectClick;
    }

    /// <summary>Gets the task list.</summary>
    public ListBox Rows => TaskRows;

    /// <summary>Gets the filter box.</summary>
    public TextBox FilterBox => CoworkFilter;

    /// <summary>Gets the new-task button.</summary>
    public Button NewTask => NewTaskButton;

    /// <summary>Gets the project picker for the next task.</summary>
    public ComboBox ProjectChoice => ProjectPicker;

    /// <summary>Gets the button that clears the project choice, so the next task is unlinked.</summary>
    public Button NoProject => NoProjectButton;

    /// <summary>Gets the notice line: a failed or refused create, or the project list's sentence.</summary>
    public TextBlock Notice => NoticeText;

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

    /// <summary>
    /// Binds the project picker and the new-task action. The list loads once here; later account changes reach the picker through the model.
    /// </summary>
    /// <param name="projects">The project list of the signed-in account, or null when there is no account.</param>
    /// <param name="createTask">
    /// The new-task action. Its argument is the selected project, or null for an unlinked task. Its result is the new task's identifier and a notice, or null.
    /// </param>
    public void BindTasks(ProjectListModel? projects, Func<string?, CancellationToken, Task<(string Id, string? Notice)>>? createTask)
    {
        if (_projects is not null)
        {
            _projects.PropertyChanged -= OnProjectsChanged;
        }

        _projects = projects;
        _createTask = createTask;
        if (projects is not null)
        {
            projects.PropertyChanged += OnProjectsChanged;
            _ = projects.LoadAsync(CancellationToken.None);
        }

        ApplyProjects();
    }

    /// <summary>Runs the new-task action with the selected project. The new task is selected in the list. A refused or failed create shows its reason on the notice line.</summary>
    /// <param name="cancellationToken">Cancels the create.</param>
    /// <returns>The new task's identifier, or null when the task was not created.</returns>
    public async Task<string?> NewTaskAsync(CancellationToken cancellationToken)
    {
        if (_createTask is null)
        {
            ShowNotice("Cowork tasks cannot be created right now.");
            return null;
        }

        try
        {
            var (id, notice) = await _createTask(_projects?.SelectedId, cancellationToken).ConfigureAwait(true);
            _list?.Select(id);
            ShowNotice(notice);
            return id;
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or InvalidDataException or UnauthorizedAccessException)
        {
            ShowNotice(exception.Message);
            return null;
        }
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

    private void ApplyProjects()
    {
        _syncingPicker = true;
        try
        {
            if (_projects is null)
            {
                ProjectPicker.ItemsSource = null;
                ProjectPicker.IsEnabled = false;
                NoProjectButton.IsEnabled = false;
            }
            else
            {
                var active = _projects.Rows.Where(static row => !row.Archived).ToList();
                var selected = _projects.Selected;
                ProjectPicker.ItemsSource = active;
                ProjectPicker.SelectedItem = selected is null ? null : active.Find(row => string.Equals(row.Id, selected.Id, StringComparison.Ordinal));
                ProjectPicker.IsEnabled = true;
                NoProjectButton.IsEnabled = true;
            }
        }
        finally
        {
            _syncingPicker = false;
        }

        RefreshNotice();
    }

    private void OnProjectsChanged(object? sender, PropertyChangedEventArgs e) => ApplyProjects();

    private void OnProjectPicked(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncingPicker || _projects is null)
        {
            return;
        }

        var id = (ProjectPicker.SelectedItem as ProjectListItem)?.Id;
        if (!_projects.Select(id))
        {
            ApplyProjects();
        }
    }

    private void OnNoProjectClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _ = _projects?.Select(null);
        ApplyProjects();
    }

    private void OnNewTaskClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => _ = NewTaskAsync(CancellationToken.None);

    private void ShowNotice(string? text)
    {
        _createNotice = text;
        RefreshNotice();
    }

    private void RefreshNotice()
    {
        var text = _createNotice ?? _projects?.Sentence;
        NoticeText.Text = text;
        NoticeText.IsVisible = text is not null;
        if (text is not null)
        {
            AutomationProperties.SetName(NoticeText, text);
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
