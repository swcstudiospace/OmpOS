namespace Autonogrammer.Native.Views.Attachments;

/// <summary>Lets the user choose image files. The desktop head registers <see cref="StorageProviderFilePicker"/>; tests use a fake.</summary>
internal interface IFilePicker
{
    /// <summary>Opens the picker.</summary>
    /// <param name="maxBytesPerFile">The most bytes to read from one file; a larger file is read one byte past this so the limit check refuses it.</param>
    /// <param name="cancellationToken">Cancels the picker.</param>
    /// <returns>The chosen files, or an empty list when the user cancelled.</returns>
    Task<IReadOnlyList<PickedImage>> PickImagesAsync(long maxBytesPerFile, CancellationToken cancellationToken);
}
