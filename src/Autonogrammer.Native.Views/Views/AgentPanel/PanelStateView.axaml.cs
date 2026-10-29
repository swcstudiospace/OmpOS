using Autonogrammer.Presentation.AgentPanel;
using Avalonia;
using Avalonia.Controls;

namespace Autonogrammer.Native.Views.AgentPanel;

/// <summary>
/// The thread block of the panel states Empty (the first-run copy and the Start first agent button), Loading (static text) and Error (the
/// assertive Panel error with Retry loading). It draws the <see cref="AgentPanelViewModel"/> that is its data context and hides itself
/// while the panel is Ready or has no model.
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
        StartFirstAgentButton.Click += OnStartClick;
        Refresh();
    }

    /// <summary>Occurs after the New agent command finished, so the owner can move focus to the new session's prompt.</summary>
    public event EventHandler? NewAgentCompleted;

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
        EmptyBlock.IsVisible = state == PanelState.Empty;
        IsVisible = state is PanelState.Error or PanelState.Loading or PanelState.Empty;
    }

    private void OnStartClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => _ = StartAsync();

    private async Task StartAsync()
    {
        if (DataContext is not AgentPanelViewModel panel)
        {
            return;
        }

        await panel.NewSessionCommand.ExecuteAsync(null).ConfigureAwait(true);
        NewAgentCompleted?.Invoke(this, EventArgs.Empty);
    }
}
