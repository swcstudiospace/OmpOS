namespace Autonogrammer.Harness.Plugins.Hooks;

/// <summary>
/// The file access the hook loader needs. The local implementation is <see cref="LocalHookFileSource"/>; a remote or hosted
/// workspace supplies its own, and tests supply a dictionary.
/// </summary>
public interface IHookFileSource
{
    /// <summary>
    /// Reports whether a file exists, how large it is and whether it is tamper resistant. Never throws for a missing path.
    /// </summary>
    /// <param name="path">The absolute path.</param>
    /// <returns>What is known about the file.</returns>
    HookFileInfo Inspect(string path);

    /// <summary>
    /// Reads at most <paramref name="maxBytes"/> bytes of a file.
    /// </summary>
    /// <param name="path">The absolute path.</param>
    /// <param name="maxBytes">The most bytes to read.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The bytes, or null when the file is missing, unreadable or larger than the cap.</returns>
    ValueTask<byte[]?> ReadAsync(string path, int maxBytes, CancellationToken cancellationToken);

    /// <summary>
    /// Returns the lowercase SHA-256 hex digest of a file's bytes, or null when it is missing, unreadable or larger than
    /// <paramref name="maxBytes"/>. The loader mixes the digest of a hook's script into the consent identity.
    /// </summary>
    /// <param name="path">The absolute path.</param>
    /// <param name="maxBytes">The largest file that is hashed.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The digest, or null.</returns>
    ValueTask<string?> DigestAsync(string path, int maxBytes, CancellationToken cancellationToken);
}
