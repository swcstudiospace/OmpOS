namespace Autonogrammer.Presentation.SideChats;

/// <summary>
/// The engine behind a side chat: a child conversation that is separate from the parent session. The desktop head implements it over a second omp session (or <c>unstable_forkSession</c> of the parent
/// when the engine offers it) in which the parent summary is given as hidden context; presentation never starts an engine itself.
/// </summary>
public interface ISideChatEngine
{
    /// <summary>Answers a question in the side chat's own conversation.</summary>
    /// <param name="request">The question with the side chat's history and the hidden parent summary.</param>
    /// <param name="cancellationToken">Cancels the question.</param>
    /// <returns>The assistant's answer.</returns>
    Task<string> AskAsync(SideChatRequest request, CancellationToken cancellationToken);

    /// <summary>Ends the side chat's child conversation. Closing a side chat never touches the parent session.</summary>
    /// <param name="chatId">The side chat.</param>
    /// <param name="cancellationToken">Cancels the close.</param>
    /// <returns>A task that completes when the child conversation is gone.</returns>
    Task CloseAsync(string chatId, CancellationToken cancellationToken);
}
