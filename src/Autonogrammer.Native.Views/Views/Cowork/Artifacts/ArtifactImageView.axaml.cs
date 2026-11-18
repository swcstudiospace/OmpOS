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
    /// <summary>Creates the view.</summary>
    public ArtifactImageView()
    {
        InitializeComponent();
    }

    /// <summary>The production decoder: Avalonia's codec. Tests inject a replacement through <see cref="Show"/>.</summary>
    private static readonly Func<Stream, Bitmap> DefaultDecoder = static stream => new Bitmap(stream);

    /// <summary>Gets the picture control. It is hidden when the image is refused.</summary>
    public Image PictureControl => Picture;

    /// <summary>Gets the message text shown when the image is refused, or an empty string.</summary>
    public string MessageText => Message.IsVisible ? Message.Text ?? string.Empty : string.Empty;

    /// <summary>Validates and shows one image.</summary>
    /// <param name="title">The validated title. It is the picture's accessible name.</param>
    /// <param name="mediaType">The declared media type, or null when the version declared none.</param>
    /// <param name="bytes">The stored image bytes.</param>
    /// <param name="maxImageBytes">The configured image size cap, from <c>Cowork:Artifacts:MaxImageBytes</c>.</param>
    /// <param name="decode">Decodes the validated bytes. Null uses the production codec.</param>
    /// <exception cref="ArgumentNullException"><paramref name="title"/> is null.</exception>
    public void Show(string title, string? mediaType, ReadOnlyMemory<byte> bytes, int maxImageBytes, Func<Stream, Bitmap>? decode = null)
    {
        ArgumentNullException.ThrowIfNull(title);
        ReleasePicture();
        if (mediaType is null)
        {
            Refuse("no_media_type");
            return;
        }

        var validation = ImageValidator.Validate(bytes.Span, mediaType, maxImageBytes);
        if (!validation.IsValid)
        {
            Refuse(validation.Code ?? "invalid");
            return;
        }

        var decoder = decode ?? DefaultDecoder;

        Bitmap? picture = null;
        try
        {
            using (var stream = new MemoryStream(bytes.ToArray()))
            {
                try
                {
                    picture = decoder(stream);
                }
                catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or NotSupportedException)
                {
                    Refuse("undecodable");
                    return;
                }
            }

            AutomationProperties.SetName(Picture, title);
            Picture.Source = picture;
            // The picture control now owns the bitmap; ReleasePicture disposes it.
            picture = null;
            Picture.IsVisible = true;
            Message.IsVisible = false;
        }
        finally
        {
            picture?.Dispose();
        }
    }

    /// <summary>Releases the decoded bitmap, if any. A host calls it before the view is replaced so the bitmap is not leaked.</summary>
    public void ReleasePicture()
    {
        if (Picture.Source is IDisposable previous)
        {
            previous.Dispose();
        }

        Picture.Source = null;
    }

    private void Refuse(string code)
    {
        Picture.IsVisible = false;
        Message.Text = FormattableString.Invariant($"This image cannot be shown ({code}).");
        Message.IsVisible = true;
    }
}
