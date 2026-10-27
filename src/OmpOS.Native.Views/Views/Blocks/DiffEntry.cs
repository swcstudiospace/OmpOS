namespace OmpOS.Native.Views.Blocks;

/// <summary>One line of a drawn diff.</summary>
/// <param name="Kind">Whether the line is context, added or removed.</param>
/// <param name="Number">The line number the gutter shows: the number in the old text for a removed line, otherwise the number in the new text. One-based.</param>
/// <param name="Text">The line without its line break.</param>
internal readonly record struct DiffEntry(DiffEntryKind Kind, int Number, string Text);
