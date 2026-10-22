namespace OmpOS.Harness.Registry.Tests;

/// <summary>
/// A file system that delegates to the real one, with two scripted behaviours: it can fail the rename, which simulates an
/// interruption after the temporary file is written and before it replaces the export, and it can delay each write while
/// recording how many writes were in flight at once.
/// </summary>
internal sealed class ScriptedFileSystem(bool failReplace = false, TimeSpan? writeDelay = null) : IRegistryFileSystem
{
    private readonly Lock _sync = new();
    private int _inFlight;
    private int _maxInFlight;

    /// <summary>
    /// The largest number of writes that were in flight at the same time.
    /// </summary>
    public int MaxConcurrentWrites
    {
        get
        {
            lock (_sync)
            {
                return _maxInFlight;
            }
        }
    }

    /// <inheritdoc />
    public async Task WriteAsync(string path, ReadOnlyMemory<byte> content, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            _inFlight++;
            _maxInFlight = Math.Max(_maxInFlight, _inFlight);
        }

        try
        {
            if (writeDelay is { } delay)
            {
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }

            await SystemRegistryFileSystem.Instance.WriteAsync(path, content, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            lock (_sync)
            {
                _inFlight--;
            }
        }
    }

    /// <inheritdoc />
    public void Replace(string source, string destination)
    {
        if (failReplace)
        {
            throw new IOException("Simulated interruption before the rename.");
        }

        SystemRegistryFileSystem.Instance.Replace(source, destination);
    }

    /// <inheritdoc />
    public void Delete(string path) => SystemRegistryFileSystem.Instance.Delete(path);
}
