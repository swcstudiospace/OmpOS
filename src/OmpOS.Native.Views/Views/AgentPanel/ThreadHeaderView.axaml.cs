using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using OmpOS.Presentation.AgentPanel;

namespace OmpOS.Native.Views.AgentPanel;

/// <summary>
/// The thread header of one agent: the title, the status, task, graph and steering chips, Close, Resume or Reopen, and the panel notice.
/// It draws the <see cref="AgentSessionViewModel"/> that is its data context. A host may put one control in <see cref="Trailing"/>, which
/// sits after Close in the keyboard order and before the notice buttons.
/// </summary>
internal partial class ThreadHeaderView : UserControl
{
    private readonly ModelWatcher<AgentSessionViewModel> _watch;
    private bool _attached;

    /// <summary>Creates a header with no model.</summary>
    public ThreadHeaderView()
    {
        InitializeComponent();
        _watch = new ModelWatcher<AgentSessionViewModel>(Refresh);
        CloseButton.Click += OnCloseClick;
        TaskChipButton.Click += OnTaskChipClick;
        PanelNoticeHost.ShowPipelineRequested += OnShowPipelineRequested;
        Refresh();
    }

    /// <summary>
    /// Occurs when Close is pressed while the agent needs the confirmation bar (a run is active or an approval waits). When no
    /// confirmation is needed the header asks the view model to close at once and raises nothing.
    /// </summary>
    public event EventHandler? CloseClicked;

    /// <summary>Occurs when the task chip is pressed. The owner decides what the chip opens; the shell has no task surface of its own.</summary>
    public event EventHandler? TaskChipClicked;

    /// <summary>Occurs when Show pipeline of the panel notice is pressed, so the owner can open the stage rows of the pipeline strip.</summary>
    public event EventHandler? ShowPipelineRequested;

    /// <summary>Gets or sets the control after Close on the title line. The shell leaves it empty.</summary>
    public object? Trailing
    {
        get => TrailingHost.Content;
        set => TrailingHost.Content = value;
    }

    /// <summary>Moves keyboard focus to Close, where focus returns after the close confirmation is dismissed.</summary>
    public void FocusCloseButton() => CloseButton.FocusWhenReady();

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _attached = true;
        _watch.Set(DataContext as AgentSessionViewModel);
        Refresh();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        _attached = false;
        _watch.Set(null);
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_attached)
        {
            _watch.Set(DataContext as AgentSessionViewModel);
        }

        Refresh();
    }

    private void Refresh()
    {
        var session = DataContext as AgentSessionViewModel;
        var row = session?.Row;

        StatusChipView.Text = row?.StatusText;
        StatusChipView.IconKey = row?.IconKey;
        StatusChipView.Tone = row is null ? StatusTone.Secondary : StatusGlyphs.ToneOf(row.Status);
        AutomationProperties.SetName(StatusChipView, AgentPanelViewText.StatusName(row?.StatusText));

        ResumeButton.IsVisible = session is { IsLost: true, IsClosed: false };
        ReopenButton.IsVisible = session is { IsClosed: true };
        CloseButton.IsVisible = session is { IsClosed: false };

        var task = session?.TaskTitle;
        TaskChipButton.IsVisible = !string.IsNullOrEmpty(task);
        TaskChipLabel.Text = AgentPanelViewText.TaskChipText(task);
        AutomationProperties.SetName(TaskChipButton, AgentPanelViewText.TaskChipName(task));

        var graph = session?.GraphId;
        GraphChipView.IsVisible = !string.IsNullOrEmpty(graph);
        GraphChipLabel.Text = AgentPanelViewText.GraphChip(graph);
        AutomationProperties.SetName(GraphChipView, AgentPanelViewText.GraphChip(graph));

        SteeringBadge.IsVisible = session is not null;
        AutomationProperties.SetName(SteeringBadge, session?.SteeringBadgeName ?? AgentPanelCopy.SteeringBadgeName(AgentPanelCopy.SteeringUnavailable));
    }

    private void OnCloseClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not AgentSessionViewModel session)
        {
            return;
        }

        if (session.RequiresCloseConfirmation)
        {
            CloseClicked?.Invoke(this, EventArgs.Empty);
            return;
        }

        session.CloseCommand.Execute(null);
    }

    private void OnTaskChipClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => TaskChipClicked?.Invoke(this, EventArgs.Empty);

    private void OnShowPipelineRequested(object? sender, EventArgs e) => ShowPipelineRequested?.Invoke(this, EventArgs.Empty);
}
