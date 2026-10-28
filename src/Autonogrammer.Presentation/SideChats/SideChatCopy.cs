namespace Autonogrammer.Presentation.SideChats;

/// <summary>The words of side chats.</summary>
public static class SideChatCopy
{
    /// <summary>The sentence under the title of a side chat: what it can see and what it cannot change.</summary>
    public const string Notice = "A side chat sees a read-only summary of the session it was opened from. Nothing you say here changes that session.";

    /// <summary>The status line while the assistant answers.</summary>
    public const string Answering = "The assistant is answering.";

    /// <summary>The label of the control that closes a side chat.</summary>
    public const string CloseLabel = "Close side chat";

    /// <summary>The accessible name of the control that closes a side chat.</summary>
    public const string CloseName = "Close this side chat. The session it was opened from stays open.";

    /// <summary>The status line when a side chat is closed.</summary>
    public const string Closed = "This side chat is closed.";

    /// <summary>The refusal for a side chat opened from a side chat.</summary>
    public const string NoNesting = "Side chats do not nest. Open one from the session instead.";

    /// <summary>The refusal for a question to a closed side chat.</summary>
    public const string ClosedRefusal = "This side chat is closed, so it cannot take a question.";

    /// <summary>The refusal for a blank question.</summary>
    public const string BlankQuestion = "Type a question first.";

    /// <summary>Gets the sentence for a failed answer.</summary>
    /// <param name="reason">The failure.</param>
    /// <returns>The sentence. It says that the question stays in the chat so the user can ask again.</returns>
    public static string Failed(string reason)
    {
        ArgumentNullException.ThrowIfNull(reason);

        return "The assistant could not answer: " + reason.TrimEnd('.') + ". Your question is still here; ask again.";
    }
}
