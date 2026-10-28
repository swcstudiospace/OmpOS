using System.Globalization;

namespace OmpOS.UI.Components;

/// <summary>
/// The labels and accessible names of the Agent Panel chrome that <c>AgentPanelCopy</c> (Presentation) does not hold: control names, group
/// captions and the composer hint. They are the same words the desktop Agent Panel uses, so the two heads speak alike. Every sentence that
/// depends on the Copywriting Contract stays in <c>AgentPanelCopy</c>; a view reads these constants and never writes a literal.
/// </summary>
public static class AgentPanelViewText
{
    /// <summary>The most characters of the task title the task chip shows before the ellipsis.</summary>
    public const int TaskChipChars = 28;

    /// <summary>The accessible name of the agent list.</summary>
    public const string AgentSessionsName = "Agent sessions";

    /// <summary>The accessible name of the row actions.</summary>
    public const string AgentActionsName = "Agent actions";

    /// <summary>The flag a long-running row carries after its status.</summary>
    public const string LongRunning = "Long-running";

    /// <summary>The accessible name of the thread header.</summary>
    public const string HeaderName = "Agent header";

    /// <summary>The label of the Close button.</summary>
    public const string CloseLabel = "Close";

    /// <summary>The accessible name of the Close button.</summary>
    public const string CloseName = "Close this agent";

    /// <summary>The accessible name of the panel notice.</summary>
    public const string PanelNoticeName = "Panel notice";

    /// <summary>The label and name of the Dismiss notice button.</summary>
    public const string DismissNotice = "Dismiss notice";

    /// <summary>The accessible name of the stop confirmation bar.</summary>
    public const string ConfirmStopName = "Confirm stop";

    /// <summary>The accessible name of the close confirmation bar.</summary>
    public const string ConfirmCloseName = "Confirm close";

    /// <summary>The accessible name of the pipeline strip live line.</summary>
    public const string PipelineAnnouncementsName = "Pipeline announcements";

    /// <summary>The accessible name of the pipeline details.</summary>
    public const string PipelineDetailsName = "Pipeline details";

    /// <summary>The label of the Show pipeline button.</summary>
    public const string ShowPipeline = "Show pipeline";

    /// <summary>The accessible name of the Show pipeline button.</summary>
    public const string ShowPipelineName = "Show pipeline details";

    /// <summary>The label of the Kanban row of the task card.</summary>
    public const string KanbanLabel = "Kanban";

    /// <summary>The label of the Graph row of the task card.</summary>
    public const string GraphLabel = "Graph";

    /// <summary>The label of the Workflow row of the task card.</summary>
    public const string WorkflowLabel = "Workflow";

    /// <summary>The heading of the run summary block.</summary>
    public const string RunSummaryHeading = "Run summary";

    /// <summary>The accessible name of the steering history list.</summary>
    public const string SteeringEntriesName = "Steering history entries";

    /// <summary>The hint line under the composer.</summary>
    public const string ComposerHint =
        "Enter sends. Shift+Enter adds a line. While a run is active: Ctrl+Enter steers, Ctrl+Shift+Enter queues a follow-up, Esc stops.";

    /// <summary>The accessible name of the composer hint line.</summary>
    public const string ComposerHintName = "Composer hint";

    /// <summary>The visible label of the follow-up button while a run is active.</summary>
    public const string QueueFollowUp = "Queue follow-up";

    /// <summary>The accessible name of the follow-up button.</summary>
    public const string QueueFollowUpName = "Queue follow-up message";

    /// <summary>The accessible name of the primary button while a run is active and the engine cannot take a live steer.</summary>
    public const string SteerName = "Send now, steer the running agent";

    /// <summary>The pending line when one follow-up is queued.</summary>
    public const string QueuedOne = "1 follow-up is queued. It starts when the active run completes.";

    /// <summary>The pending line while a run is active and nothing is queued, for an engine without live steering.</summary>
    public const string RunActiveIdle = "A run is active. Send waits until the active turn ends; Queue waits until the run completes.";

    /// <summary>Returns the pending line when several follow-ups are queued.</summary>
    /// <param name="count">The number of queued follow-ups.</param>
    /// <returns>The sentence.</returns>
    public static string QueuedMany(int count) =>
        string.Create(CultureInfo.InvariantCulture, $"{count} follow-ups are queued. They start one at a time when the active run completes.");

    /// <summary>Returns the accessible name of a status chip.</summary>
    /// <param name="statusText">The status in words.</param>
    /// <returns><c>Status: {text}</c>.</returns>
    public static string StatusName(string? statusText) => string.Create(CultureInfo.InvariantCulture, $"Status: {statusText}");

    /// <summary>Returns the text of the task chip, with the title cut at <see cref="TaskChipChars"/> characters.</summary>
    /// <param name="title">The task title.</param>
    /// <returns><c>Task: {title}</c>.</returns>
    public static string TaskChipText(string? title)
    {
        var text = title ?? string.Empty;
        var shown = text.Length <= TaskChipChars ? text : string.Concat(text.AsSpan(0, TaskChipChars - 1), "\u2026");
        return string.Create(CultureInfo.InvariantCulture, $"Task: {shown}");
    }

    /// <summary>Returns the accessible name of the task chip, with the full title.</summary>
    /// <param name="title">The task title.</param>
    /// <returns><c>Task {title}. Open task context.</c>.</returns>
    public static string TaskChipName(string? title) => string.Create(CultureInfo.InvariantCulture, $"Task {title}. Open task context.");

    /// <summary>Returns the text and the accessible name of the graph chip.</summary>
    /// <param name="graphId">The Graph identifier.</param>
    /// <returns><c>Graph {id}</c>.</returns>
    public static string GraphChip(string? graphId) => string.Create(CultureInfo.InvariantCulture, $"Graph {graphId}");

    /// <summary>Returns the visible line under the pipeline strip.</summary>
    /// <param name="announcement">The latest announcement.</param>
    /// <returns><c>Latest: {announcement}</c>.</returns>
    public static string LatestLine(string announcement) => string.Create(CultureInfo.InvariantCulture, $"Latest: {announcement}");
}
