using Autonogrammer.Contracts.Cowork;
using Autonogrammer.Presentation.AgentPanel;

namespace Autonogrammer.Presentation.Cowork;

/// <summary>
/// One rail row. It mirrors <see cref="CoworkSessionSummary"/> and does not implement a Code session summary.
/// Text is plain. There is no message, path or folder member.
/// </summary>
public sealed record CoworkSessionRow
{
    /// <summary>Gets the session identifier.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the plain title, capped at <see cref="CoworkSessionSummary.MaxTitleLength"/> characters.</summary>
    public required string Title { get; init; }

    /// <summary>Gets the Cowork lifecycle.</summary>
    public required CoworkSessionStatus Status { get; init; }

    /// <summary>Gets the status in words.</summary>
    public required string StatusText { get; init; }

    /// <summary>Gets when the task was created.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Gets when the task was last updated.</summary>
    public required DateTimeOffset UpdatedAt { get; init; }

    /// <summary>Gets the project identifier, or null when the task is not in a project.</summary>
    public string? ProjectId { get; init; }

    /// <summary>Gets the device identifier, or null when it is unknown.</summary>
    public string? DeviceId { get; init; }

    /// <summary>Builds a row from a summary. The title is plain text and capped.</summary>
    /// <param name="summary">The catalog summary.</param>
    /// <returns>The row.</returns>
    public static CoworkSessionRow From(CoworkSessionSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        return new CoworkSessionRow
        {
            Id = summary.Id,
            Title = PanelText.Plain(summary.Title, CoworkSessionSummary.MaxTitleLength),
            Status = summary.Status,
            StatusText = Words(summary.Status),
            CreatedAt = summary.CreatedAt,
            UpdatedAt = summary.UpdatedAt,
            ProjectId = PlainOrNull(summary.ProjectId),
            DeviceId = PlainOrNull(summary.DeviceId),
        };
    }

    private static string? PlainOrNull(string? value)
    {
        var plain = PanelText.Plain(value, 128);
        return plain.Length == 0 ? null : plain;
    }

    private static string Words(CoworkSessionStatus status) => status switch
    {
        CoworkSessionStatus.Created => "Created",
        CoworkSessionStatus.Running => "Running",
        CoworkSessionStatus.WaitingForDecision => "Waiting for decision",
        CoworkSessionStatus.Paused => "Paused",
        CoworkSessionStatus.Completed => "Completed",
        CoworkSessionStatus.Failed => "Failed",
        CoworkSessionStatus.Lost => "Lost",
        _ => "Status unknown",
    };
}
