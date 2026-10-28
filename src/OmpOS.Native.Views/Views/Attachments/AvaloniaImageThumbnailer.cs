using Avalonia.Media.Imaging;
using OmpOS.Presentation.Composer.Attachments;

namespace OmpOS.Native.Views.Attachments;

/// <summary>Decodes an attached image at a small width and re-encodes it as PNG for the preview strip.</summary>
internal sealed class AvaloniaImageThumbnailer : IImageThumbnailer
{
    /// <inheritdoc />
    public byte[]? CreateThumbnail(byte[] data, string mimeType, int maxEdge)
    {
        ArgumentNullException.ThrowIfNull(data);

        try
        {
            using var source = new MemoryStream(data, writable: false);
            using var small = Bitmap.DecodeToWidth(source, maxEdge, BitmapInterpolationMode.HighQuality);
            using var target = new MemoryStream();
            small.Save(target, PngBitmapEncoderOptions.Default);
            return target.ToArray();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException or IOException)
        {
            return null;
        }
    }
}
