using Autonogrammer.Presentation.AgentPanel;

namespace Autonogrammer.Presentation.Cowork;

/// <summary>
/// One header-end entry. The host constructs it; it is never page or tool text. Both surfaces draw the same list.
/// </summary>
public sealed class SurfaceChromeEntry
{
    private readonly Action _invoke;

    /// <summary>Creates an entry.</summary>
    /// <param name="id">The stable identifier. Used once: a second add of the same id is ignored.</param>
    /// <param name="label">The visible label.</param>
    /// <param name="invoke">The action the entry runs. The head supplies it.</param>
    /// <param name="keyboardShortcut">The shortcut as plain text, or null when the entry has none.</param>
    public SurfaceChromeEntry(string id, string label, Action invoke, string? keyboardShortcut = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentNullException.ThrowIfNull(invoke);
        Id = RequiredPlain(id, 80);
        Label = RequiredPlain(label, 80);
        KeyboardShortcut = string.IsNullOrWhiteSpace(keyboardShortcut) ? null : PanelText.Plain(keyboardShortcut, 40);
        if (KeyboardShortcut is { Length: 0 })
        {
            KeyboardShortcut = null;
        }

        _invoke = invoke;
    }

    /// <summary>Gets the stable identifier.</summary>
    public string Id { get; }

    /// <summary>Gets the visible label.</summary>
    public string Label { get; }

    /// <summary>Gets the shortcut as plain text, or null.</summary>
    public string? KeyboardShortcut { get; }

    /// <summary>Runs the entry's action.</summary>
    public void Invoke() => _invoke();

    private static string RequiredPlain(string value, int maxChars)
    {
        var plain = PanelText.Plain(value, maxChars);
        if (plain.Length == 0)
        {
            throw new ArgumentException("The text is empty after it is made plain.", nameof(value));
        }

        return plain;
    }
}
