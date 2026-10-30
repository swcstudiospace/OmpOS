using Autonogrammer.Presentation.Subagents;

namespace Autonogrammer.Presentation.Tests.Subagents;

/// <summary>
/// Hand-built task-call scripts. Presentation cannot see harness events, so these are the updates the panel applies.
/// The matching ACP payloads live in <c>RecordedTaskEvents</c>.
/// </summary>
internal static class RecordedTaskFrames
{
    /// <summary>Two named task calls, each with a running status and a last output line.</summary>
    public static IReadOnlyList<SubagentActivityUpdateEventArgs> WithNames { get; } =
    [
        new("call-1", "researcher", SubagentStatus.Running, null),
        new("call-1", "researcher", SubagentStatus.Running, "Searching the repo."),
        new("call-2", "writer", SubagentStatus.Running, "Drafting the note."),
    ];

    /// <summary>Two task calls that do not name an agent.</summary>
    public static IReadOnlyList<SubagentActivityUpdateEventArgs> WithoutNames { get; } =
    [
        new("call-a", null, SubagentStatus.Running, "first line"),
        new("call-b", null, SubagentStatus.Running, "second line"),
    ];

    /// <summary>A done update, a failed update and a cancelled update, plus one call that stays running.</summary>
    public static IReadOnlyList<SubagentActivityUpdateEventArgs> StatusTransitions { get; } =
    [
        new("call-run", "runner", SubagentStatus.Running, null),
        new("call-done", "finisher", SubagentStatus.Running, null),
        new("call-fail", "breaker", SubagentStatus.Running, null),
        new("call-stop", "halting", SubagentStatus.Running, null),
        new("call-done", null, SubagentStatus.Done, "finished the note"),
        new("call-fail", null, SubagentStatus.Failed, "the tool failed"),
        new("call-stop", null, SubagentStatus.Cancelled, "the call was cancelled"),
    ];

    /// <summary>Agent output that looks like markup and a link. The panel must keep it as literal text.</summary>
    public static IReadOnlyList<SubagentActivityUpdateEventArgs> MarkupOutput { get; } =
    [
        new("call-mark", "marker", SubagentStatus.Running, "<a href=\"https://evil.example\">click</a> [open](https://evil.example)"),
    ];
}
