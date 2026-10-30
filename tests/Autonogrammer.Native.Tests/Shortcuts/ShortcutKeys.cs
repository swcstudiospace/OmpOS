using Autonogrammer.Native.Tests.Settings;
using Avalonia.Controls;
using Avalonia.Input;

namespace Autonogrammer.Native.Tests.Shortcuts;

/// <summary>Raises a catalogued gesture on a shown window the way the keyboard does, so a test names the gesture once, in the catalogue.</summary>
internal static class ShortcutKeys
{
    /// <summary>Presses and releases the keys of a gesture on the window that owns the focus, then settles the window.</summary>
    /// <param name="window">The shown window.</param>
    /// <param name="gesture">The gesture.</param>
    internal static void Press(Window window, KeyGesture gesture)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(gesture);

        SettingsTree.Press(window, gesture.Key, PhysicalOf(gesture.Key), (RawInputModifiers)gesture.KeyModifiers);
    }

    private static PhysicalKey PhysicalOf(Key key) => key switch
    {
        Key.Enter => PhysicalKey.Enter,
        Key.Escape => PhysicalKey.Escape,
        Key.F6 => PhysicalKey.F6,
        Key.D1 => PhysicalKey.Digit1,
        Key.D2 => PhysicalKey.Digit2,
        Key.OemComma => PhysicalKey.Comma,
        Key.F => PhysicalKey.F,
        Key.N => PhysicalKey.N,
        Key.T => PhysicalKey.T,
        Key.A => PhysicalKey.A,
        Key.K => PhysicalKey.K,
        Key.S => PhysicalKey.S,
        Key.W => PhysicalKey.W,
        Key.Delete => PhysicalKey.Delete,
        Key.Up => PhysicalKey.ArrowUp,
        Key.Down => PhysicalKey.ArrowDown,
        Key.Left => PhysicalKey.ArrowLeft,
        Key.Right => PhysicalKey.ArrowRight,
        Key.Home => PhysicalKey.Home,
        Key.End => PhysicalKey.End,
        _ => throw new ArgumentOutOfRangeException(nameof(key), key, "The test has no physical key for this key."),
    };
}
