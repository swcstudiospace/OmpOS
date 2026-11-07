using Autonogrammer.Cowork.Artifacts;
using Autonogrammer.Cowork.Artifacts.Rendering;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media.Imaging;

namespace Autonogrammer.Native.Views.Cowork.Artifacts;

/// <summary>
/// A raster image artifact. The bytes are checked by <see cref="ImageValidator"/> before anything is decoded: the magic number,
/// the declared media type, the size cap and the dimensions. A refused image shows its refusal code instead of a picture.
/// </summary>
public partial class ArtifactImageView : UserControl
{
    private static readonly int MaxImageBytes = new ArtifactOptions().MaxImageBytes;

    /// <summary>Creates the view.</summary>
    public ArtifactImageView()
    {
        InitializeComponent();
    }

    /// <summary>Gets the picture control. It is hidden when the image is refused.</summary>
    public Image PictureControl => Picture;

    /// <summary>Gets the message text shown when the image is refused, or an empty string.</summary>
    public string MessageText => Message.IsVisible ? Message.Text ?? string.Empty : string.Empty;

    /// <summary>Validates and shows one image.</summary>
    /// <param name="title">The validated title. It is the picture's accessible name.</param>
    /// <param name="mediaType">The declared media type, or null when the version declared none.</param>
    /// <param name="bytes">The stored image bytes.</param>
    /// <exception cref="ArgumentNullException"><paramref name="title"/> is null.</exception>
    public void Show(string title, string? mediaType, ReadOnlyMemory<byte> bytes)
    {
        ArgumentNullException.ThrowIfNull(title);
        ReleasePicture();
        if (mediaType is null)
        {
            Refuse("no_media_type");
            return;
        }

        var validation = ImageValidator.Validate(bytes.Span, mediaType, MaxImageBytes);
        if (!validation.IsValid)
        {
            Refuse(validation.Code ?? "invalid");
            return;
        }

        using var stream = new MemoryStream(bytes.ToArray());
        AutomationProperties.SetName(Picture, title);
        Picture.Source = new Bitmap(stream);
        Picture.IsVisible = true;
        Message.IsVisible = false;
    }

    private void Refuse(string code)
    {
        Picture.IsVisible = false;
        Message.Text = FormattableString.Invariant($"This image cannot be shown ({code}).");
        Message.IsVisible = true;
    }

    private void ReleasePicture()
    {
        if (Picture.Source is IDisposable previous)
        {
            previous.Dispose();
        }

        Picture.Source = null;
    }
}
