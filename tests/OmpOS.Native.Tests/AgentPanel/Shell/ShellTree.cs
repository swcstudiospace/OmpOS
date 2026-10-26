using Avalonia.Controls;
using Avalonia.VisualTree;

namespace OmpOS.Native.Tests.AgentPanel.Shell;

/// <summary>Small helpers over the visual tree of a shell under test.</summary>
internal static class ShellTree
{
    /// <summary>Finds the first named control of type <typeparamref name="T"/> under <paramref name="root"/>, in any name scope.</summary>
    /// <typeparam name="T">The control type.</typeparam>
    /// <param name="root">Where to search.</param>
    /// <param name="name">The control's <c>x:Name</c>.</param>
    /// <returns>The control, or null.</returns>
    public static T? Named<T>(Control root, string name)
        where T : Control =>
        root.GetVisualDescendants().OfType<T>().FirstOrDefault(control => string.Equals(control.Name, name, StringComparison.Ordinal));

    /// <summary>
    /// Says whether a control is drawn: it is in the visual tree and effectively visible. A control under a collapsed scroll viewer has no
    /// visual parent until the viewer is first shown, and Avalonia reports such a control as effectively visible.
    /// </summary>
    /// <param name="control">The control to test.</param>
    /// <returns>True when the control is in the visual tree and effectively visible.</returns>
    public static bool IsShown(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);

        return control.IsAttachedToVisualTree() && control.IsEffectivelyVisible;
    }

    /// <summary>Finds the first named control of type <typeparamref name="T"/> under <paramref name="root"/> that is effectively visible.</summary>
    /// <typeparam name="T">The control type.</typeparam>
    /// <param name="root">Where to search.</param>
    /// <param name="name">The control's <c>x:Name</c>.</param>
    /// <returns>The control, or null when none is visible.</returns>
    public static T? NamedVisible<T>(Control root, string name)
        where T : Control =>
        root.GetVisualDescendants().OfType<T>().FirstOrDefault(control => string.Equals(control.Name, name, StringComparison.Ordinal) && control.IsEffectivelyVisible);

    /// <summary>Gets the text of every <see cref="TextBlock"/> that is effectively visible under <paramref name="root"/>.</summary>
    /// <param name="root">Where to search.</param>
    /// <returns>The visible texts, in visual order.</returns>
    public static IReadOnlyList<string> VisibleTexts(Control root) =>
        [.. root.GetVisualDescendants()
            .OfType<TextBlock>()
            .Where(text => text.IsEffectivelyVisible && !string.IsNullOrEmpty(text.Text))
            .Select(text => text.Text!)];
}
