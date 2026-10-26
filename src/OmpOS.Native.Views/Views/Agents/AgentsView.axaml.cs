using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using OmpOS.Presentation.Agents;

namespace OmpOS.Native.Views.Agents;

/// <summary>
/// The Agents window (ADE-05). It draws the <see cref="AgentsViewModel"/> that is its data context: the status filter, one
/// button per visible run, and the open run's title, status and transcript. A row click opens the run's view and Close run
/// closes it; neither changes the run's status.
/// </summary>
internal partial class AgentsView : UserControl
{
    /// <summary>The filter choices in combo-box order. Index 0 is All; index n is the status with value n - 1.</summary>
    private static readonly string[] FilterLabels = ["All", "Running", "Completed", "Failed", "Cancelled"];

    private AgentsViewModel? _model;
    private bool _syncingFilter;

    /// <summary>Creates the window with the status filter set to All and no run open.</summary>
    public AgentsView()
    {
        InitializeComponent();
        StatusFilter.ItemsSource = FilterLabels;
        StatusFilter.SelectionChanged += OnFilterSelectionChanged;
        AgentRows.AddHandler(Button.ClickEvent, OnRunRowClick);
        CloseRunButton.Click += OnCloseRunClick;
        Refresh();
    }

    /// <summary>Binds the window to the state of the data context and redraws it.</summary>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_model is not null)
        {
            _model.PropertyChanged -= OnModelPropertyChanged;
            ((INotifyCollectionChanged)_model.VisibleRuns).CollectionChanged -= OnVisibleRunsChanged;
        }

        _model = DataContext as AgentsViewModel;
        if (_model is not null)
        {
            _model.PropertyChanged += OnModelPropertyChanged;
            ((INotifyCollectionChanged)_model.VisibleRuns).CollectionChanged += OnVisibleRunsChanged;
        }

        Refresh();
    }

    private static int FilterIndex(AgentRunStatus? status) => status is null ? 0 : (int)status.Value + 1;

    private static AgentRunStatus? FilterStatus(int index) => index <= 0 ? null : (AgentRunStatus)(index - 1);

    private void Refresh()
    {
        _syncingFilter = true;
        try
        {
            StatusFilter.SelectedIndex = FilterIndex(_model?.StatusFilter);
        }
        finally
        {
            _syncingFilter = false;
        }

        var opened = _model?.OpenedRun;
        RunPanel.DataContext = opened;
        RunPanel.IsVisible = opened is not null;

        EmptyState.IsVisible = _model is { VisibleRuns.Count: 0 };
        EmptyState.Text = _model?.StatusFilter is null ? "No runs yet." : "No runs have this status.";
    }

    private void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e) => Refresh();

    private void OnVisibleRunsChanged(object? sender, NotifyCollectionChangedEventArgs e) => Refresh();

    private void OnFilterSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_model is null || _syncingFilter || StatusFilter.SelectedIndex < 0)
        {
            return;
        }

        _model.StatusFilter = FilterStatus(StatusFilter.SelectedIndex);
    }

    private void OnRunRowClick(object? sender, RoutedEventArgs e)
    {
        if (_model is not null && e.Source is Control { DataContext: AgentRun run })
        {
            _model.OpenRun(run);
        }
    }

    private void OnCloseRunClick(object? sender, RoutedEventArgs e) => _model?.CloseRun();
}
