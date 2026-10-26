namespace OmpOS.Presentation.Chats;

/// <summary>What a <see cref="TranscriptSearchModel"/> searches.</summary>
public enum SearchScope
{
    /// <summary>The parent session's transcript only.</summary>
    Transcript,

    /// <summary>The parent session's transcript and the messages of every side chat opened from it.</summary>
    TranscriptAndSideChats,
}
