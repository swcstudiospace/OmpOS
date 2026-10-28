namespace OmpOS.Presentation.Mentions;

/// <summary>A chat or side chat a mention can attach.</summary>
/// <param name="Id">The session identifier.</param>
/// <param name="Title">The chat title.</param>
/// <param name="IsSideChat">Whether it is a side chat rather than a chat.</param>
/// <param name="LastActivity">When the chat last changed; the picker lists recent chats first.</param>
public sealed record MentionChatInfo(string Id, string Title, bool IsSideChat, DateTimeOffset LastActivity);
