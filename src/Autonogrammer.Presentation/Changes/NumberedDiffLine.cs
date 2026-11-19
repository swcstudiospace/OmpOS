using Autonogrammer.Presentation.Editor;

namespace Autonogrammer.Presentation.Changes;

/// <summary>One line of a diff as a gutter shows it: its kind, its line number and its text without the line break.</summary>
/// <param name="Kind">Whether the line is context, removed or added.</param>
/// <param name="Number">The line number the gutter shows: the number in the old text for a removed line, otherwise the number in the new text. One-based.</param>
/// <param name="Text">The line without its line break.</param>
public sealed record NumberedDiffLine(DiffLineKind Kind, int Number, string Text);
