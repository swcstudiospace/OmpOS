using System.Text.Json.Serialization;

namespace OmpOS.Contracts.Controls;

/// <summary>The answer of <c>POST /sessions/{id}/changes/{path}/accept</c> and <c>reject</c>.</summary>
public sealed record ChangeDecisionResponse
{
    /// <summary>Gets the path of the change, as <c>GET /sessions/{id}/changes</c> reports it.</summary>
    public required string Path { get; init; }

    /// <summary>Gets the review state after the decision.</summary>
    public required ChangeReviewStatus Status { get; init; }

    /// <summary>Gets a value indicating whether the decision wrote the file in the workspace. False when the decision was only recorded.</summary>
    public required bool Applied { get; init; }

    /// <summary>Gets a plain sentence that says why nothing was written, or null when the decision was applied.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Reason { get; init; }
}
