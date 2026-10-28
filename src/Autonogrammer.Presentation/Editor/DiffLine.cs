namespace Autonogrammer.Presentation.Editor;

/// <summary>One line of a diff hunk, without its leading marker and without its line break.</summary>
/// <param name="Kind">Whether the line is context, removed or added.</param>
/// <param name="Text">The line's content.</param>
public sealed record DiffLine(DiffLineKind Kind, string Text);
