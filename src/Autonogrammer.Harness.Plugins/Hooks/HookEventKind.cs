namespace Autonogrammer.Harness.Plugins.Hooks;

/// <summary>
/// How an event treats a hook's answer. It decides the failure policy of the hook (design 3.5).
/// </summary>
public enum HookEventKind
{
    /// <summary>
    /// A permission event: <c>preToolUse</c>, <c>subagentStart</c>, <c>beforeShellExecution</c>,
    /// <c>beforeMCPExecution</c>, <c>beforeReadFile</c> and <c>beforeTabFileRead</c>. Invalid output and missing consent deny.
    /// </summary>
    Permission,

    /// <summary>
    /// An event whose answer changes what happens next without being a permission: <c>beforeSubmitPrompt</c>,
    /// <c>stop</c>, <c>subagentStop</c>, <c>sessionStart</c>, <c>postToolUse</c> and the like. A failing hook is ignored.
    /// </summary>
    Control,

    /// <summary>An event that only observes. The answer is logged and not used.</summary>
    Observe,
}
