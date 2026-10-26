namespace OmpOS.Sandbox;

/// <summary>
/// What a reattach shows the user: the transcript read back from the volume plus, after a sleep, a resume marker.
/// </summary>
/// <remarks>
/// Only the transcript is restored. Processes, shell state, environment variables and in-memory state that existed before
/// sleep are gone; <see cref="ProcessStateRestored"/> is always <see langword="false"/> and nothing in this type claims otherwise.
/// </remarks>
/// <param name="Session">The session after reattach (running).</param>
/// <param name="Transcript">
/// Every stored entry in order, read from the volume. When the container slept, the last entry is the resume marker.
/// </param>
/// <param name="ResumeMarker">The marker written by this reattach, or <see langword="null"/> when the container had not slept.</param>
public sealed record CloudSessionRestore(CloudSession Session, IReadOnlyList<CloudTranscriptEntry> Transcript, CloudTranscriptEntry? ResumeMarker)
{
    /// <summary>
    /// Gets a value indicating whether running processes or shell state from before sleep were restored.
    /// Always <see langword="false"/>: a woken container starts fresh processes.
    /// </summary>
    public static bool ProcessStateRestored => false;
}
