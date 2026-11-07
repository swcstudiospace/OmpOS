namespace Autonogrammer.Presentation.Subagents;

/// <summary>Screen copy for the sub-agent panel. English only, in one place.</summary>
public static class SubagentCopy
{
    /// <summary>The accessible name of the panel.</summary>
    public const string PanelName = "Sub-agents";

    /// <summary>The notice that this view reads the ACP stream only.</summary>
    public const string AcpOnlyNotice = "Sub-agent view limited to ACP.";

    /// <summary>The notice shown when a task call did not name its agent.</summary>
    public const string NamesUnavailable = "Sub-agent names are unavailable.";

    /// <summary>The accessible name of the running-count line.</summary>
    public const string RunningCountName = "Running sub-agents";

    /// <summary>The accessible name of the status live region.</summary>
    public const string LiveRegionName = "Sub-agent status";

    /// <summary>The accessible name of the row list.</summary>
    public const string RowsName = "Sub-agent rows";

    /// <summary>Builds the fallback name for an unnamed task call. <paramref name="number"/> starts at 1.</summary>
    /// <param name="number">The 1-based index among unnamed rows.</param>
    /// <returns>The label <c>Sub-agent N</c>.</returns>
    public static string Unnamed(int number) => $"Sub-agent {number}";

    /// <summary>The word the panel shows for <paramref name="status"/>.</summary>
    /// <param name="status">The row status.</param>
    /// <returns>running, done, failed or cancelled.</returns>
    public static string StatusLabel(SubagentStatus status) => status switch
    {
        SubagentStatus.Done => "done",
        SubagentStatus.Failed => "failed",
        SubagentStatus.Cancelled => "cancelled",
        _ => "running",
    };
}
