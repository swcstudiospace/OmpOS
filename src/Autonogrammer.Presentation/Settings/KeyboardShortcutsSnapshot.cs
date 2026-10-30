namespace Autonogrammer.Presentation.Settings;

/// <summary>The shortcuts the Keyboard page lists.</summary>
/// <param name="Rows">The shortcuts in the order the source lists them.</param>
public sealed record KeyboardShortcutsSnapshot(IReadOnlyList<ShortcutRow> Rows);
