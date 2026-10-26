using System.Globalization;

namespace OmpOS.Native.Views.AgentPanel;

/// <summary>
/// The labels and accessible names of the Agent Panel chrome that <c>AgentPanelCopy</c> does not hold: control names, group captions and the
/// two sentences of the composer. Every sentence that depends on the Copywriting Contract stays in <c>AgentPanelCopy</c>; a view reads
/// these constants and never writes a literal.
/// </summary>
internal static class AgentPanelViewText
{
    /// <summary>The most characters of the task title the task chip shows before the ellipsis.</summary>
    public const int TaskChipChars = 28;

    /// <summary>The tooltip of the New agent button.</summary>
    public const string NewAgentTooltip = "New agent (Ctrl+N)";

    /// <summary>The accessible name of the rail filter box.</summary>
    public const string FilterName = "Filter agents";

    /// <summary>The accessible name of the agent list.</summary>
    public const string AgentSessionsName = "Agent sessions";

    /// <summary>The accessible name of the row context menu.</summary>
    public const string AgentActionsName = "Agent actions";

    /// <summary>The flag a long-running row carries after its chip.</summary>
    public const string LongRunning = "Long-running";

    /// <summary>The accessible name of the panel error banner.</summary>
    public const string PanelErrorName = "Panel error";

    /// <summary>The accessible name of the thread header.</summary>
    public const string HeaderName = "Agent header";

    /// <summary>The accessible name of the thread title.</summary>
    public const string TitleName = "Agent title";

    /// <summary>The label of the Close button.</summary>
    public const string CloseLabel = "Close";

    /// <summary>The accessible name of the Close button.</summary>
    public const string CloseName = "Close this agent";

    /// <summary>The accessible name of the panel notice.</summary>
    public const string PanelNoticeName = "Panel notice";

    /// <summary>The label of the Show pipeline button.</summary>
    public const string ShowPipeline = "Show pipeline";

    /// <summary>The accessible name of the Show pipeline button.</summary>
    public const string ShowPipelineName = "Show pipeline details";

    /// <summary>The label and name of the Dismiss notice button.</summary>
    public const string DismissNotice = "Dismiss notice";

    /// <summary>The accessible name of the stop confirmation bar.</summary>
    public const string ConfirmStopName = "Confirm stop";

    /// <summary>The accessible name of the close confirmation bar.</summary>
    public const string ConfirmCloseName = "Confirm close";

    /// <summary>The accessible name of the pipeline strip live line.</summary>
    public const string PipelineAnnouncementsName = "Pipeline announcements";

    /// <summary>The accessible name of the Pipeline tab scroller.</summary>
    public const string PipelineDetailsName = "Pipeline details";

    /// <summary>The accessible name of the Context tab scroller.</summary>
    public const string TaskContextDetailsName = "Task context details";

    /// <summary>The accessible name of the Steering tab scroller.</summary>
    public const string SteeringEntriesName = "Steering history entries";

    /// <summary>The heading of the run summary block.</summary>
    public const string RunSummaryHeading = "Run summary";

    /// <summary>The label of the Kanban row of the task card.</summary>
    public const string KanbanLabel = "Kanban";

    /// <summary>The label of the Graph row of the task card.</summary>
    public const string GraphLabel = "Graph";

    /// <summary>The label of the Workflow row of the task card.</summary>
    public const string WorkflowLabel = "Workflow";

    /// <summary>The hint line under the composer.</summary>
    public const string ComposerHint =
        "Enter sends. Shift+Enter adds a line. While a run is active: Ctrl+Enter steers, Ctrl+Shift+Enter queues a follow-up, Esc stops.";

    /// <summary>The accessible name of the composer hint line.</summary>
    public const string ComposerHintName = "Composer hint";

    /// <summary>The visible label of the follow-up button while no run is active.</summary>
    public const string FollowUp = "Follow-up";

    /// <summary>The visible label of the follow-up button while a run is active.</summary>
    public const string QueueFollowUp = "Queue follow-up";

    /// <summary>The accessible name of the follow-up button.</summary>
    public const string QueueFollowUpName = "Queue follow-up message";

    /// <summary>Returns the accessible name of a status chip.</summary>
    /// <param name="statusText">The status in words.</param>
    /// <returns><c>Status: {text}</c>.</returns>
    internal static string StatusName(string? statusText) => string.Create(CultureInfo.InvariantCulture, $"Status: {statusText}");

    /// <summary>Returns the text of the task chip, with the title cut at <see cref="TaskChipChars"/> characters.</summary>
    /// <param name="title">The task title.</param>
    /// <returns><c>Task: {title}</c>.</returns>
    internal static string TaskChipText(string? title)
    {
        var text = title ?? string.Empty;
        var shown = text.Length <= TaskChipChars ? text : string.Concat(text.AsSpan(0, TaskChipChars - 1), "\u2026");
        return string.Create(CultureInfo.InvariantCulture, $"Task: {shown}");
    }

    /// <summary>Returns the accessible name of the task chip, with the full title.</summary>
    /// <param name="title">The task title.</param>
    /// <returns><c>Task {title}. Open task context.</c>.</returns>
    internal static string TaskChipName(string? title) => string.Create(CultureInfo.InvariantCulture, $"Task {title}. Open task context.");

    /// <summary>Returns the text and the accessible name of the graph chip.</summary>
    /// <param name="graphId">The Graph identifier.</param>
    /// <returns><c>Graph {id}</c>.</returns>
    internal static string GraphChip(string? graphId) => string.Create(CultureInfo.InvariantCulture, $"Graph {graphId}");

    /// <summary>Returns the visible line under the pipeline strip.</summary>
    /// <param name="announcement">The latest announcement.</param>
    /// <returns><c>Latest: {announcement}</c>.</returns>
    internal static string LatestLine(string announcement) => string.Create(CultureInfo.InvariantCulture, $"Latest: {announcement}");
}
