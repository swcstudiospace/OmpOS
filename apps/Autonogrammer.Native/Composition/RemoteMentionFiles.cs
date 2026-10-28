using Autonogrammer.Presentation.Mentions;

namespace Autonogrammer.Native.Composition;

/// <summary>
/// The file source of the @-mentions of a session whose workspace is on an ssh host: it lists no file and reads none, so the file and folder mentions offer nothing and say why. The files of a remote
/// workspace stay reachable through the explorer and the editor; attaching them to a prompt needs the remote file system behind the mention picker, which this version does not wire.
/// </summary>
internal sealed class RemoteMentionFiles : IMentionFileSource
{
    private const string Reason = "Files of a remote workspace cannot be attached with an @ mention yet. Open the file in the editor instead.";

    /// <inheritdoc/>
    public Task<IReadOnlyList<MentionFileEntry>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<MentionFileEntry>>([]);

    /// <inheritdoc/>
    public Task<MentionContent> ReadAsync(string relativePath, int maxBytes, CancellationToken cancellationToken) =>
        throw new MentionUnavailableException(Reason);

    /// <inheritdoc/>
    public Uri ToUri(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        throw new MentionUnavailableException(Reason);
    }
}
