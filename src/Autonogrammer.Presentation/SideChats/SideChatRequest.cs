using Autonogrammer.Presentation.Chats;

namespace Autonogrammer.Presentation.SideChats;

/// <summary>One question to a side chat's engine.</summary>
/// <param name="ChatId">The side chat.</param>
/// <param name="ParentSessionId">The session the side chat was opened from.</param>
/// <param name="ParentSummary">A read-only summary of the parent session, taken when the side chat was opened. It is hidden context: the engine reads it, the user does not see it as a message, and the engine never writes to the parent.</param>
/// <param name="History">The side chat's own messages before <paramref name="Question"/>, oldest first.</param>
/// <param name="Question">What the user asked.</param>
public sealed record SideChatRequest(string ChatId, string ParentSessionId, string ParentSummary, IReadOnlyList<SideChatMessage> History, string Question);
