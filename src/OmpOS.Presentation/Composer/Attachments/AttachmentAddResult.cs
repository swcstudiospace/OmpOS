namespace OmpOS.Presentation.Composer.Attachments;

/// <summary>The outcome of adding one image.</summary>
/// <param name="Attachment">The attachment, or <see langword="null"/> when it was refused.</param>
/// <param name="Error">Why it was refused, or <see cref="AttachmentError.None"/>.</param>
/// <param name="Message">The sentence to show the user; empty on success.</param>
public sealed record AttachmentAddResult(ComposerAttachment? Attachment, AttachmentError Error, string Message)
{
    /// <summary>Gets whether the image was attached.</summary>
    public bool Succeeded => Attachment is not null;
}
