namespace Autonogrammer.Harness.OmpConfig;

/// <summary>One line of <c>models.yml</c> with the exact line ending it had, so joining the lines gives back the original bytes.</summary>
/// <param name="Content">The text of the line without its line ending.</param>
/// <param name="Eol">The line ending: <c>"\n"</c>, <c>"\r\n"</c>, or empty for a last line without one.</param>
internal readonly record struct OmpYamlLine(string Content, string Eol);
