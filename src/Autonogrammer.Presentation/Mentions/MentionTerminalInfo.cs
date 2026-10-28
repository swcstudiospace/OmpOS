namespace Autonogrammer.Presentation.Mentions;

/// <summary>A terminal session a mention can attach.</summary>
/// <param name="Id">The session identifier.</param>
/// <param name="Title">The terminal title.</param>
/// <param name="IsRunning">Whether the terminal still runs a command.</param>
public sealed record MentionTerminalInfo(string Id, string Title, bool IsRunning);
