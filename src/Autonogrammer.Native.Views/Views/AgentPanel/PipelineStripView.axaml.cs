using Autonogrammer.Presentation.AgentPanel;
using Avalonia;
using Avalonia.Controls;

namespace Autonogrammer.Native.Views.AgentPanel;

/// <summary>
/// The plugin connection row under the thread header: one button of pills that opens a flyout with the stage rows of every source, and the
/// <c>Latest:</c> line that carries the timeline's throttled announcement in a polite live region. It draws the
/// <see cref="StageTimelineModel"/> that is its data context.
/// </summary>
internal partial class PipelineStripView : UserControl
{
    private readonly ModelWatcher<StageTimelineModel> _watch;
    private readonly Flyout _detailsFlyout;
    private bool _attached;

    /// <summary>Creates a strip with no model.</summary>
    public PipelineStripView()
    {
        InitializeComponent();
        _watch = new ModelWatcher<StageTimelineModel>(Refresh);
        _detailsFlyout = (Flyout)PipelineStrip.Flyout!;
        _detailsFlyout.Opening += OnFlyoutOpening;
        Refresh();
    }

    /// <summary>Gets or sets the run summary sentence shown above the stage rows in the flyout, or null or empty for none.</summary>
    public string? RunSummary
    {
        get => Details.RunSummary;
        set => Details.RunSummary = value;
    }

    /// <summary>Opens the flyout with the stage rows of every source, for example when the panel notice asks to show the pipeline.</summary>
    public void ShowDetails() => _detailsFlyout.ShowAt(PipelineStrip);

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _attached = true;
        _watch.Set(DataContext as StageTimelineModel);
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
            _watch.Set(DataContext as StageTimelineModel);
        }

        Refresh();
    }

    private void Refresh()
    {
        var timeline = DataContext as StageTimelineModel;
        Details.DataContext = timeline;
        PipelineStrip.IsVisible = timeline is not null;
        StripFrame.IsVisible = timeline is not null;
        var announcement = timeline?.StatusAnnouncement;
        var text = string.IsNullOrWhiteSpace(announcement) ? null : AgentPanelViewText.LatestLine(announcement);
        if (!string.Equals(Announcement.Text, text, StringComparison.Ordinal))
        {
            Announcement.Text = text;
        }

        Announcement.IsVisible = text is not null;
    }

    private void OnFlyoutOpening(object? sender, EventArgs e) => Details.Classes.Set(MotionProbe.ReducedMotionClass, MotionProbe.IsReduced(this));
}
