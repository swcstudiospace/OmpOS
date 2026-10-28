using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Autonogrammer.Native.Views.Hooks;

/// <summary>The hook run log in a drawer: a Close button above the log. The view holds no state; the host shows it, hides it and handles <see cref="CloseRequested"/>.</summary>
internal partial class HookRunLogDrawerView : UserControl
{
    /// <summary>Creates a drawer with no model.</summary>
    public HookRunLogDrawerView()
    {
        InitializeComponent();
    }

    /// <summary>Raised when the user asks to close the drawer.</summary>
    public event EventHandler? CloseRequested;

    /// <summary>Gets the Close button.</summary>
    internal Button Close => CloseButton;

    /// <summary>Gets the log the drawer shows.</summary>
    internal HookRunLogView LogView => Log;

    private void OnCloseClick(object? sender, RoutedEventArgs e) => CloseRequested?.Invoke(this, EventArgs.Empty);
}
