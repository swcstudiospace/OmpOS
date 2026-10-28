namespace OmpOS.Presentation.Composer.Attachments;

/// <summary>One image attached to the message being composed.</summary>
/// <param name="Id">A unique identifier within the composer.</param>
/// <param name="Name">The display name (a file name without a path), also the alt text.</param>
/// <param name="MimeType">The MIME type found from the bytes.</param>
/// <param name="Data">The encoded image.</param>
/// <param name="Thumbnail">The encoded preview, or <see langword="null"/> when none could be made.</param>
/// <param name="SizeText">The size as shown to the user.</param>
public sealed record ComposerAttachment(string Id, string Name, string MimeType, byte[] Data, byte[]? Thumbnail, string SizeText)
{
    /// <summary>Gets the alt text of the preview.</summary>
    public string AltText => Name;

    /// <summary>Gets whether a preview exists.</summary>
    public bool HasThumbnail => Thumbnail is { Length: > 0 };

    /// <summary>Gets the name a screen reader speaks for the item.</summary>
    public string AccessibleName => AttachmentCopy.ItemName(this);

    /// <summary>Gets the name of the remove button.</summary>
    public string RemoveName => AttachmentCopy.RemoveName(Name);
}
