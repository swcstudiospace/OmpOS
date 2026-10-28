using System.Collections.Frozen;
using OmpOS.Harness.Plugins.Hooks;

namespace OmpOS.AgentWorker.Hooks;

/// <summary>
/// The hook events a cloud worker runs: the fourteen events Cursor supports in cloud agents (research 08, section 3.2). The session and
/// workspace lifecycle events (<c>sessionStart</c>, <c>sessionEnd</c>, <c>workspaceOpen</c>), the MCP events and the two Tab events are not
/// run, so a hook file written for the desktop that names them keeps working there and is skipped here, and the worker never reports one of
/// them as working.
/// </summary>
internal static class CloudHookEvents
{
    /// <summary>Gets the events a cloud worker runs.</summary>
    public static FrozenSet<HookEvent> Supported { get; } = new[]
    {
        HookEvent.BeforeShellExecution,
        HookEvent.AfterShellExecution,
        HookEvent.BeforeReadFile,
        HookEvent.AfterFileEdit,
        HookEvent.PreToolUse,
        HookEvent.PostToolUse,
        HookEvent.PostToolUseFailure,
        HookEvent.SubagentStart,
        HookEvent.SubagentStop,
        HookEvent.BeforeSubmitPrompt,
        HookEvent.PreCompact,
        HookEvent.AfterAgentResponse,
        HookEvent.AfterAgentThought,
        HookEvent.Stop,
    }.ToFrozenSet();

    /// <summary>Tells whether a cloud worker runs hooks of an event.</summary>
    /// <param name="hookEvent">The event.</param>
    /// <returns><see langword="true"/> for one of the fourteen supported events.</returns>
    public static bool IsSupported(HookEvent hookEvent) => Supported.Contains(hookEvent);
}
