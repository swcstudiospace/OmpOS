namespace OmpOS.Presentation.SideChats;

/// <summary>A request to fork a session.</summary>
/// <param name="SessionId">The session to fork.</param>
/// <param name="FromMessageIndex">The number of messages (user and assistant turns, from 0) the fork keeps, or <see langword="null"/> to keep the whole conversation. Later messages and their subagents are dropped.</param>
public sealed record ForkRequest(string SessionId, int? FromMessageIndex);
