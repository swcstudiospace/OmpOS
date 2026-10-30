using Avalonia.Controls;

namespace Autonogrammer.Native.Views.Settings;

/// <summary>
/// The Keyboard page: the shortcuts the shell handles, grouped by scope, one row each with what it does and its keys. The page has nothing to
/// edit; remapping is left out on purpose.
/// </summary>
internal partial class KeyboardPageView : SettingsPageViewBase
{
    /// <summary>Creates a view with no page.</summary>
    public KeyboardPageView()
    {
        InitializeComponent();
        Start();
    }

    /// <summary>Gets the text shown when the source reported no shortcut.</summary>
    internal TextBlock Empty => EmptyText;

    /// <summary>Gets the list of scope groups.</summary>
    internal ItemsControl Groups => ShortcutGroups;

    /// <inheritdoc />
    internal override PageStatusView Status => StateView;

    /// <inheritdoc />
    protected override Control ReadyHost => ReadyPanel;
}
