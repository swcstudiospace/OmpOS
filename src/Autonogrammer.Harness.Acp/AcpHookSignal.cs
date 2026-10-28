using System.Text.Json;

namespace Autonogrammer.Harness.Acp;

/// <summary>
/// One observation from the ACP traffic: a tool call, a tool call update, an aggregated assistant message or thinking block, or a plan.
/// Only the members that belong to <see cref="Kind"/> are set.
/// </summary>
public sealed record AcpHookSignal
{
    /// <summary>Gets what was observed.</summary>
    public required AcpHookSignalKind Kind { get; init; }

    /// <summary>Gets the tool call identifier for tool call signals.</summary>
    public string? ToolCallId { get; init; }

    /// <summary>Gets the display title of the tool call, when the agent sent one.</summary>
    public string? Title { get; init; }

    /// <summary>Gets the ACP tool kind (<c>execute</c>, <c>read</c>, <c>edit</c>, ...), when the agent sent one.</summary>
    public string? ToolKind { get; init; }

    /// <summary>Gets the ACP tool call status (<c>pending</c>, <c>in_progress</c>, <c>completed</c>, <c>failed</c>), when the agent sent one.</summary>
    public string? Status { get; init; }

    /// <summary>Gets the raw tool input (<c>rawInput</c>) of a tool call, when the agent sent one.</summary>
    public JsonElement? RawInput { get; init; }

    /// <summary>Gets the raw tool output (<c>rawOutput</c>) of a tool call update, when the agent sent one.</summary>
    public JsonElement? RawOutput { get; init; }

    /// <summary>Gets the text of the signal: the output text of a tool call update, the aggregated message or thought, or the plan checklist.</summary>
    public string? Text { get; init; }

    /// <summary>Gets how long a thinking block took, measured from its first to its last chunk.</summary>
    public TimeSpan? Duration { get; init; }
}
