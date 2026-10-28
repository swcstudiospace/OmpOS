using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;

namespace OmpOS.Native.Views.Attachments;

/// <summary>Turns the encoded thumbnail bytes of an attachment into a bitmap for the preview strip.</summary>
internal sealed class ThumbnailConverter : IValueConverter
{
    /// <summary>Gets the shared converter.</summary>
    internal static ThumbnailConverter Instance { get; } = new();

    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not byte[] { Length: > 0 } bytes)
        {
            return null;
        }

        try
        {
            using var stream = new MemoryStream(bytes, writable: false);
            return new Bitmap(stream);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
