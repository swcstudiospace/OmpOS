namespace OmpOS.Presentation.Mentions;

/// <summary>One row the picker offers: something the user may attach.</summary>
/// <param name="Kind">What the row attaches.</param>
/// <param name="Id">The stable key inside its kind: a workspace-relative path, a session id, a pull request number, a rule or skill name.</param>
/// <param name="Display">The name shown and written into the message after the trigger.</param>
/// <param name="Detail">A short secondary text (the folder of a file, a status, a title), or <see langword="null"/>.</param>
/// <param name="Score">The rank of the row for the typed query; higher is better. It is 0 for an empty query.</param>
/// <param name="RequiresConfirmation">Whether the row may hold secrets, so accepting it first asks the user to confirm.</param>
/// <param name="DisabledReason">Why the row cannot be attached now (for example no git repository), or <see langword="null"/> when it can.</param>
/// <param name="SizeBytes">The size of the content in bytes when the source knows it, or -1.</param>
public sealed record MentionCandidate(
    MentionKind Kind,
    string Id,
    string Display,
    string? Detail = null,
    double Score = 0,
    bool RequiresConfirmation = false,
    string? DisabledReason = null,
    long SizeBytes = -1)
{
    /// <summary>Gets whether the row can be attached.</summary>
    public bool IsEnabled => DisabledReason is null;
}
