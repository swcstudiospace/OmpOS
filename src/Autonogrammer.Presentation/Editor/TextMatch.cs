namespace Autonogrammer.Presentation.Editor;

/// <summary>
/// One find match in a document. <see cref="Line"/> and <see cref="Column"/> are zero-based, and <see cref="Length"/> is the
/// matched length in characters. A match never spans a line break unless the query itself contains one.
/// </summary>
/// <param name="Line">The zero-based line where the match starts.</param>
/// <param name="Column">The zero-based column where the match starts.</param>
/// <param name="Length">The matched length in characters.</param>
public sealed record TextMatch(int Line, int Column, int Length);
