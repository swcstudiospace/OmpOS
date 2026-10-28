using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Autonogrammer.Native.Views.AgentPanel;
using Autonogrammer.Presentation.Composer;

namespace Autonogrammer.Native.Views;

/// <summary>
/// The context ring beside the composer chips. It draws the bound <see cref="ContextRingModel"/>: an arc of the filled share of the window, the percentage, and the
/// state in words from 70 percent on. It is hidden until the model has usage to show. The tooltip and the automation name carry the full sentence of the model.
/// </summary>
internal partial class ContextRingView : UserControl
{
    private const double MaxSweep = 359.9;

    private readonly ModelWatcher<ContextRingModel> _watch;

    /// <summary>Creates the ring with no model. It stays hidden until a model with usage is bound.</summary>
    public ContextRingView()
    {
        InitializeComponent();
        _watch = new ModelWatcher<ContextRingModel>(Refresh);
        Refresh();
    }

    /// <summary>Gets the model the ring draws, or null.</summary>
    internal ContextRingModel? Model => DataContext as ContextRingModel;

    /// <summary>Gets the text beside the arc: the percentage, or <c>?</c> when the window size is unknown.</summary>
    internal TextBlock Percent => PercentText;

    /// <summary>Gets the state in words, visible once the window is getting full.</summary>
    internal TextBlock State => StateText;

    /// <summary>Gets the arc that shows the filled share.</summary>
    internal Avalonia.Controls.Shapes.Arc Arc => Progress;

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _watch.Set(Model);
        Refresh();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _watch.Set(null);
        base.OnDetachedFromVisualTree(e);
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (VisualRoot is not null)
        {
            _watch.Set(Model);
        }

        Refresh();
    }

    private void Refresh()
    {
        var model = Model;
        IsVisible = model is { Text.Length: > 0 };
        if (model is null)
        {
            return;
        }

        Progress.SweepAngle = Math.Min(model.SweepDegrees, MaxSweep);
        PercentText.Text = model.Text;
        var state = model.ProjectedState;
        Progress.Classes.Set("ring-warning", state == ContextRingState.Warning);
        Progress.Classes.Set("ring-critical", state == ContextRingState.Critical);
        var showState = state is ContextRingState.Warning or ContextRingState.Critical or ContextRingState.Unknown;
        StateText.IsVisible = showState;
        StateText.Text = showState ? ContextRingCopy.StateWord(state) : string.Empty;
        AutomationProperties.SetName(this, model.AutomationName);
        ToolTip.SetTip(RingHost, model.Tooltip);
    }
}
