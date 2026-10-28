using System.Globalization;

namespace Autonogrammer.Presentation.SideChats;

/// <summary>The words of fork and duplicate.</summary>
public static class ForkCopy
{
    /// <summary>The refusal for a fork from a message the engine cannot make.</summary>
    public const string WholeChatOnly = "This engine forks the whole chat only. Duplicate the chat instead.";

    /// <summary>The refusal for a message count below 1.</summary>
    public const string CountOutOfRange = "Choose at least one message to keep.";

    /// <summary>The status line while a fork is already running.</summary>
    public const string Busy = "A fork is already running.";

    /// <summary>Gets the sentence for a fork that worked.</summary>
    /// <param name="title">The new session's title or id.</param>
    /// <param name="messageCount">The number of messages kept, or <see langword="null"/> for the whole chat.</param>
    /// <returns>The sentence.</returns>
    public static string Forked(string title, int? messageCount) =>
        messageCount is { } count
            ? string.Create(CultureInfo.InvariantCulture, $"Forked {title} from the first {count} {(count == 1 ? "message" : "messages")}. The original chat is unchanged.")
            : string.Create(CultureInfo.InvariantCulture, $"Duplicated the chat as {title}. The original chat is unchanged.");

    /// <summary>Gets the sentence for a failed fork.</summary>
    /// <param name="reason">The failure.</param>
    /// <returns>The sentence.</returns>
    public static string Failed(string reason)
    {
        ArgumentNullException.ThrowIfNull(reason);

        return "The fork failed: " + reason.TrimEnd('.') + ". Nothing was changed.";
    }
}
