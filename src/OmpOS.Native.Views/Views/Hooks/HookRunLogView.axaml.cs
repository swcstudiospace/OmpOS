using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using OmpOS.Presentation.Hooks;

namespace OmpOS.Native.Views.Hooks;

/// <summary>
/// The bounded hook run log: a filter by outcome, a Copy as JSON button and one row per run, newest first. It draws a
/// <see cref="HookRunLogModel"/>; the model has redacted every text, so the view only displays.
/// </summary>
internal partial class HookRunLogView : UserControl
{
    private Action? _copied;

    /// <summary>Creates a view with no model.</summary>
    public HookRunLogView()
    {
        InitializeComponent();
    }

    /// <summary>Gets the filter list.</summary>
    internal ComboBox Filter => FilterBox;

    /// <summary>Gets the Copy as JSON button.</summary>
    internal Button Copy => CopyButton;

    /// <summary>Gets the text shown when there is nothing to list.</summary>
    internal TextBlock Empty => EmptyText;

    /// <summary>Gets the list of run rows.</summary>
    internal ItemsControl Rows => LogRows;

    /// <summary>Sets what happens after the log was copied, for example the page's polite announcement. Null clears it.</summary>
    /// <param name="copied">The callback, or null.</param>
    internal void OnCopied(Action? copied) => _copied = copied;

    private void OnCopyClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not HookRunLogModel model || TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
        {
            return;
        }

        // Copying reports no expected failure the user could act on, so the task is not observed beyond its announcement.
        _ = CopyAsync(clipboard, model.ToJson());
    }

    private async Task CopyAsync(Avalonia.Input.Platform.IClipboard clipboard, string json)
    {
        await clipboard.SetTextAsync(json).ConfigureAwait(true);
        _copied?.Invoke();
    }
}
