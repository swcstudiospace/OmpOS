namespace Autonogrammer.Presentation.Chats;

/// <summary>Carries the match that a search selected.</summary>
public sealed class MatchSelectedEventArgs(TranscriptMatch match) : EventArgs
{
    /// <summary>Gets the selected match.</summary>
    public TranscriptMatch Match { get; } = match;
}
