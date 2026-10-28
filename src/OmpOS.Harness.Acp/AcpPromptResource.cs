namespace OmpOS.Harness.Acp;

/// <summary>
/// A neutral, already-resolved @-mention that is attached to a prompt as an embedded resource. The presentation layer's
/// resolved mention is converted to this type by the wiring, because this assembly references neither presentation nor contracts.
/// </summary>
/// <param name="Location">The resource location, for example <c>file:///abs/path</c>.</param>
/// <param name="MimeType">The MIME type, or blank for <c>text/plain</c>.</param>
/// <param name="Text">The resource text, possibly already truncated upstream.</param>
/// <param name="DisplayName">A short name used in omission notes.</param>
/// <param name="OriginalLength">The size in bytes of the full content before any cap.</param>
/// <param name="Truncated">Whether the upstream resolver already truncated <paramref name="Text"/>.</param>
public sealed record AcpPromptResource(string Location, string MimeType, string Text, string DisplayName, long OriginalLength, bool Truncated);
