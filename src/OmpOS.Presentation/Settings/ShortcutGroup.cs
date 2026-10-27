namespace OmpOS.Presentation.Settings;

/// <summary>The shortcuts of one scope, as the Keyboard page draws them under one heading.</summary>
/// <param name="Scope">The scope; it is the heading.</param>
/// <param name="Rows">The shortcuts of the scope in the order the source listed them.</param>
public sealed record ShortcutGroup(string Scope, IReadOnlyList<ShortcutRow> Rows);
