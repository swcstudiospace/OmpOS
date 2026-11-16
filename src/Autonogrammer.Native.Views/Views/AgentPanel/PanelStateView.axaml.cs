using Autonogrammer.Presentation.AgentPanel;
using Avalonia;
using Avalonia.Controls;

namespace Autonogrammer.Native.Views.AgentPanel;

/// <summary>
/// The thread block of the panel states Loading (static text) and Error (the assertive Panel error with Retry loading). It draws the
/// <see cref="AgentPanelViewModel"/> that is its data context and hides itself while the panel is Empty, Ready or has no model: the Code landing
/// takes the empty state, and a ready panel draws nothing here.
/// </summary>
internal partial class PanelStateView : UserControl
{
    private readonly ModelWatcher<AgentPanelViewModel> _watch;
    private bool _attached;

    /// <summary>Creates a block with no model. It is hidden.</summary>
    public PanelStateView()
    {
        InitializeComponent();
        _watch = new ModelWatcher<AgentPanelViewModel>(Refresh);
        Refresh();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _attached = true;
        _watch.Set(DataContext as AgentPanelViewModel);
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
            _watch.Set(DataContext as AgentPanelViewModel);
        }

        Refresh();
    }

    private void Refresh()
    {
        var state = (DataContext as AgentPanelViewModel)?.State;
        PanelError.IsVisible = state == PanelState.Error;
        LoadingBlock.IsVisible = state == PanelState.Loading;
        IsVisible = state is PanelState.Error or PanelState.Loading;
    }
}
