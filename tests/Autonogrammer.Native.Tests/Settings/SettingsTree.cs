using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Shouldly;

namespace Autonogrammer.Native.Tests.Settings;

/// <summary>Walks the visual tree of a Settings view the way the accessibility, keyboard and font tests need.</summary>
internal static class SettingsTree
{
    /// <summary>Gets the visible controls of a type under a root, in tree order. Parts of a control template are included.</summary>
    /// <typeparam name="T">The control type.</typeparam>
    /// <param name="root">The root.</param>
    /// <returns>The controls.</returns>
    internal static IReadOnlyList<T> Visible<T>(Visual root)
        where T : Control =>
        [.. root.GetVisualDescendants().OfType<T>().Where(static control => control.IsEffectivelyVisible)];

    /// <summary>Finds the visible control of a type whose accessible name is exactly the given name.</summary>
    /// <typeparam name="T">The control type.</typeparam>
    /// <param name="root">The root.</param>
    /// <param name="name">The accessible name.</param>
    /// <returns>The control.</returns>
    internal static T Named<T>(Visual root, string name)
        where T : Control
    {
        var found = Visible<T>(root).FirstOrDefault(control => string.Equals(AutomationProperties.GetName(control), name, StringComparison.Ordinal));
        return found.ShouldNotBeNull($"no visible {typeof(T).Name} is named \"{name}\"");
    }

    /// <summary>Returns whether a visible control of a type has the accessible name.</summary>
    /// <typeparam name="T">The control type.</typeparam>
    /// <param name="root">The root.</param>
    /// <param name="name">The accessible name.</param>
    /// <returns>True when one exists.</returns>
    internal static bool Has<T>(Visual root, string name)
        where T : Control =>
        Visible<T>(root).Any(control => string.Equals(AutomationProperties.GetName(control), name, StringComparison.Ordinal));

    /// <summary>
    /// Gets the authored controls a Tab press reaches, in keyboard order. Template parts are skipped. A list is one stop: Tab enters
    /// it once, on its selected item or else its first, and the arrow keys move inside it, so its other items are not stops.
    /// </summary>
    /// <param name="root">The root.</param>
    /// <returns>The tab stops.</returns>
    internal static IReadOnlyList<Control> TabStops(Visual root) =>
        [.. root.GetVisualDescendants()
            .OfType<Control>()
            .Where(static control => control.TemplatedParent is null
                && control.Focusable
                && control.IsTabStop
                && control.IsEffectivelyVisible
                && control.IsEffectivelyEnabled
                && (control is not ListBoxItem item || IsListEntry(item)))];

    private static bool IsListEntry(ListBoxItem item)
    {
        var list = item.FindAncestorOfType<ListBox>();
        if (list is null)
        {
            return true;
        }

        var selected = list.SelectedItem is { } value ? list.ContainerFromItem(value) : null;
        return ReferenceEquals(item, selected ?? list.GetVisualDescendants().OfType<ListBoxItem>().FirstOrDefault());
    }

    /// <summary>Gets the visible authored controls that can take focus or announce a change but have no accessible name.</summary>
    /// <param name="root">The root.</param>
    /// <returns>The type names of the unnamed controls.</returns>
    internal static IReadOnlyList<string> Unnamed(Visual root) =>
        [.. root.GetVisualDescendants()
            .OfType<Control>()
            .Where(static control => control.TemplatedParent is null && control.IsEffectivelyVisible)
            .Where(static control => (control.Focusable && control.IsTabStop)
                || AutomationProperties.GetLiveSetting(control) is AutomationLiveSetting.Polite or AutomationLiveSetting.Assertive)
            .Where(static control => string.IsNullOrWhiteSpace(AutomationProperties.GetName(control)))
            .Select(static control => control.GetType().Name + ":" + control.Name)];

    /// <summary>Gets the text of every visible text block, in tree order.</summary>
    /// <param name="root">The root.</param>
    /// <returns>The texts.</returns>
    internal static IReadOnlyList<string> Texts(Visual root) =>
        [.. Visible<TextBlock>(root).Where(static block => block.TemplatedParent is null && !string.IsNullOrEmpty(block.Text)).Select(static block => block.Text!)];

    /// <summary>Gets every string the controls under a root hold that a user or a screen reader could read: text, names, help text and tooltips.</summary>
    /// <param name="root">The root.</param>
    /// <returns>The strings.</returns>
    internal static IReadOnlyList<string> Everything(Visual root)
    {
        var strings = new List<string>(Descriptions(root));
        foreach (var control in root.GetVisualDescendants().OfType<Control>())
        {
            if (control is TextBlock block && block.Text is { Length: > 0 } blockText)
            {
                strings.Add(blockText);
            }

            if (control is TextBox box && box.Text is { Length: > 0 } boxText)
            {
                strings.Add(boxText);
            }
        }

        return strings;
    }

    /// <summary>Gets every accessible name, help text and tooltip under a root: what a screen reader speaks, not what a control displays.</summary>
    /// <param name="root">The root.</param>
    /// <returns>The strings.</returns>
    internal static IReadOnlyList<string> Descriptions(Visual root)
    {
        var strings = new List<string>();
        foreach (var control in root.GetVisualDescendants().OfType<Control>())
        {
            if (AutomationProperties.GetName(control) is { Length: > 0 } name)
            {
                strings.Add(name);
            }

            if (AutomationProperties.GetHelpText(control) is { Length: > 0 } help)
            {
                strings.Add(help);
            }

            if (ToolTip.GetTip(control) is string tip && tip.Length > 0)
            {
                strings.Add(tip);
            }
        }

        return strings;
    }

    /// <summary>Gets the accessible name of the control that has keyboard focus in a window, or null.</summary>
    /// <param name="window">The window.</param>
    /// <returns>The name, or null when nothing has focus.</returns>
    internal static string? FocusedName(TopLevel window) =>
        window.FocusManager?.GetFocusedElement() is Control control ? AutomationProperties.GetName(control) : null;

    /// <summary>Runs pending dispatcher work and, while the window is still open, one layout pass. A key press may close the window.</summary>
    /// <param name="window">The window.</param>
    internal static void SettleSafe(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        if (window.IsVisible)
        {
            ShellHarness.Settle(window);
        }
    }

    /// <summary>Presses one key on a window and lets it settle.</summary>
    /// <param name="window">The window that owns the focus.</param>
    /// <param name="key">The logical key.</param>
    /// <param name="physical">The physical key.</param>
    /// <param name="modifiers">The held modifiers.</param>
    internal static void Press(Window window, Key key, PhysicalKey physical, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPress(key, modifiers, physical, null);
        SettleSafe(window);
    }

    /// <summary>Clicks a button the way Enter on a focused button does, then settles.</summary>
    /// <param name="window">The window.</param>
    /// <param name="button">The button.</param>
    internal static void Click(Window window, Button button)
    {
        _ = button.Focus();
        Press(window, Key.Enter, PhysicalKey.Enter);
    }

    /// <summary>Presses Tab (or Shift+Tab) on a window and lets it settle.</summary>
    /// <param name="window">The window.</param>
    /// <param name="backwards">True for Shift+Tab.</param>
    internal static void PressTab(Window window, bool backwards = false) =>
        Press(window, Key.Tab, PhysicalKey.Tab, backwards ? RawInputModifiers.Shift : RawInputModifiers.None);
}
