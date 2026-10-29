using Autonogrammer.Native.Views.AgentPanel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Autonogrammer.Native.Tests.AgentPanel.Views;

/// <summary>Shows one Agent Panel view in a plain window and walks it the way the accessibility and keyboard tests need.</summary>
internal static class ViewHarness
{
    /// <summary>The class the shell puts on the window and on the main view under reduced motion.</summary>
    internal const string ReducedMotionClass = "reduced-motion";

    /// <summary>Shows a view as the content of a new window and gives it the layout pass a real window gets.</summary>
    /// <param name="view">The view.</param>
    /// <param name="reducedMotion">Whether the window carries the reduced-motion class.</param>
    /// <param name="width">The window width.</param>
    /// <param name="height">The window height.</param>
    /// <returns>The shown window.</returns>
    internal static Window Show(Control view, bool reducedMotion = false, double width = 480, double height = 640)
    {
        var window = new Window { Content = view, Width = width, Height = height };
        if (reducedMotion)
        {
            window.Classes.Add(ReducedMotionClass);
        }

        window.Show();
        Settle(window);
        return window;
    }

    /// <summary>Runs pending dispatcher work and one layout pass.</summary>
    /// <param name="window">The shown window.</param>
    internal static void Settle(Window window) => ShellHarness.Settle(window);

    /// <summary>Presses and releases one key on the window.</summary>
    /// <param name="window">The window that owns the focus.</param>
    /// <param name="key">The logical key.</param>
    /// <param name="physical">The physical key.</param>
    /// <param name="modifiers">The held modifiers.</param>
    internal static void Press(Window window, Key key, PhysicalKey physical, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPress(key, modifiers, physical, null);
        Settle(window);
    }

    /// <summary>Clicks a button the way Enter on a focused button does, then settles.</summary>
    /// <param name="window">The window.</param>
    /// <param name="button">The button.</param>
    internal static void Click(Window window, Button button)
    {
        button.Focus();
        Press(window, Key.Enter, PhysicalKey.Enter);
    }

    /// <summary>Gets the controls a Tab press can reach in the view: authored, focusable, tab stops, visible and enabled. Template parts are skipped.</summary>
    /// <param name="root">The view.</param>
    /// <returns>The stops in tree order, which is the keyboard order.</returns>
    internal static IReadOnlyList<Control> TabStops(Control root) =>
        [.. root.GetVisualDescendants()
            .OfType<Control>()
            .Where(static control => control.TemplatedParent is null
                && control.Focusable
                && control.IsTabStop
                && control.IsEffectivelyVisible
                && control.IsEffectivelyEnabled)];

    /// <summary>Gets the accessible names of the tab stops in keyboard order.</summary>
    /// <param name="root">The view.</param>
    /// <returns>The names.</returns>
    internal static IReadOnlyList<string> TabOrder(Control root) =>
        [.. TabStops(root).Select(static control => AutomationProperties.GetName(control) ?? string.Empty)];

    /// <summary>Gets the authored controls that are a tab stop or a live region and have no accessible name, as their type names.</summary>
    /// <param name="root">The view.</param>
    /// <returns>The unnamed controls. Empty when the view passes the walk.</returns>
    internal static IReadOnlyList<string> Unnamed(Control root) =>
        [.. root.GetVisualDescendants()
            .OfType<Control>()
            .Where(static control => control.TemplatedParent is null)
            .Where(static control => (control.Focusable && control.IsTabStop && control.IsEffectivelyVisible)
                || AutomationProperties.GetLiveSetting(control) is AutomationLiveSetting.Polite or AutomationLiveSetting.Assertive)
            .Where(static control => string.IsNullOrWhiteSpace(AutomationProperties.GetName(control)))
            .Select(static control => control.GetType().Name)];

    /// <summary>Counts the glyphs under the view that carry the pulse class.</summary>
    /// <param name="root">The view.</param>
    /// <returns>The number of pulsing glyphs.</returns>
    internal static int PulsingIcons(Control root) =>
        root.GetVisualDescendants().OfType<StageIcon>().Count(static icon => icon.IsPulsing);

    /// <summary>Gets the visible text of every text block under the view, in tree order.</summary>
    /// <param name="root">The view.</param>
    /// <returns>The texts of the visible text blocks that carry text.</returns>
    internal static IReadOnlyList<string> VisibleTexts(Control root) =>
        [.. root.GetVisualDescendants()
            .OfType<TextBlock>()
            .Where(static block => block.TemplatedParent is null && block.IsEffectivelyVisible && !string.IsNullOrEmpty(block.Text))
            .Select(static block => block.Text!)];

    /// <summary>Finds a named control inside a view.</summary>
    /// <typeparam name="T">The control type.</typeparam>
    /// <param name="root">The view.</param>
    /// <param name="name">The <c>x:Name</c>.</param>
    /// <returns>The control.</returns>
    internal static T Find<T>(Control root, string name)
        where T : Control =>
        root.GetVisualDescendants().OfType<T>().First(control => string.Equals(control.Name, name, StringComparison.Ordinal));

    /// <summary>Drains queued dispatcher jobs, for example a deferred focus.</summary>
    internal static void Drain() => Dispatcher.UIThread.RunJobs();

    /// <summary>Gets the mono face of the <c>Autonogrammer.Font.Mono</c> token, which ids and fix commands are drawn in.</summary>
    /// <returns>The font family.</returns>
    internal static Avalonia.Media.FontFamily MonoFont() =>
        (Avalonia.Media.FontFamily)Application.Current!.FindResource("Autonogrammer.Font.Mono")!;
}
