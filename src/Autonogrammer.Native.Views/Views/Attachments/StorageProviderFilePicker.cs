using Autonogrammer.Presentation.Composer.Attachments;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace Autonogrammer.Native.Views.Attachments;

/// <summary>The Avalonia file picker: opens the storage provider of the window that holds the composer.</summary>
internal sealed class StorageProviderFilePicker : IFilePicker
{
    private readonly Func<TopLevel?> _topLevel;

    /// <summary>Creates the picker.</summary>
    /// <param name="topLevel">Finds the window or top level that owns the dialog.</param>
    internal StorageProviderFilePicker(Func<TopLevel?> topLevel)
    {
        ArgumentNullException.ThrowIfNull(topLevel);
        _topLevel = topLevel;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PickedImage>> PickImagesAsync(long maxBytesPerFile, CancellationToken cancellationToken)
    {
        if (_topLevel() is not { StorageProvider: { CanOpen: true } provider })
        {
            return [];
        }

        var files = await provider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = AttachmentCopy.PickerTitle,
            AllowMultiple = true,
            FileTypeFilter =
            [
                new FilePickerFileType(AttachmentCopy.PickerFilter)
                {
                    Patterns = ["*.png", "*.jpg", "*.jpeg", "*.gif", "*.webp"],
                    MimeTypes = [ImageSniffer.Png, ImageSniffer.Jpeg, ImageSniffer.Gif, ImageSniffer.WebP],
                },
            ],
        }).ConfigureAwait(true);

        var picked = new List<PickedImage>(files.Count);
        foreach (var file in files)
        {
            picked.Add(await StorageImageReader.ReadAsync(file, maxBytesPerFile, cancellationToken).ConfigureAwait(true));
        }

        return picked;
    }
}
