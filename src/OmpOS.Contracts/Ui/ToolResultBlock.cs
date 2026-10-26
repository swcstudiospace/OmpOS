namespace OmpOS.Contracts.Ui;

/// <summary>
/// The output of a finished or progressing tool call (kind "tool_result"). It is a block of its own, apart from the
/// <see cref="ToolCallBlock"/> card it answers, so a transcript shows the call and what it returned as two things.
/// </summary>
public sealed record ToolResultBlock : UiBlock
{
    /// <summary>Gets the identifier of the tool call this result belongs to.</summary>
    public required string CallId { get; init; }

    /// <summary>Gets the name of the tool that produced the result.</summary>
    public required string ToolName { get; init; }

    /// <summary>Gets the status the call reported with this output (for example "in_progress", "completed", "failed").</summary>
    public required string Status { get; init; }

    /// <summary>Gets the output text, verbatim. A result block is only produced for non-empty output.</summary>
    public required string Output { get; init; }
}
