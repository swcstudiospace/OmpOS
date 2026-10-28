using Autonogrammer.Presentation.Editor;

namespace Autonogrammer.Native.Views.Diff;

/// <summary>
/// One line of a hunk as the diff review draws it: the line's marker and its text. The marker carries the kind in text, so
/// a removed or added line is never told apart by colour alone.
/// </summary>
/// <param name="Line">The diff line.</param>
internal sealed record DiffLineRow(DiffLine Line)
{
    /// <summary>Gets the line marker: <c>-</c> for removed, <c>+</c> for added, and a space for context.</summary>
    public string Marker => Line.Kind switch
    {
        DiffLineKind.Removed => "-",
        DiffLineKind.Added => "+",
        _ => " ",
    };

    /// <summary>Gets the text the row shows: the marker, a space, and the line's content.</summary>
    public string Display => $"{Marker} {Line.Text}";
}
