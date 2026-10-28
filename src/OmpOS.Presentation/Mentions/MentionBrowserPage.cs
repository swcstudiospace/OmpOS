namespace OmpOS.Presentation.Mentions;

/// <summary>The page the built-in browser shows, as text.</summary>
/// <param name="Title">The page title.</param>
/// <param name="Url">The page address.</param>
/// <param name="Text">The visible text of the page, already cut to the byte cap.</param>
/// <param name="SizeBytes">The size of the full text in bytes.</param>
public sealed record MentionBrowserPage(string Title, Uri Url, string Text, long SizeBytes);
