using Avalonia.Controls;

namespace OmpOS.Native.Views.Settings;

/// <summary>
/// The Plugins page: the four plugins connected to every omp process, one card each with its role, availability and version. The page has
/// nothing to edit; there is deliberately no switch to turn a plugin off.
/// </summary>
internal partial class PluginsPageView : SettingsPageViewBase
{
    /// <summary>Creates a view with no page.</summary>
    public PluginsPageView()
    {
        InitializeComponent();
        Start();
    }

    /// <summary>Gets the text shown when the source reported no plugin.</summary>
    internal TextBlock Empty => EmptyText;

    /// <inheritdoc />
    internal override PageStatusView Status => StateView;

    /// <inheritdoc />
    protected override Control ReadyHost => ReadyPanel;
}
