using Avalonia;
using Avalonia.Controls;
using Autonogrammer.Presentation.Commands.Handlers;

namespace Autonogrammer.Native.Views.Display;

/// <summary>
/// Carries the window's <see cref="DisplaySettings"/> down the visual tree. The shell sets <see cref="SettingsProperty"/> once on the main
/// view; the property is inherited, so every descendant (a code block, an agent row, the thread header) sees the same model, however late it
/// is created. A view that cares about a switch reads it through a watcher and redraws when the switch changes.
/// </summary>
public static class DisplayOptions
{
    /// <summary>The display settings of the window. Inherited by every descendant; null means the defaults of <see cref="DisplaySettingsValues.Defaults"/>.</summary>
    public static readonly AttachedProperty<DisplaySettings?> SettingsProperty =
        AvaloniaProperty.RegisterAttached<Control, DisplaySettings?>("Settings", typeof(DisplayOptions), defaultValue: null, inherits: true);

    /// <summary>Gets the display settings that apply to a control.</summary>
    /// <param name="control">The control.</param>
    /// <returns>The settings, or null when none are set on the control or an ancestor.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    public static DisplaySettings? GetSettings(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);

        return control.GetValue(SettingsProperty);
    }

    /// <summary>Sets the display settings of a control and, by inheritance, of everything below it.</summary>
    /// <param name="control">The control.</param>
    /// <param name="value">The settings, or null for the defaults.</param>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    public static void SetSettings(Control control, DisplaySettings? value)
    {
        ArgumentNullException.ThrowIfNull(control);

        control.SetValue(SettingsProperty, value);
    }
}
