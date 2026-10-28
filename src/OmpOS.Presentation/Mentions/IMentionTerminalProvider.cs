namespace OmpOS.Presentation.Mentions;

/// <summary>The terminals a mention can attach, and their output.</summary>
public interface IMentionTerminalProvider
{
    /// <summary>Lists the terminals, most recent first.</summary>
    /// <param name="cancellationToken">Cancels the listing.</param>
    /// <returns>The terminals.</returns>
    Task<IReadOnlyList<MentionTerminalInfo>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Reads the recent output of a terminal.</summary>
    /// <param name="id">The terminal identifier.</param>
    /// <param name="maxBytes">The byte cap; the end of the output is kept.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The output text.</returns>
    /// <exception cref="MentionUnavailableException">The terminal is gone.</exception>
    Task<MentionContent> ReadOutputAsync(string id, int maxBytes, CancellationToken cancellationToken);
}
