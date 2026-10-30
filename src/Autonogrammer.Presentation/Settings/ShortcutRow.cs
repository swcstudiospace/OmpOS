namespace Autonogrammer.Presentation.Settings;

/// <summary>One keyboard shortcut the shell handles, as the Keyboard page lists it.</summary>
/// <param name="Action">What the shortcut does, in words.</param>
/// <param name="Gesture">The keys, formatted for reading, for example <c>Ctrl+N</c>.</param>
/// <param name="Scope">The area the shortcut belongs to; the page groups rows by it.</param>
public sealed record ShortcutRow(string Action, string Gesture, string Scope)
{
    /// <summary>Gets the row as one sentence for the accessible name.</summary>
    public string Summary => KeyboardCopy.RowSummary(this);
}
