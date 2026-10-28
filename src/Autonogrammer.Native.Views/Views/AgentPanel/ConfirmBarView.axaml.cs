using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Autonogrammer.Presentation.AgentPanel;

namespace Autonogrammer.Native.Views.AgentPanel;

/// <summary>
/// The stop and close confirmation bars of one agent. The owner opens a bar with <see cref="OpenStop"/> or <see cref="OpenClose"/> and
/// acts on <see cref="StopConfirmed"/> and <see cref="CloseConfirmed"/>; Esc and the Keep buttons dismiss the bar and raise
/// <see cref="Dismissed"/> so the owner can return focus. The stop bar closes by itself when the run ends. A change of data context closes
/// both bars without an event, because the bars belong to the session that was shown.
/// </summary>
internal partial class ConfirmBarView : UserControl
{
    private readonly ModelWatcher<SteeringInputModel> _steeringWatch;
    private bool _attached;

    /// <summary>Creates the view with both bars closed.</summary>
    public ConfirmBarView()
    {
        InitializeComponent();
        _steeringWatch = new ModelWatcher<SteeringInputModel>(OnSteeringChanged);
        StopAgentButton.Click += OnStopAgentClick;
        KeepRunningButton.Click += OnKeepRunningClick;
        CloseAgentButton.Click += OnCloseAgentClick;
        KeepAgentOpenButton.Click += OnKeepAgentOpenClick;
        StopConfirmBar.AddHandler(KeyDownEvent, OnStopBarKeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        CloseConfirmBar.AddHandler(KeyDownEvent, OnCloseBarKeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);
    }

    /// <summary>Occurs when Stop agent is pressed. The bar closes; the owner stops the run.</summary>
    public event EventHandler? StopConfirmed;

    /// <summary>Occurs when Close agent is pressed. The bar closes; the owner closes the agent.</summary>
    public event EventHandler? CloseConfirmed;

    /// <summary>Occurs when a bar was dismissed by Esc, a Keep button or the end of the run. The argument says which bar.</summary>
    public event EventHandler<ConfirmBarDismissedEventArgs>? Dismissed;

    /// <summary>Gets whether the stop confirmation bar is open.</summary>
    public bool IsStopOpen => StopConfirmBar.IsVisible;

    /// <summary>Gets whether the close confirmation bar is open.</summary>
    public bool IsCloseOpen => CloseConfirmBar.IsVisible;

    /// <summary>Opens the stop confirmation bar and moves focus to Stop agent.</summary>
    public void OpenStop()
    {
        CloseConfirmBar.IsVisible = false;
        StopConfirmBar.IsVisible = true;
        FocusSoon(StopAgentButton);
    }

    /// <summary>Opens the close confirmation bar and moves focus to Keep agent open, the safe choice.</summary>
    public void OpenClose()
    {
        StopConfirmBar.IsVisible = false;
        CloseConfirmBar.IsVisible = true;
        FocusSoon(KeepAgentOpenButton);
    }

    /// <summary>Closes both bars without raising an event.</summary>
    public void Dismiss()
    {
        StopConfirmBar.IsVisible = false;
        CloseConfirmBar.IsVisible = false;
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _attached = true;
        _steeringWatch.Set((DataContext as AgentSessionViewModel)?.Steering);
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        _attached = false;
        _steeringWatch.Set(null);
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        Dismiss();
        if (_attached)
        {
            _steeringWatch.Set((DataContext as AgentSessionViewModel)?.Steering);
        }
    }

    private static void FocusSoon(Button button)
    {
        if (!button.Focus())
        {
            Dispatcher.UIThread.Post(() => button.Focus(), DispatcherPriority.Input);
        }
    }

    private void OnSteeringChanged()
    {
        if (IsStopOpen && _steeringWatch.Model is { IsRunActive: false })
        {
            Dismiss();
            Dismissed?.Invoke(this, new ConfirmBarDismissedEventArgs(ConfirmBarKind.Stop));
        }
    }

    private void OnStopAgentClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        StopConfirmBar.IsVisible = false;
        StopConfirmed?.Invoke(this, EventArgs.Empty);
    }

    private void OnKeepRunningClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => DismissBar(ConfirmBarKind.Stop);

    private void OnCloseAgentClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        CloseConfirmBar.IsVisible = false;
        CloseConfirmed?.Invoke(this, EventArgs.Empty);
    }

    private void OnKeepAgentOpenClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => DismissBar(ConfirmBarKind.Close);

    private void OnStopBarKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            DismissBar(ConfirmBarKind.Stop);
        }
    }

    private void OnCloseBarKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            DismissBar(ConfirmBarKind.Close);
        }
    }

    private void DismissBar(ConfirmBarKind kind)
    {
        Dismiss();
        Dismissed?.Invoke(this, new ConfirmBarDismissedEventArgs(kind));
    }
}
