namespace OmpOS.Harness.Abstractions;

/// <summary>An image that travels with a prompt as an image content block. The composer has already checked the bytes and the limits; the engine session only encodes it.</summary>
/// <param name="Name">A short display name used when the image has to be left out of the prompt; it is never sent as content.</param>
/// <param name="MimeType">The media type: <c>image/png</c>, <c>image/jpeg</c>, <c>image/gif</c> or <c>image/webp</c>.</param>
/// <param name="Data">The encoded image bytes, not base64.</param>
public sealed record PromptImage(string Name, string MimeType, ReadOnlyMemory<byte> Data);
