using System.ComponentModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using OmpOS.Presentation.AgentPanel;

namespace OmpOS.Native.Views.AgentPanel;

/// <summary>
/// The Agents rail: the new-agent button, the filter box and one grouped row per session. It draws the <see cref="SessionCatalogModel"/>
/// that is its data context and draws the loading and error copy of <see cref="Panel"/>. Selecting a row selects the session in the
/// catalog; closing is raised as <see cref="CloseRequested"/> so the owner can ask for confirmation first.
/// </summary>
internal partial class AgentRailView : UserControl
{
    private readonly ModelWatcher<SessionCatalogModel> _catalogWatch;
    private readonly ModelWatcher<AgentPanelViewModel> _panelWatch;
    private AgentPanelViewModel? _panel;
    private bool _attached;
    private int _syncing;

    /// <summary>Creates a rail with no model.</summary>
    public AgentRailView()
    {
        InitializeComponent();
        _catalogWatch = new ModelWatcher<SessionCatalogModel>(Refresh);
        _panelWatch = new ModelWatcher<AgentPanelViewModel>(Refresh);

        AgentRows.SelectionChanged += OnSelectionChanged;
        AgentRows.ContainerPrepared += OnContainerPrepared;
        AgentRows.AddHandler(KeyDownEvent, OnRowsKeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        AgentFilter.KeyDown += OnFilterKeyDown;
        NewAgentButton.Click += OnNewAgentClick;
        NewTerminalButton.Click += OnNewTerminalClick;
        RowMenu.Opening += OnMenuOpening;
        ResumeItem.Click += OnResumeClick;
        ReopenItem.Click += OnReopenClick;
        CloseItem.Click += OnCloseClick;
        Refresh();
    }

    /// <summary>Occurs after the New agent command finished, so the owner can move focus to the new session's prompt.</summary>
    public event EventHandler? NewAgentCompleted;

    /// <summary>Occurs after the New omp terminal command finished, so the owner can move focus to the terminal.</summary>
    public event EventHandler? NewTerminalCompleted;

    /// <summary>Occurs when Enter is pressed in the agent list, so the owner can move focus to the prompt of the selected agent.</summary>
    public event EventHandler? RowActivated;

    /// <summary>Occurs when the user asks to close an agent, by Delete or the row menu. The argument is the session identifier.</summary>
    public event EventHandler<AgentRowEventArgs>? CloseRequested;

    /// <summary>
    /// Gets or sets the panel view model. It supplies the New agent command and the loading and error copy of the rail. Null draws a rail
    /// that shows only the catalog.
    /// </summary>
    public AgentPanelViewModel? Panel
    {
        get => _panel;
        set
        {
            if (ReferenceEquals(_panel, value))
            {
                return;
            }

            _panel = value;
            if (_attached)
            {
                _panelWatch.Set(value);
            }

            Refresh();
        }
    }

    /// <summary>Moves keyboard focus to the selected row, or to the list when no row is selected.</summary>
    public void FocusRows()
    {
        if (AgentRows.SelectedItem is { } item && AgentRows.ContainerFromItem(item) is Control container)
        {
            container.FocusWhenReady();
            return;
        }

        AgentRows.FocusWhenReady();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _attached = true;
        _catalogWatch.Set(DataContext as SessionCatalogModel);
        _panelWatch.Set(_panel);
        Refresh();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        _attached = false;
        _catalogWatch.Set(null);
        _panelWatch.Set(null);
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_attached)
        {
            _catalogWatch.Set(DataContext as SessionCatalogModel);
        }

        Refresh();
    }

