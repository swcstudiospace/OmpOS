namespace Autonogrammer.Cowork.Abstractions;

/// <summary>An image block. A page or screen capture is untrusted. Mime type is PNG, JPEG or WebP.</summary>
/// <param name="Data">The image bytes.</param>
/// <param name="MimeType">The mime type.</param>
/// <param name="Description">The alt text.</param>
/// <param name="IsUntrusted">Whether the image came from a page, a screen, or another untrusted source.</param>
public sealed record CoworkImageBlock(ReadOnlyMemory<byte> Data, string MimeType, string Description, bool IsUntrusted) : CoworkContentBlock;
