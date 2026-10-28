using System.Text.Json.Serialization;

namespace Autonogrammer.Sandbox;

/// <summary>
/// One stored line of a cloud session transcript.
/// </summary>
/// <param name="At">When the entry was recorded.</param>
/// <param name="Kind">
/// <see cref="MessageKind"/> for a conversation message or <see cref="ResumeMarkerKind"/> for a marker written when a session
/// was woken after sleep. Other values are preserved as read, never rejected.
/// </param>
/// <param name="Role">The speaker (for example <c>user</c> or <c>assistant</c>); <c>system</c> for markers.</param>
/// <param name="Text">The entry text.</param>
public sealed record CloudTranscriptEntry(DateTimeOffset At, string Kind, string Role, string Text)
{
    /// <summary>The <see cref="Kind"/> of an ordinary conversation message.</summary>
    public const string MessageKind = "message";

    /// <summary>The <see cref="Kind"/> of the marker written when a session resumes after sleep.</summary>
    public const string ResumeMarkerKind = "resume-marker";

    /// <summary>Gets a value indicating whether this entry is a resume marker.</summary>
    [JsonIgnore]
    public bool IsResumeMarker => string.Equals(Kind, ResumeMarkerKind, StringComparison.Ordinal);
}
