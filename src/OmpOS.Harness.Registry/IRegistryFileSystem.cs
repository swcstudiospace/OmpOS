namespace OmpOS.Harness.Registry;

/// <summary>
/// The storage operations the writer needs to publish an export. The writer only ever writes a temporary file and then
/// replaces the export with it, so an implementation that fails at any step leaves the previous export in place.
/// </summary>
public interface IRegistryFileSystem
{
    /// <summary>
    /// Creates a new file and writes the content to it. The file must not already exist.
    /// </summary>
    /// <param name="path">The file to create.</param>
    /// <param name="content">The bytes to write.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    Task WriteAsync(string path, ReadOnlyMemory<byte> content, CancellationToken cancellationToken);

    /// <summary>
    /// Renames <paramref name="source"/> over <paramref name="destination"/>, replacing it.
    /// </summary>
    /// <param name="source">The file to move.</param>
    /// <param name="destination">The file to replace.</param>
    void Replace(string source, string destination);

    /// <summary>
    /// Deletes a file. Does nothing when the file does not exist.
    /// </summary>
    /// <param name="path">The file to delete.</param>
    void Delete(string path);
}
