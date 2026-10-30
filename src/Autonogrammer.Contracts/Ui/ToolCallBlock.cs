using System.Text.Json;

namespace Autonogrammer.Contracts.Ui;

/// <summary>
/// A tool-call card block (kind "tool_call").
/// </summary>
public sealed record ToolCallBlock : UiBlock
{
    /// <summary>Gets the tool-call identifier.</summary>
    public required string CallId { get; init; }

    /// <summary>Gets the invoked tool name.</summary>
    public required string ToolName { get; init; }

    /// <summary>Gets the call status (for example "running", "completed", "failed").</summary>
    public required string Status { get; init; }

    /// <summary>Gets the raw tool arguments payload.</summary>
    public required JsonElement Arguments { get; init; }
}
