using Autonogrammer.Contracts.Sessions;
using Autonogrammer.Native.Views.Cowork;
using Autonogrammer.Presentation.AgentPanel;
using Autonogrammer.Presentation.Cowork;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Autonogrammer.Native.Views.Shell;

/// <summary>
/// The window content: Code and Cowork stay alive, and a switch changes visibility only. The host owns the switch model and the chrome model.
/// Ctrl+Shift+1 and Ctrl+Shift+2 (Meta as well) are handled here, before the Code view's own keys.
/// </summary>
public sealed partial class SurfaceHost : UserControl, IDisposable
{
    private readonly CoworkSurfaceServices? _services;
    private readonly SurfaceActivityModel? _activity;
    private TopLevel? _keyHost;
    private bool _closed;

    /// <summary>Creates the host over the Code view. Without Cowork services the switch is not in the tree.</summary>
    /// <param name="code">The Code surface. It is not replaced on a switch.</param>
    /// <param name="services">The Cowork services, or null for a Code-only window.</param>
    /// <exception cref="ArgumentNullException"><paramref name="code"/> is null.</exception>
    public SurfaceHost(MainView code, CoworkSurfaceServices? services)
    {
        ArgumentNullException.ThrowIfNull(code);
        InitializeComponent();
        Code = code;
        CodeHost.Content = code;
        Switch = new SurfaceSwitchModel();
        Chrome = new SurfaceChromeModel();
        _services = services;
        if (services is null)
        {
            Switch.SetCoworkAvailable(false);
            return;
        }

        Cowork = new CoworkView();
        Cowork.Attach(services.Shell);
        CoworkHost.Content = Cowork;
        _activity = new SurfaceActivityModel(
            Switch,
            services.ReadCodeRows ?? EmptyRows,
            services.SubscribeCode ?? Noop,
            services.UnsubscribeCode ?? Noop,
            services.Catalog);
        var codeSwitch = new SurfaceSwitchView();
        var coworkSwitch = new SurfaceSwitchView();
        codeSwitch.Attach(Switch, _activity);
        coworkSwitch.Attach(Switch, _activity);
        code.SetSurfaceSwitch(codeSwitch);
        Cowork.SwitchSlot.Content = coworkSwitch;
        code.FindControl<ContentControl>("HeaderEndSlot")!.Content = new SurfaceChromeBar(Chrome);
        Cowork.HeaderSlot.Content = new SurfaceChromeBar(Chrome);
        Switch.Restore(services.SavedSurface, available: true);
        Switch.Changed += OnSwitchChanged;
        ApplyVisibility();
    }

    /// <summary>Gets the Code surface.</summary>
    public MainView Code { get; }

    /// <summary>Gets the Cowork surface, or null when Cowork services were not supplied.</summary>
    public CoworkView? Cowork { get; }

    /// <summary>Gets the switch both bars bind.</summary>
    public SurfaceSwitchModel Switch { get; }

    /// <summary>Gets the header-end entries both bars draw.</summary>
    public SurfaceChromeModel Chrome { get; }

    /// <summary>Closes the Code view and releases the Cowork models.</summary>
    public void Close() => Dispose();

    /// <inheritdoc />
    public void Dispose()
    {
        if (_closed)
        {
            return;
        }

        _closed = true;
        Code.Close();
        _activity?.Dispose();
        _services?.Shell.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _keyHost = TopLevel.GetTopLevel(this);
        _keyHost?.AddHandler(KeyDownEvent, OnKeyDownTunnel, RoutingStrategies.Tunnel);
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _keyHost?.RemoveHandler(KeyDownEvent, OnKeyDownTunnel);
        _keyHost = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnSwitchChanged(object? sender, EventArgs e)
    {
        ApplyVisibility();
        _services?.PersistSurface?.Invoke(Switch.Selected);
    }

    private void ApplyVisibility()
    {
        var cowork = Switch.Selected == SessionSurface.Cowork && Cowork is not null;
        CodeHost.IsVisible = !cowork;
        CoworkHost.IsVisible = cowork;
        Classes.Set("surface-cowork", cowork);
    }

    private void OnKeyDownTunnel(object? sender, KeyEventArgs e)
    {
        if (e.Handled || Cowork is null || e.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            return;
        }

        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        var command = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        if (command && shift && e.Key is Key.D1 or Key.D2)
        {
            e.Handled = true;
            var surface = e.Key == Key.D1 ? SessionSurface.Code : SessionSurface.Cowork;
            Switch.Select(surface);
            FocusSelectedToggle();
            return;
        }

        if (e.Key == Key.F6 && Switch.Selected == SessionSurface.Cowork && !e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            e.Handled = true;
            Cowork.CycleRegions(shift);
        }
    }

    private void FocusSelectedToggle()
    {
        var view = Switch.Selected == SessionSurface.Cowork
            ? Cowork?.SwitchSlot.Content as SurfaceSwitchView
            : Code.FindControl<ContentControl>("SurfaceSwitchSlot")?.Content as SurfaceSwitchView;
        var toggle = Switch.Selected == SessionSurface.Cowork ? view?.CoworkButton : view?.CodeButton;
        if (toggle is not null)
        {
            // The second post runs after the caller's layout pass, when the newly shown toggle can take focus.
            Avalonia.Threading.Dispatcher.UIThread.Post(() => Avalonia.Threading.Dispatcher.UIThread.Post(() => toggle.Focus()));
        }
    }

        private static IReadOnlyList<AgentRow> EmptyRows() => [];

    private static void Noop(EventHandler handler)
    {
    }

    private sealed class SurfaceChromeBar : StackPanel
    {
        public SurfaceChromeBar(SurfaceChromeModel chrome)
        {
            ArgumentNullException.ThrowIfNull(chrome);
            Orientation = Avalonia.Layout.Orientation.Horizontal;
            Spacing = 4;
            chrome.Changed += (_, _) => Rebuild(chrome);
            Rebuild(chrome);
        }

        private void Rebuild(SurfaceChromeModel chrome)
        {
            Children.Clear();
            foreach (var entry in chrome.Entries)
            {
                var button = new Button
                {
                    Content = entry.Label,
                    MinHeight = 32,
                    MinWidth = 32,
                    IsTabStop = true,
                    Focusable = true,
                };
                Avalonia.Automation.AutomationProperties.SetName(button, entry.Label);
                button.Click += (_, _) => entry.Invoke();
                Children.Add(button);
            }
        }
    }
}
