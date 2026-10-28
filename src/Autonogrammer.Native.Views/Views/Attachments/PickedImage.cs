namespace Autonogrammer.Native.Views.Attachments;

/// <summary>An image file read from a picker, the clipboard or a drop, before the composer validates it.</summary>
/// <param name="Name">The file name, or a generated name for a pasted image.</param>
/// <param name="Data">The bytes read, at most one more than the size limit so the model can refuse an oversized file.</param>
/// <param name="MimeType">The type the source declared, or <see langword="null"/>.</param>
internal sealed record PickedImage(string Name, byte[] Data, string? MimeType);
