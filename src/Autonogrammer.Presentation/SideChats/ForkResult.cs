namespace Autonogrammer.Presentation.SideChats;

/// <summary>A new session made by forking another.</summary>
/// <param name="NewSessionId">The new session's id.</param>
/// <param name="Title">The new session's title, or an empty string when the engine gave none.</param>
public sealed record ForkResult(string NewSessionId, string Title);
