using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;

namespace Autonogrammer.Native.Views.Attachments;

/// <summary>Reads a bitmap from the system clipboard of the window that holds the composer and encodes it as PNG.</summary>
internal sealed class AvaloniaClipboardImageSource : IClipboardImageSource
{
    private readonly Func<TopLevel?> _topLevel;

    /// <summary>Creates the source.</summary>
    /// <param name="topLevel">Finds the window whose clipboard is read.</param>
    internal AvaloniaClipboardImageSource(Func<TopLevel?> topLevel)
    {
        ArgumentNullException.ThrowIfNull(topLevel);
        _topLevel = topLevel;
    }

    /// <inheritdoc />
    public async Task<PickedImage?> TryGetImageAsync(CancellationToken cancellationToken)
    {
        if (_topLevel()?.Clipboard is not { } clipboard)
        {
            return null;
        }

        using var bitmap = await clipboard.TryGetBitmapAsync().ConfigureAwait(true);
        if (bitmap is null)
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();
        using var stream = new MemoryStream();
        bitmap.Save(stream, PngBitmapEncoderOptions.Default);
        return new PickedImage("pasted-image.png", stream.ToArray(), "image/png");
    }
}
