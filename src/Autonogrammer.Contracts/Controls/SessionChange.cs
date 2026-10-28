using System.Text.Json.Serialization;

namespace Autonogrammer.Contracts.Controls;

/// <summary>One file the session's runs edited: the net change from the text before the first edit to the text after the latest one.</summary>
public sealed record SessionChange
{
    /// <summary>Gets the path of the edited file, as the harness reported it.</summary>
    public required string Path { get; init; }

    /// <summary>Gets whether the edits created the file or changed it.</summary>
    public required ChangeKind Kind { get; init; }

    /// <summary>Gets the review state of the change as it stands.</summary>
    public required ChangeReviewStatus Status { get; init; }

    /// <summary>Gets the file text before the first edit, or null when the edits created the file.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OldText { get; init; }

    /// <summary>Gets the file text after the latest edit.</summary>
    public required string NewText { get; init; }
}
