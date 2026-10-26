using Avalonia;
using Avalonia.Controls;
using OmpOS.Presentation.AgentPanel;

namespace OmpOS.Native.Views.AgentPanel;

/// <summary>
/// The panel notice under the thread header: one sentence for degraded stages, no plugins, a lost session or a resumed one, with the
/// buttons that fit the notice (Show pipeline, Resume agent, Dismiss notice). It draws the <see cref="AgentSessionViewModel"/> that is its
/// data context and is hidden while the session has no notice.
/// </summary>
internal partial class PanelNoticeView : UserControl
{
    private readonly ModelWatcher<AgentSessionViewModel> _watch;
    private bool _attached;

    /// <summary>Creates a notice with no model. It is hidden.</summary>
    public PanelNoticeView()
    {
        InitializeComponent();
        _watch = new ModelWatcher<AgentSessionViewModel>(Refresh);
        ShowPipelineButton.Click += OnShowPipelineClick;
        Refresh();
    }

    /// <summary>Occurs when Show pipeline is pressed, so the owner can open the stage rows of the pipeline strip.</summary>
    public event EventHandler? ShowPipelineRequested;

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
        var kind = (DataContext as AgentSessionViewModel)?.NoticeKind ?? PanelNoticeKind.None;
        ShowPipelineButton.IsVisible = kind is PanelNoticeKind.Degraded or PanelNoticeKind.NoPlugins;
        ResumeButton.IsVisible = kind == PanelNoticeKind.Lost;
        DismissButton.IsVisible = kind is PanelNoticeKind.Degraded or PanelNoticeKind.NoPlugins or PanelNoticeKind.Resumed;
        NoticeIcon.IconKey = kind switch
        {
            PanelNoticeKind.Lost => StatusIcon.Question,
            PanelNoticeKind.Resumed => StatusIcon.Info,
            PanelNoticeKind.None => null,
            _ => StatusIcon.Alert,
        };
    }

    private void OnShowPipelineClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        ShowPipelineRequested?.Invoke(this, EventArgs.Empty);
}
