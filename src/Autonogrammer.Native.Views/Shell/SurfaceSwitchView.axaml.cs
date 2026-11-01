using Autonogrammer.Contracts.Sessions;
using Autonogrammer.Presentation.Cowork;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;

namespace Autonogrammer.Native.Views.Shell;

/// <summary>
/// The Code | Cowork segmented control and the polite chip for the hidden surface. Selection is the toggle state.
/// </summary>
public partial class SurfaceSwitchView : UserControl
{
    private SurfaceSwitchModel? _switch;
    private SurfaceActivityModel? _activity;

    /// <summary>Creates the control with no model.</summary>
    public SurfaceSwitchView()
    {
        InitializeComponent();
    }

    /// <summary>Gets the Code toggle.</summary>
    public ToggleButton CodeButton => CodeToggle;

    /// <summary>Gets the Cowork toggle.</summary>
    public ToggleButton CoworkButton => CoworkToggle;

    /// <summary>Gets the hidden-surface chip.</summary>
    public TextBlock Chip => HiddenSurfaceChip;

    /// <summary>Binds the control to the switch and the activity chip.</summary>
    /// <param name="surface">The switch both bars share.</param>
    /// <param name="activity">The chip source, or null when there is no activity model yet.</param>
    /// <exception cref="ArgumentNullException"><paramref name="surface"/> is null.</exception>
    public void Attach(SurfaceSwitchModel surface, SurfaceActivityModel? activity)
    {
        ArgumentNullException.ThrowIfNull(surface);
        Detach();
        _switch = surface;
        _activity = activity;
        surface.Changed += OnChanged;
        if (activity is not null)
        {
            activity.Changed += OnChanged;
        }

        Apply();
    }

    private void Detach()
    {
        if (_switch is not null)
        {
            _switch.Changed -= OnChanged;
        }

        if (_activity is not null)
        {
            _activity.Changed -= OnChanged;
        }
    }

    private void OnChanged(object? sender, EventArgs e) => Apply();

    private void Apply()
    {
        if (_switch is null)
        {
            return;
        }

        var cowork = _switch.Selected == SessionSurface.Cowork;
        CodeToggle.IsChecked = !cowork;
        CoworkToggle.IsChecked = cowork;
        var text = _activity?.ChipText ?? string.Empty;
        HiddenSurfaceChip.IsVisible = text.Length > 0;
        HiddenSurfaceChip.Text = text;
        AutomationProperties.SetName(HiddenSurfaceChip, text);
    }

    private void OnCodeClick(object? sender, RoutedEventArgs e) => _switch?.Select(SessionSurface.Code);

    private void OnCoworkClick(object? sender, RoutedEventArgs e) => _switch?.Select(SessionSurface.Cowork);
}
