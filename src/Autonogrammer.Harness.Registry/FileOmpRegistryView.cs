namespace Autonogrammer.Harness.Registry;

/// <summary>
/// The omp-side view that reads the verified export file with <see cref="RegistryReader"/>.
/// </summary>
public sealed class FileOmpRegistryView : IOmpRegistryView
{
    private readonly string _path;

    /// <summary>
    /// Initializes a new instance over an export file.
    /// </summary>
    /// <param name="path">The export file omp reads.</param>
    public FileOmpRegistryView(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = path;
    }

    /// <inheritdoc />
    public Task<RegistryReadResult> ReadAsync(long expectedGeneration, CancellationToken cancellationToken) =>
        RegistryReader.ReadAsync(_path, expectedGeneration, cancellationToken);
}
