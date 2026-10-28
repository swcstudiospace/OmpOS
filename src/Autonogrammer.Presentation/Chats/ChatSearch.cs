using Autonogrammer.GenUi;

namespace Autonogrammer.Presentation.Chats;

/// <summary>
/// Case-insensitive search over a session's transcript and every side chat opened from it. Like
/// <see cref="TranscriptSearch"/> it is a pure function of its inputs.
/// </summary>
/// <remarks>
/// Matches come in a fixed order: the parent transcript's matches first (exactly those of <see cref="TranscriptSearch.Find"/>),
/// then each side chat's matches in the order the chats are given, each by message and then by offset. A message is searched
/// as its verbatim text, and matches within one message do not overlap.
/// </remarks>
public static class ChatSearch
{
    /// <summary>Finds every case-insensitive match of <paramref name="query"/> in the transcript and in each side chat.</summary>
    /// <param name="transcript">The parent transcript, with its streaming tail.</param>
    /// <param name="chats">The side chats opened from the session, in the order their matches are reported.</param>
    /// <param name="query">The text to find. An empty query matches nothing.</param>
    /// <returns>The matches, transcript first and then each side chat. Empty when nothing matches or the query is empty.</returns>
    /// <exception cref="ArgumentNullException">A parameter is null.</exception>
    public static IReadOnlyList<TranscriptMatch> Find(UiTree transcript, IEnumerable<SideChat> chats, string query)
    {
        ArgumentNullException.ThrowIfNull(transcript);
        ArgumentNullException.ThrowIfNull(chats);
        ArgumentNullException.ThrowIfNull(query);

        var matches = new List<TranscriptMatch>(TranscriptSearch.Find(transcript, query));
        if (query.Length == 0)
        {
            return matches;
        }

        foreach (var chat in chats)
        {
            matches.AddRange(FindInChat(chat, query));
        }

        return matches;
    }

    /// <summary>Finds every case-insensitive match of <paramref name="query"/> in one side chat's messages.</summary>
    /// <param name="chat">The side chat to search.</param>
    /// <param name="query">The text to find. An empty query matches nothing.</param>
    /// <returns>The matches in message order, each naming the chat. Empty when nothing matches or the query is empty.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="chat"/> or <paramref name="query"/> is null.</exception>
    public static IReadOnlyList<TranscriptMatch> FindInChat(SideChat chat, string query)
    {
        ArgumentNullException.ThrowIfNull(chat);
        ArgumentNullException.ThrowIfNull(query);

        var matches = new List<TranscriptMatch>();
        if (query.Length == 0)
        {
            return matches;
        }

        var messages = chat.Messages;
        var found = new List<TranscriptMatch>();
        for (var index = 0; index < messages.Count; index++)
        {
            found.Clear();
            TranscriptSearch.AddMatches(found, index, messages[index].Text, query);
            foreach (var match in found)
            {
                matches.Add(match with { ChatId = chat.Id, ChatTitle = chat.Title });
            }
        }

        return matches;
    }
}
