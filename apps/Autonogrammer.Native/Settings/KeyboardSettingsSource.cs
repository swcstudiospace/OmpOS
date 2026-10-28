using Avalonia.Input;
using Autonogrammer.Native.Views.Shortcuts;
using Autonogrammer.Presentation.Settings;

namespace Autonogrammer.Native.Settings;

/// <summary>
/// The Keyboard page's shortcuts: the entries of <see cref="ShortcutCatalogue"/>, each gesture written out with Avalonia's own
/// <see cref="KeyGesture"/> text. The page is read-only; remapping is omitted by design, so there is no write.
/// </summary>
internal sealed class KeyboardSettingsSource : IKeyboardSettingsSource
{
    private readonly IReadOnlyList<ShortcutEntry> _entries;

    /// <summary>Initializes a new instance of the <see cref="KeyboardSettingsSource"/> class over the shipped catalogue.</summary>
    public KeyboardSettingsSource()
        : this(ShortcutCatalogue.Entries)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="KeyboardSettingsSource"/> class over a list of shortcuts.</summary>
    /// <param name="entries">The shortcuts to list.</param>
    /// <exception cref="ArgumentNullException"><paramref name="entries"/> is null.</exception>
    public KeyboardSettingsSource(IReadOnlyList<ShortcutEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        _entries = entries;
    }

    /// <summary>Writes a gesture as the user reads it: the modifiers, then the key, joined by plus signs.</summary>
    /// <param name="gesture">The gesture.</param>
    /// <returns>The text, for example <c>Ctrl+Shift+T</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="gesture"/> is null.</exception>
    public static string Format(KeyGesture gesture)
    {
        ArgumentNullException.ThrowIfNull(gesture);
        var text = gesture.ToString("g", System.Globalization.CultureInfo.InvariantCulture);
        var keyName = gesture.Key.ToString();
        var keyLabel = gesture.Key switch
        {
            >= Key.D0 and <= Key.D9 => ((int)gesture.Key - (int)Key.D0).ToString(System.Globalization.CultureInfo.InvariantCulture),
            Key.OemComma => ",",
            Key.Enter => "Enter",
            _ => null,
        };

        // Only the key name at the end of Avalonia's text is replaced, so the modifier words stay Avalonia's.
        return keyLabel is not null && text.EndsWith(keyName, StringComparison.Ordinal)
            ? string.Concat(text.AsSpan(0, text.Length - keyName.Length), keyLabel)
            : text;
    }

    /// <inheritdoc/>
    public Task<KeyboardShortcutsSnapshot> LoadAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new KeyboardShortcutsSnapshot([.. _entries.Select(static entry => new ShortcutRow(entry.Action, Format(entry.Gesture), entry.Scope))]));
}
