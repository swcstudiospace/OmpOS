using Autonogrammer.Presentation.AgentPanel;

namespace Autonogrammer.UI.Components.Blocks;

/// <summary>Maps the status text of a tool call to the glyph key of the Agent Panel glyph table. The status words stay beside the glyph: a status is never colour alone.</summary>
public static class ToolStatusIcon
{
    /// <summary>Returns the glyph key of a tool status.</summary>
    /// <param name="status">The status as the engine reported it, for example <c>completed</c>, <c>failed</c>, <c>in_progress</c> or <c>pending</c>.</param>
    /// <returns>A key of <see cref="StatusIcon"/>; an unknown status draws the ring.</returns>
    public static string Of(string? status) => status?.ToUpperInvariant() switch
    {
        "COMPLETED" or "COMPLETE" or "DONE" or "SUCCEEDED" or "SUCCESS" => StatusIcon.Check,
        "FAILED" or "ERROR" or "CANCELLED" or "CANCELED" => StatusIcon.Cross,
        "IN_PROGRESS" or "RUNNING" or "STARTED" => StatusIcon.Running,
        "PENDING" or "QUEUED" => StatusIcon.Clock,
        _ => StatusIcon.Ring,
    };
}
