using Autonogrammer.Presentation.Dispatch;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;

namespace Autonogrammer.Native.Views.Dispatch;

/// <summary>
/// The Dispatch page. It binds <see cref="DispatchPageModel"/> for the devices, the status line, the problems line, the command rows and the
/// pairing text. A running command offers Cancel. This desktop's pairing shows its pause switch and one switch per capability. The view raises
/// the user's choice through its callbacks and changes nothing itself; the settings window sends each choice to the relay. Without a local device
/// it says to pair in the desktop app.
/// </summary>
internal partial class DispatchPageView : UserControl
{
    private bool _syncing;

    /// <summary>Initializes a new instance of the <see cref="DispatchPageView"/> class.</summary>
    public DispatchPageView()
    {
        InitializeComponent();
        PageHeading.Text = DispatchCopy.PageHeading;
        PauseToggle.PropertyChanged += OnPausePropertyChanged;
    }

    /// <summary>Gets or sets the callback the user's Cancel raises; the argument is the command identifier.</summary>
    public Action<string>? CancelRequested { get; set; }

    /// <summary>Gets or sets the callback the user's capability switch raises; the argument carries the capability and its new policy.</summary>
    public Action<DispatchCapabilityPolicy>? CapabilityChanged { get; set; }

    /// <summary>Gets or sets the callback the user's pause switch raises; the argument is true to pause Dispatch on this desktop.</summary>
    public Action<bool>? PauseChanged { get; set; }

    /// <inheritdoc/>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is DispatchPageModel page)
        {
            PairText.Text = page.PairThisDevice is { } pair ? pair.StatusText : DispatchCopy.PairInDesktopApp;
            FailureText.Text = string.Join(Environment.NewLine, page.Failures);
            ShowPairControls(page.PairThisDevice);
        }
        else
        {
            PairText.Text = DispatchCopy.PairInDesktopApp;
            FailureText.Text = string.Empty;
            ShowPairControls(null);
        }
    }

    private void ShowPairControls(PairThisDeviceModel? pair)
    {
        _syncing = true;
        try
        {
            PauseToggle.IsVisible = pair is { Paired: true };
            PauseToggle.IsChecked = pair is { Paused: true };
            CapabilityList.Children.Clear();
            if (pair is { Paired: true })
            {
                foreach (var policy in pair.Capabilities)
                {
                    CapabilityList.Children.Add(CapabilityToggle(policy));
                }
            }
        }
        finally
        {
            _syncing = false;
        }
    }

    private ToggleSwitch CapabilityToggle(DispatchCapabilityPolicy policy)
    {
        var label = DispatchCopy.CapabilityLabel(policy.Capability);
        var toggle = new ToggleSwitch { Content = label, IsChecked = policy.Enabled };
        AutomationProperties.SetName(toggle, label);
        toggle.PropertyChanged += (_, args) =>
        {
            if (!_syncing && Equals(args.Property, ToggleButton.IsCheckedProperty))
            {
                CapabilityChanged?.Invoke(policy with { Enabled = toggle.IsChecked == true });
            }
        };
        return toggle;
    }

    private void OnPausePropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs args)
    {
        if (!_syncing && Equals(args.Property, ToggleButton.IsCheckedProperty))
        {
            PauseChanged?.Invoke(PauseToggle.IsChecked == true);
        }
    }

    private void OnCancelClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string commandId })
        {
            CancelRequested?.Invoke(commandId);
            _ = PageHeading.Focus();
        }
    }
}