    private void Refresh()
    {
        var catalog = DataContext as SessionCatalogModel;

        _syncing++;
        try
        {
            var rows = catalog?.Rows;
            if (!ReferenceEquals(AgentRows.ItemsSource, rows))
            {
                AgentRows.ItemsSource = rows;
            }

            var selected = catalog is null ? null : rows?.FirstOrDefault(row => string.Equals(row.Id, catalog.SelectedId, StringComparison.Ordinal));
            if (!ReferenceEquals(AgentRows.SelectedItem, selected))
            {
                AgentRows.SelectedItem = selected;
            }
        }
        finally
        {
            _syncing--;
        }

        string? heading = null;
        string? body = null;
        if (_panel?.State == PanelState.Loading)
        {
            heading = AgentPanelCopy.RailLoading;
        }
        else if (_panel?.State == PanelState.Error)
        {
            heading = AgentPanelCopy.RailErrorHeading;
            body = AgentPanelCopy.RailErrorBody;
        }
        else if (catalog is { TotalCount: 0 })
        {
            heading = AgentPanelCopy.EmptyRailHeading;
            body = AgentPanelCopy.EmptyRailBody;
        }
        else if (catalog is { HasNoMatches: true })
        {
            heading = catalog.NoMatchHeading;
            body = catalog.NoMatchBody;
        }

        StateHeading.Text = heading;
        StateBody.Text = body;
        StateBody.IsVisible = body is not null;
        StatePanel.IsVisible = heading is not null;
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (AgentRows.SelectedItem is AgentRow selected)
        {
            ShowMenuItemsFor(selected);
        }

        if (_syncing > 0 || DataContext is not SessionCatalogModel catalog)
        {
            return;
        }

        if (AgentRows.SelectedItem is AgentRow row)
        {
            catalog.Select(row.Id);
        }
    }

    private void OnContainerPrepared(object? sender, ContainerPreparedEventArgs e)
    {
        if (e.Container is ListBoxItem { Content: AgentRow row } item)
        {
            AutomationProperties.SetName(item, row.AccessibleName);
        }
    }

    private void OnRowsKeyDown(object? sender, KeyEventArgs e)
    {
        if (AgentRows.SelectedItem is not AgentRow row || e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            RowActivated?.Invoke(this, EventArgs.Empty);
        }
        else if (e.Key == Key.Delete)
        {
            e.Handled = true;
            CloseRequested?.Invoke(this, new AgentRowEventArgs(row.Id));
        }
    }

    private void OnFilterKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && e.KeyModifiers == KeyModifiers.None)
        {
            e.Handled = true;
            AgentFilter.Text = string.Empty;
            if (DataContext is SessionCatalogModel catalog)
            {
                catalog.Filter = string.Empty;
            }
        }
    }

    private void OnNewAgentClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => _ = NewAgentAsync();

    private void OnNewTerminalClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => _ = NewTerminalAsync();

    private async Task NewTerminalAsync()
    {
        if (_panel is null)
        {
            return;
        }

        await _panel.NewTerminalCommand.ExecuteAsync(null).ConfigureAwait(true);
        NewTerminalCompleted?.Invoke(this, EventArgs.Empty);
    }

    private async Task NewAgentAsync()
    {
        if (_panel is null)
        {
            return;
        }

        await _panel.NewSessionCommand.ExecuteAsync(null).ConfigureAwait(true);
        NewAgentCompleted?.Invoke(this, EventArgs.Empty);
    }

    private void OnMenuOpening(object? sender, CancelEventArgs e)
    {
        if (AgentRows.SelectedItem is not AgentRow row)
        {
            e.Cancel = true;
            return;
        }

        ShowMenuItemsFor(row);
    }

    // The menu offers what fits the selected row, so the items are right whenever the menu opens.
    private void ShowMenuItemsFor(AgentRow row)
    {
        var closed = row.Status == AgentRowStatus.Closed;
        ResumeItem.IsVisible = row.IsLost || closed;
        ReopenItem.IsVisible = closed;
        CloseItem.IsVisible = !closed;
    }

    private AgentSessionViewModel? SelectedSession()
    {
        var session = _panel?.Selected;
        return AgentRows.SelectedItem is AgentRow row && session is not null && string.Equals(session.Id, row.Id, StringComparison.Ordinal)
            ? session
            : null;
    }

    private void OnResumeClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var session = SelectedSession();
        if (session is null)
        {
            return;
        }

        (session.IsClosed ? session.ReopenCommand : session.ResumeCommand).Execute(null);
    }

    private void OnReopenClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => SelectedSession()?.ReopenCommand.Execute(null);

    private void OnCloseClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (AgentRows.SelectedItem is AgentRow row)
        {
            CloseRequested?.Invoke(this, new AgentRowEventArgs(row.Id));
        }
    }
}
