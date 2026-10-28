namespace OmpOS.Workspace.Checkpoints;

/// <summary>The stored copies of one session's earlier file content. A copy is named by the hash of its bytes, so the same content is kept once and a damaged copy is found when it is read.</summary>
internal sealed class CheckpointBlobs
{
    private readonly string _directory;

    /// <summary>Creates the blob area of one session folder.</summary>
    /// <param name="sessionDirectory">The session's folder.</param>
    public CheckpointBlobs(string sessionDirectory) => _directory = Path.Combine(sessionDirectory, "blobs");

    /// <summary>Keeps a copy when none with the same hash exists.</summary>
    /// <param name="hash">The raw hash of <paramref name="content"/>.</param>
    /// <param name="content">The bytes.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    public async Task WriteAsync(string hash, ReadOnlyMemory<byte> content, CancellationToken cancellationToken)
    {
        CheckpointFiles.CreateOwnerDirectory(_directory);
        var path = Path.Combine(_directory, hash);
        if (!File.Exists(path))
        {
            await CheckpointFiles.WriteOwnerFileAsync(path, content.ToArray(), cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Reads a copy back and checks it against its name.</summary>
    /// <param name="hash">The raw hash the copy is named by.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The bytes, or <see langword="null"/> when the copy is missing, unreadable or damaged.</returns>
    public async Task<byte[]?> ReadAsync(string hash, CancellationToken cancellationToken)
    {
        if (hash.Length != 64 || !hash.All(static character => character is (>= '0' and <= '9') or (>= 'a' and <= 'f')))
        {
            return null;
        }

        try
        {
            var bytes = await File.ReadAllBytesAsync(Path.Combine(_directory, hash), cancellationToken).ConfigureAwait(false);
            return string.Equals(CheckpointHash.Raw(bytes), hash, StringComparison.Ordinal) ? bytes : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Deletes every copy that the timeline no longer refers to.</summary>
    /// <param name="referenced">The names still in use.</param>
    public void Prune(IReadOnlySet<string> referenced)
    {
        if (!Directory.Exists(_directory))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(_directory))
        {
            if (referenced.Contains(Path.GetFileName(file)))
            {
                continue;
            }

            try
            {
                File.Delete(file);
            }
            catch (IOException)
            {
                // A copy that cannot be deleted now is deleted by the next prune.
            }
            catch (UnauthorizedAccessException)
            {
                // A copy that cannot be deleted now is deleted by the next prune.
            }
        }
    }
}
