using OmpOS.Native.Views.AgentPanel;
using OmpOS.Presentation.AgentPanel;
using OmpOS.Presentation.Kanban;

namespace OmpOS.Native.Views.Kanban;

/// <summary>The glyph and colour role of each runtime status, in the vocabulary of the Agent Panel status chip. Status is always glyph plus words.</summary>
internal static class KanbanStatusVisuals
{
    /// <summary>Returns the glyph key of a runtime status.</summary>
    /// <param name="status">The status, or null when the runtime reported none.</param>
    /// <returns>A key from <see cref="StatusIcon"/>.</returns>
    internal static string IconOf(RuntimeStatus? status) => status switch
    {
        RuntimeStatus.Queued => StatusIcon.Clock,
        RuntimeStatus.Running => StatusIcon.Running,
        RuntimeStatus.AwaitingInput => StatusIcon.Bubble,
        RuntimeStatus.NeedsAttention => StatusIcon.Alert,
        RuntimeStatus.Paused => StatusIcon.Paused,
        RuntimeStatus.Completed => StatusIcon.Check,
        RuntimeStatus.Failed => StatusIcon.Cross,
        RuntimeStatus.Unreachable => StatusIcon.Question,
        _ => StatusIcon.Ring,
    };

    /// <summary>Returns the colour role of the words of a runtime status.</summary>
    /// <param name="status">The status, or null when the runtime reported none.</param>
    /// <returns>The role.</returns>
    internal static StatusTone ToneOf(RuntimeStatus? status) => status switch
    {
        RuntimeStatus.Queued or RuntimeStatus.Running => StatusTone.Primary,
        RuntimeStatus.AwaitingInput or RuntimeStatus.NeedsAttention or RuntimeStatus.Unreachable => StatusTone.Warning,
        RuntimeStatus.Completed => StatusTone.Success,
        RuntimeStatus.Failed => StatusTone.Error,
        _ => StatusTone.Secondary,
    };
}
