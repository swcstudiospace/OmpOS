using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Autonogrammer.Native.Views.Export;
using Xunit;

namespace Autonogrammer.Native.Tests.Export;

/// <summary>Shared helpers for the export tests: shows the export view, finds its controls and sends keys.</summary>
internal static class ExportHarness
{
    /// <summary>Gets the cancellation token of the running test.</summary>
    public static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>Shows the export view in an active window, and gives it the layout passes a real window gets.</summary>
    /// <param name="view">The view to show.</param>
    /// <returns>The shown window.</returns>
    public static ExportWindow Show(TranscriptExportView view)
    {
        var window = new ExportWindow { Width = 700, Height = 700, Content = view };
        window.Show();
        window.Activate();
        Settle(window);
        return window;
    }

    /// <summary>Runs pending dispatcher work and several layout passes.</summary>
    /// <param name="window">The window to settle.</param>
    public static void Settle(Window window)
    {
        for (var pass = 0; pass < 5; pass++)
        {
            Dispatcher.UIThread.RunJobs();
            window.GetLayoutManager()?.ExecuteLayoutPass();
        }

        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Presses and releases one key on the window, as the keyboard would, and settles the layout.</summary>
    /// <param name="window">The shown window that owns the focus.</param>
    /// <param name="key">The logical key.</param>
    /// <param name="physical">The physical key.</param>
    public static void Press(Window window, Key key, PhysicalKey physical)
    {
        window.KeyPress(key, default, physical, null);
        Settle(window);
    }

    /// <summary>Raises the click of a button, as pressing it does.</summary>
    /// <param name="button">The button to click.</param>
    public static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    /// <summary>Gets the export path text box.</summary>
    public static TextBox PathInput(TranscriptExportView view) => view.FindControl<TextBox>("PathInput")!;

    /// <summary>Gets the Export button.</summary>
    public static Button ExportButton(TranscriptExportView view) => view.FindControl<Button>("ExportButton")!;

    /// <summary>Gets the export status line.</summary>
    public static TextBlock Status(TranscriptExportView view) => view.FindControl<TextBlock>("StatusText")!;

    /// <summary>Gets the usage period selector.</summary>
    public static ComboBox PeriodSelector(TranscriptExportView view) => view.FindControl<ComboBox>("PeriodSelector")!;

    /// <summary>Gets the usage list.</summary>
    public static ListBox UsageList(TranscriptExportView view) => view.FindControl<ListBox>("UsageList")!;

    /// <summary>Gets the line shown when there is no usage.</summary>
    public static TextBlock UsageEmptyText(TranscriptExportView view) => view.FindControl<TextBlock>("UsageEmptyText")!;

    /// <summary>Gets the text of every cell the usage list currently shows.</summary>
    public static IReadOnlyList<string> UsageCells(TranscriptExportView view) =>
        UsageList(view).GetVisualDescendants().OfType<TextBlock>().Select(cell => cell.Text ?? string.Empty).ToList();

    /// <summary>True when keyboard focus is on the control or one of its descendants. A list focuses its selected item, not itself.</summary>
    /// <param name="window">The shown window that owns the focus.</param>
    /// <param name="control">The control that should hold the focus, directly or through a descendant.</param>
    public static bool HasKeyboardFocusWithin(Window window, Visual control)
    {
        for (var current = window.FocusManager?.GetFocusedElement() as Visual; current is not null; current = current.GetVisualParent())
        {
            if (ReferenceEquals(current, control))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Waits, for at most ten seconds, until the condition holds.</summary>
    /// <param name="window">The shown window to settle while waiting.</param>
    /// <param name="condition">The condition to wait for.</param>
    /// <param name="because">What the wait is for, named in the timeout.</param>
    /// <exception cref="TimeoutException">The condition did not hold in time.</exception>
    public static async Task WaitForAsync(Window window, Func<bool> condition, string because)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException($"Timed out waiting for {because}.");
            }

            Settle(window);
            await Task.Delay(10, Token).ConfigureAwait(true);
        }

        Settle(window);
    }
}
