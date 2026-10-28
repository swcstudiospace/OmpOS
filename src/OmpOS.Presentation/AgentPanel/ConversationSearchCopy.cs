using System.Globalization;

namespace OmpOS.Presentation.AgentPanel;

/// <summary>Every user-facing sentence of the conversation search.</summary>
public static class ConversationSearchCopy
{
    /// <summary>The accessible name of the search overlay.</summary>
    public const string DialogName = "Search conversations";

    /// <summary>The accessible name and placeholder of the query box.</summary>
    public const string QueryName = "Search conversations";

    /// <summary>The help text of the query box: how to move through the results.</summary>
    public const string QueryHelp = "Type to search titles and messages. Up and Down choose a result, Enter opens it, Escape closes the search.";

    /// <summary>The accessible name of the result list.</summary>
    public const string ListName = "Conversation results";

    /// <summary>The accessible name of the live region that announces the result count.</summary>
    public const string CountName = "Search results";

    /// <summary>The text shown before anything is typed.</summary>
    public const string Prompt = "Type to search your conversations.";

    /// <summary>Builds the text shown when nothing matches.</summary>
    /// <param name="query">The query.</param>
    /// <returns>The sentence.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is null.</exception>
    public static string NoMatches(string query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return "No conversations match \u201c" + query.Trim() + "\u201d.";
    }

    /// <summary>Builds the announcement of the result count.</summary>
    /// <param name="count">The number of results.</param>
    /// <returns>The sentence.</returns>
    public static string ResultCount(int count) => count switch
    {
        0 => "No results",
        1 => "1 conversation",
        _ => string.Create(CultureInfo.InvariantCulture, $"{count} conversations"),
    };
}
