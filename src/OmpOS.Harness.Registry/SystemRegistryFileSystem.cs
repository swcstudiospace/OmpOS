namespace OmpOS.Harness.Registry;

/// <summary>
/// The file system the writer uses by default. Temporary files are written with write-through, so the bytes reach the
/// disk before the rename.
/// </summary>
public sealed class SystemRegistryFileSystem : IRegistryFileSystem
{
    /// <summary>
    /// The shared instance.
    /// </summary>
    public static SystemRegistryFileSystem Instance { get; } = new();

    /// <inheritdoc />
    public async Task WriteAsync(string path, ReadOnlyMemory<byte> content, CancellationToken cancellationToken)
    {
        var options = new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.Write,
            Share = FileShare.None,
            Options = FileOptions.Asynchronous | FileOptions.WriteThrough,
        };
        using var stream = new FileStream(path, options);
        await stream.WriteAsync(content, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Replace(string source, string destination) => File.Move(source, destination, overwrite: true);

    /// <inheritdoc />
    public void Delete(string path) => File.Delete(path);
}
