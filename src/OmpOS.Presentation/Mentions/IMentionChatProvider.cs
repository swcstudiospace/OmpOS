namespace OmpOS.Presentation.Mentions;

/// <summary>The chats and side chats a mention can attach, and their transcripts.</summary>
public interface IMentionChatProvider
{
    /// <summary>Lists the chats and side chats.</summary>
    /// <param name="cancellationToken">Cancels the listing.</param>
    /// <returns>The chats in any order; the source ranks them.</returns>
    Task<IReadOnlyList<MentionChatInfo>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Reads a chat transcript as plain text.</summary>
    /// <param name="id">The session identifier.</param>
    /// <param name="maxBytes">The byte cap; the end of the transcript is kept.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The transcript text.</returns>
    /// <exception cref="MentionUnavailableException">The chat is gone.</exception>
    Task<MentionContent> ReadTranscriptAsync(string id, int maxBytes, CancellationToken cancellationToken);
}
