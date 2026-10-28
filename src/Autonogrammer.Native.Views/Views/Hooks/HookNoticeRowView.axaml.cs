using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Autonogrammer.Native.Views.Hooks;

/// <summary>
/// A hook notice in the thread: a one-line denial or continuation, or a collapsible Hook context row. A notice that asks for approval offers a
/// Review button; the host handles <see cref="ReviewRequested"/> and opens the Hooks page.
/// </summary>
internal partial class HookNoticeRowView : UserControl
{
    /// <summary>Creates a view with no notice.</summary>
    public HookNoticeRowView()
    {
        InitializeComponent();
    }

    /// <summary>Raised when the user asks to review the hook that needs approval.</summary>
    public event EventHandler? ReviewRequested;

    /// <summary>Gets the line of a notice that is not collapsible.</summary>
    internal TextBlock Line => NoticeText;

    /// <summary>Gets the expander of a context row.</summary>
    internal Expander Context => ContextExpander;

    /// <summary>Gets the Review button.</summary>
    internal Button Review => ReviewButton;

    private void OnReviewClick(object? sender, RoutedEventArgs e) => ReviewRequested?.Invoke(this, EventArgs.Empty);
}
