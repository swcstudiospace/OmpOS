namespace OmpOS.Presentation.Mentions;

/// <summary>The text a mention attaches to a message, read at send time.</summary>
/// <param name="Text">The text, already cut to the byte cap the reader was given.</param>
/// <param name="SizeBytes">The size in bytes of the full content before any cut.</param>
/// <param name="Truncated">Whether <paramref name="Text"/> is shorter than the full content.</param>
public sealed record MentionContent(string Text, long SizeBytes, bool Truncated);
