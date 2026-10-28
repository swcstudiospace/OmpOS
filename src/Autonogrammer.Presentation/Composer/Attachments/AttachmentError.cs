namespace Autonogrammer.Presentation.Composer.Attachments;

/// <summary>Why an image was not attached.</summary>
public enum AttachmentError
{
    /// <summary>The image was attached.</summary>
    None,

    /// <summary>The file has no bytes.</summary>
    Empty,

    /// <summary>The message already carries the most images.</summary>
    TooMany,

    /// <summary>The image is larger than the single-image limit.</summary>
    TooLarge,

    /// <summary>Adding the image would pass the total limit.</summary>
    TotalTooLarge,

    /// <summary>The bytes are not PNG, JPEG, GIF or WebP.</summary>
    UnsupportedType,

    /// <summary>The model or the engine cannot take images.</summary>
    Refused,
}
