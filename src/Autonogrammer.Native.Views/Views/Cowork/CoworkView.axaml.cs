using Autonogrammer.Presentation.Cowork;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Autonogrammer.Native.Views.Cowork;

/// <summary>
/// The Cowork surface: the same top bar slots as Code, the rail, the conversation host and the dock. F6 cycles those three regions.
/// The conversation host stays empty until a later phase fills it.
/// </summary>
public partial class CoworkView : UserControl
{
    private TopLevel? _keyHost;
    private int _region;

    /// <summary>Creates the surface and its regions.</summary>
    public CoworkView()
    {
        InitializeComponent();
        Rail = new CoworkRailView();
        Dock = new CoworkDockHost();
        RailHost.Content = Rail;
        DockHost.Content = Dock;
    }

    /// <summary>Gets the rail.</summary>
    public CoworkRailView Rail { get; }

    /// <summary>Gets the dock.</summary>
    public CoworkDockHost Dock { get; }

    /// <summary>Gets the conversation host. It is the F6 target and starts empty.</summary>
    public ContentControl Conversation => ConversationHost;

    /// <summary>Gets the switch slot in the top bar.</summary>
    public ContentControl SwitchSlot => SurfaceSwitchSlot;

    /// <summary>Gets the header-end slot in the top bar.</summary>
    public ContentControl HeaderSlot => HeaderEndSlot;

    /// <summary>Binds the regions to the shell.</summary>
    /// <param name="shell">The Cowork shell.</param>
    /// <exception cref="ArgumentNullException"><paramref name="shell"/> is null.</exception>
    public void Attach(CoworkShellModel shell)
    {
        ArgumentNullException.ThrowIfNull(shell);
        Rail.Attach(shell.Rail);
        Dock.Attach(shell);
        Dock.IsVisible = shell.IsDockOpen;
        shell.Changed += (_, _) => Dock.IsVisible = shell.IsDockOpen;
    }

    /// <summary>Moves focus to the next or previous region. Rail, conversation, then dock.</summary>
    /// <param name="backwards">True for Shift+F6.</param>
    public void CycleRegions(bool backwards)
    {
        var regions = Regions();
        if (regions.Count == 0)
        {
            return;
        }

        _region = (_region + (backwards ? regions.Count - 1 : 1)) % regions.Count;
        regions[_region].Focus();
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

    private void OnKeyDownTunnel(object? sender, KeyEventArgs e)
    {
        if (e.Handled || e.Key != Key.F6 || e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            return;
        }

        e.Handled = true;
        CycleRegions(e.KeyModifiers.HasFlag(KeyModifiers.Shift));
    }

    private void OnSkipClick(object? sender, RoutedEventArgs e)
    {
        ConversationHost.Focus();
        _region = 1;
    }

    private List<Control> Regions()
    {
        var regions = new List<Control> { Rail, ConversationHost };
        if (Dock.IsVisible)
        {
            regions.Add(Dock);
        }

        return regions;
    }
}
