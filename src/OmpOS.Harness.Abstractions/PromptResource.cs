namespace OmpOS.Harness.Abstractions;

/// <summary>
/// A resolved @-mention that travels with a prompt as an embedded resource: the engine inlines its text next to the user's words.
/// </summary>
/// <param name="Location">The absolute URI that names the resource, for example the <c>file:</c> URI of a file.</param>
/// <param name="MimeType">The media type of <paramref name="Text"/>, for example <c>text/plain</c>.</param>
/// <param name="Text">The text of the resource, already cut to the size budget.</param>
/// <param name="DisplayName">The name shown to the user and to the engine.</param>
/// <param name="OriginalLength">The size of the resource before it was cut, in bytes.</param>
/// <param name="Truncated">Whether <paramref name="Text"/> is shorter than the resource.</param>
public sealed record PromptResource(Uri Location, string MimeType, string Text, string DisplayName, long OriginalLength, bool Truncated);
