using System.Runtime.InteropServices;

namespace OmpOS.Presentation.Editor;

/// <summary>
/// A zero-based line and column position in a <see cref="TextDocument"/>. A line ends at a <c>'\n'</c>; a <c>'\r'</c>
/// before it stays part of that line's content.
/// </summary>
/// <param name="Line">The zero-based line index.</param>
/// <param name="Column">The zero-based character offset from the start of the line.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct TextPosition(int Line, int Column);
