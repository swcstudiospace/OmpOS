namespace Autonogrammer.Harness.Plugins.Hooks;

/// <summary>
/// The 21 hook events of Cursor 3.27, https://cursor.com/docs/hooks. The wire name of each event is camelCase and is
/// produced by <see cref="HookEvents.Format"/>, for example <c>beforeMCPExecution</c>. A hook file may name an event
/// this enum does not know yet; the loader keeps such a hook as an unknown event and never fails the whole file.
/// </summary>
public enum HookEvent
{
    /// <summary>A conversation starts. Fire-and-forget; the hook may return <c>env</c> and <c>additional_context</c>.</summary>
    SessionStart,

    /// <summary>A conversation ends. Fire-and-forget.</summary>
    SessionEnd,

    /// <summary>Before any tool runs. A hook can deny it or rewrite its input.</summary>
    PreToolUse,

    /// <summary>After a tool succeeded. A hook can add context or replace an MCP tool's output.</summary>
    PostToolUse,

    /// <summary>After a tool failed, timed out or was denied. A hook can add context.</summary>
    PostToolUseFailure,

    /// <summary>Before a subagent is spawned. A hook can deny it.</summary>
    SubagentStart,

    /// <summary>After a subagent finished. A hook can request a follow-up.</summary>
    SubagentStop,

    /// <summary>Before a shell command runs. A hook can allow, deny or ask.</summary>
    BeforeShellExecution,

    /// <summary>After a shell command ran. Observe only.</summary>
    AfterShellExecution,

    /// <summary>Before an MCP tool runs. A hook can allow, deny or ask.</summary>
    BeforeMcpExecution,

    /// <summary>After an MCP tool ran. Observe only.</summary>
    AfterMcpExecution,

    /// <summary>Before the agent reads a file. A hook can deny the read.</summary>
    BeforeReadFile,

    /// <summary>After the agent edited a file. Observe only.</summary>
    AfterFileEdit,

    /// <summary>After the user sent a prompt and before it reaches the agent. A hook can block it.</summary>
    BeforeSubmitPrompt,

    /// <summary>Before the context is compacted. Observe only.</summary>
    PreCompact,

    /// <summary>When the agent loop ends. A hook can request a follow-up prompt.</summary>
    Stop,

    /// <summary>After the agent finished an assistant message. Observe only.</summary>
    AfterAgentResponse,

    /// <summary>After the agent finished a thinking block. Observe only.</summary>
    AfterAgentThought,

    /// <summary>Before Tab (inline completion) reads a file. Autonogrammer has no Tab engine, so this event never fires.</summary>
    BeforeTabFileRead,

    /// <summary>After Tab (inline completion) edited a file. Autonogrammer has no Tab engine, so this event never fires.</summary>
    AfterTabFileEdit,

    /// <summary>A workspace opened or its folders changed. Outside any agent session.</summary>
    WorkspaceOpen,
}
