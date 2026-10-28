namespace OmpOS.Native.Views.Attachments;

/// <summary>Reads an image from the clipboard. The desktop head registers <see cref="AvaloniaClipboardImageSource"/>; tests use a fake.</summary>
internal interface IClipboardImageSource
{
    /// <summary>Reads the clipboard image.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The image, or <see langword="null"/> when the clipboard holds none.</returns>
    Task<PickedImage?> TryGetImageAsync(CancellationToken cancellationToken);
}
