using System.Globalization;

namespace OmpOS.Presentation.Composer.Attachments;

/// <summary>Every user-facing sentence of image attachments. The views and their tests read the same constants.</summary>
public static class AttachmentCopy
{
    /// <summary>The accessible name of the Attach button.</summary>
    public const string AttachName = "Attach image";

    /// <summary>The accessible name of the preview strip.</summary>
    public const string StripName = "Attached images";

    /// <summary>The accessible name of the attachment notice.</summary>
    public const string NoticeName = "Image attachment message";

    /// <summary>The picker title.</summary>
    public const string PickerTitle = "Attach images";

    /// <summary>The picker filter name.</summary>
    public const string PickerFilter = "Images (PNG, JPEG, GIF, WebP)";

    /// <summary>Shown when the engine does not accept images.</summary>
    public const string EngineRefuses = "This engine does not accept images in prompts.";

    /// <summary>Shown when the selected model does not read images.</summary>
    public const string ModelRefuses = "The selected model does not read images. Choose a model with image input.";

    /// <summary>Shown while the model's image support is not known.</summary>
    public const string ModelMayIgnore = "The model may ignore images.";

    /// <summary>Shown for a file with no bytes.</summary>
    public const string Empty = "That file is empty.";

    /// <summary>Shown for a file that is not an allowed image.</summary>
    public const string Unsupported = "Only PNG, JPEG, GIF and WebP images can be attached.";

    /// <summary>The help text of the preview strip.</summary>
    public const string StripHelp = "Press Delete to remove the focused image.";

    /// <summary>Builds the too-many sentence.</summary>
    /// <param name="max">The limit.</param>
    /// <returns>The sentence.</returns>
    public static string TooMany(int max) => string.Create(CultureInfo.InvariantCulture, $"A message carries at most {max} images.");

    /// <summary>Builds the too-large sentence.</summary>
    /// <param name="maxBytes">The limit in bytes.</param>
    /// <returns>The sentence.</returns>
    public static string TooLarge(long maxBytes) => string.Create(CultureInfo.InvariantCulture, $"An image can be at most {FormatSize(maxBytes)}.");

    /// <summary>Builds the total-too-large sentence.</summary>
    /// <param name="maxBytes">The limit in bytes.</param>
    /// <returns>The sentence.</returns>
    public static string TotalTooLarge(long maxBytes) => string.Create(CultureInfo.InvariantCulture, $"All images together can be at most {FormatSize(maxBytes)}.");

    /// <summary>Builds the accessible name of one attachment.</summary>
    /// <param name="attachment">The attachment.</param>
    /// <returns>For example <c>Image shot.png, 12 KB</c>.</returns>
    public static string ItemName(ComposerAttachment attachment)
    {
        ArgumentNullException.ThrowIfNull(attachment);
        return "Image " + attachment.Name + ", " + attachment.SizeText;
    }

    /// <summary>Builds the accessible name of an attachment's remove button.</summary>
    /// <param name="name">The display name.</param>
    /// <returns>For example <c>Remove image shot.png</c>.</returns>
    public static string RemoveName(string name) => "Remove image " + name;

    /// <summary>Formats a byte count.</summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>The size text.</returns>
    public static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => string.Create(CultureInfo.InvariantCulture, $"{bytes} B"),
        < 1024 * 1024 => string.Create(CultureInfo.InvariantCulture, $"{bytes / 1024.0:0.#} KB"),
        _ => string.Create(CultureInfo.InvariantCulture, $"{bytes / (1024.0 * 1024.0):0.#} MB"),
    };
}
