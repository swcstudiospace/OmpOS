namespace Autonogrammer.Cowork.Sessions;

/// <summary>
/// One task's standing block and its delivery state. <see cref="Text"/> is the snapshot taken when the task first started; it is not
/// recalculated when the task resumes. <see cref="ReplayedText"/> is the block as omp replayed it, kept byte for byte, or null.
/// </summary>
/// <param name="SessionId">The task.</param>
/// <param name="State">The delivery state.</param>
/// <param name="Uri">The stable resource URI the block is sent under.</param>
/// <param name="MimeType">The MIME type the block is sent with.</param>
/// <param name="Text">The block, exactly as it is sent.</param>
/// <param name="ReplayedText">The block as omp replayed it, or null when no replay carried it.</param>
public sealed record CoworkFirstPromptRecord(
    string SessionId,
    CoworkFirstPromptState State,
    System.Uri Uri,
    string MimeType,
    string Text,
    string? ReplayedText)
{
    /// <summary>Returns the identifier and state only. The block text is never printed.</summary>
    /// <returns>A short description that holds no content.</returns>
    public override string ToString() => "CoworkFirstPromptRecord { SessionId = " + SessionId + ", State = " + State + " }";
}
