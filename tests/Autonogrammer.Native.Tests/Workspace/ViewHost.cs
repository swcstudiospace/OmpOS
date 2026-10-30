using Autonogrammer.Native.Views.Search;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using Shouldly;

namespace Autonogrammer.Native.Tests.Workspace;

/// <summary>
/// Hosts a view in a shown window for the headless tests, presses keys on it, lists the keyboard stops that have no name,
/// asserts the text a view shows, and waits for a search to publish its outcome.
/// </summary>
internal static class ViewHost
{
    /// <summary>Shows a view in a new window and gives it the layout pass a real window gets.</summary>
    /// <param name="view">The view to show.</param>
    /// <returns>The shown window.</returns>
    public static Window Show(Control view)
    {
        var window = new Window
        {
            Content = view,
            Width = 900,
            Height = 1200,
        };
        window.Show();
        ShellHarness.Settle(window);
        return window;
    }

    /// <summary>Presses one key down on a window, as the keyboard would, then settles the layout. The key is not released; see <see cref="PressAndRelease"/>.</summary>
    /// <param name="window">The shown window that owns the focus.</param>
    /// <param name="key">The logical key.</param>
    /// <param name="physical">The physical key.</param>
    public static void Press(Window window, Key key, PhysicalKey physical)
    {
        window.KeyPress(key, default, physical, null);
        ShellHarness.Settle(window);
    }

    /// <summary>
    /// Presses one key down and releases it, as the keyboard does, then settles the layout. Some controls act on the key up,
    /// so a press alone does not reach them: <see cref="Press"/> raises only the key down.
    /// </summary>
    /// <param name="window">The shown window that owns the focus.</param>
    /// <param name="key">The logical key.</param>
    /// <param name="physical">The physical key.</param>
    public static void PressAndRelease(Window window, Key key, PhysicalKey physical)
    {
        window.KeyPress(key, default, physical, null);
        window.KeyRelease(key, default, physical, null);
        ShellHarness.Settle(window);
    }

    /// <summary>Lists the focusable tab stops under a root that have no accessible name. An empty list means every stop is named.</summary>
    /// <param name="root">The root to scan.</param>
    /// <returns>The type names of the unnamed stops.</returns>
    public static IReadOnlyList<string> UnnamedTabStops(Control root) =>
        root.GetVisualDescendants()
            .OfType<Control>()
            .Where(static control => control.TemplatedParent is null)
            .Where(static control => control.Focusable && control.IsTabStop)
            .Where(static control => string.IsNullOrWhiteSpace(AutomationProperties.GetName(control)))
            .Select(static control => control.GetType().Name)
            .ToList();

    /// <summary>Asserts that the shown text contains a text, compared ordinally.</summary>
    /// <param name="shown">The text the view shows.</param>
    /// <param name="text">The text that must be shown.</param>
    public static void ShouldShowText(this IReadOnlyList<string> shown, string text) =>
        shown.Contains(text, StringComparer.Ordinal).ShouldBeTrue("The view should show '" + text + "'.");

    /// <summary>Asserts that the shown text does not contain a text, compared ordinally.</summary>
    /// <param name="shown">The text the view shows.</param>
    /// <param name="text">The text that must not be shown.</param>
    public static void ShouldNotShowText(this IReadOnlyList<string> shown, string text) =>
        shown.Contains(text, StringComparer.Ordinal).ShouldBeFalse("The view should not show '" + text + "'.");

    /// <summary>Completes once the search model has no search running. Returns at once when none is running.</summary>
    /// <param name="model">The search model to wait for.</param>
    public static async Task WaitForSearchAsync(WorkspaceSearchModel model)
    {
        if (!model.IsSearching)
        {
            return;
        }

        var settled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnChanged(object? sender, EventArgs e)
        {
            if (!model.IsSearching)
            {
                settled.TrySetResult();
            }
        }

        model.Changed += OnChanged;
        try
        {
            await settled.Task.ConfigureAwait(true);
        }
        finally
        {
            model.Changed -= OnChanged;
        }
    }
}
