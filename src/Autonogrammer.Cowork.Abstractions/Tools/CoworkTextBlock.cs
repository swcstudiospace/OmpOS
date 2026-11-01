namespace Autonogrammer.Cowork.Abstractions;

/// <summary>A text block. Untrusted text taints the session.</summary>
/// <param name="Text">The text.</param>
/// <param name="IsUntrusted">Whether the text came from a page, a screen, or another untrusted source.</param>
public sealed record CoworkTextBlock(string Text, bool IsUntrusted) : CoworkContentBlock;
