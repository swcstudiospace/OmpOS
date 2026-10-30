namespace Autonogrammer.Presentation.Subagents;

/// <summary>
/// One task call as the panel shows it. <see cref="LastLine"/> is plain text only: the panel never treats it as markup
/// and never opens a link from it.
/// </summary>
public sealed record SubagentRowModel
{
    /// <summary>Gets the task call identifier.</summary>
    public required string CallId { get; init; }

    /// <summary>Gets the agent name, or <c>Sub-agent N</c> when the call did not name one. Never empty.</summary>
    public required string Name { get; init; }

    /// <summary>Gets whether <see cref="Name"/> was supplied by the call. False means the panel assigned <c>Sub-agent N</c>.</summary>
    public required bool NameFromCall { get; init; }

    /// <summary>Gets the status.</summary>
    public required SubagentStatus Status { get; init; }

    /// <summary>Gets the elapsed time from the injected clock. Terminal rows freeze this when they leave running.</summary>
    public required TimeSpan Elapsed { get; init; }

    /// <summary>Gets the last output line as plain text. Never markup, never a link.</summary>
    public required string LastLine { get; init; }

    /// <summary>Gets the status word.</summary>
    public string StatusLabel => SubagentCopy.StatusLabel(Status);

    /// <summary>Gets the elapsed time as minutes and seconds, or hours when it reaches an hour.</summary>
    public string ElapsedText
    {
        get
        {
            var elapsed = Elapsed < TimeSpan.Zero ? TimeSpan.Zero : Elapsed;
            return elapsed.TotalHours >= 1
                ? $"{(int)elapsed.TotalHours}:{elapsed.Minutes:00}:{elapsed.Seconds:00}"
                : $"{(int)elapsed.TotalMinutes}:{elapsed.Seconds:00}";
        }
    }

    /// <summary>Gets the accessible name of the row: name, status and elapsed time.</summary>
    public string AccessibleName => $"{Name}, {StatusLabel}, {ElapsedText}";

    /// <summary>Gets whether the last line is plain text. Always true.</summary>
    public bool IsPlainText { get; } = true;

    /// <summary>Gets a link the row would open. Always null: agent text is never an opening link.</summary>
    public Uri? OpeningLink { get; }
}
