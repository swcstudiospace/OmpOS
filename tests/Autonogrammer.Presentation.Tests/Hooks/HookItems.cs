using Autonogrammer.Presentation.Hooks;

namespace Autonogrammer.Presentation.Tests.Hooks;

/// <summary>Builds hooks and runs for the Hooks page tests.</summary>
internal static class HookItems
{
    /// <summary>Builds a consented, enabled shell hook.</summary>
    /// <param name="id">The identifier, which is also the display name.</param>
    /// <param name="eventName">The event.</param>
    /// <param name="scope">The scope.</param>
    /// <param name="file">The file that declares the hook.</param>
    /// <param name="command">The shell string.</param>
    /// <returns>The hook.</returns>
    public static HookItem Hook(string id, string eventName = "beforeShellExecution", HookScopeKind scope = HookScopeKind.Project, string file = "/work/.cursor/hooks.json", string command = "./guard.sh") => new()
    {
        Id = id,
        DisplayName = id,
        EventName = eventName,
        Scope = scope,
        SourceFile = file,
        Command = command,
        ShellName = "PosixSh",
        TimeoutSeconds = 30,
        IsConsented = true,
        IsGuarding = true,
    };

    /// <summary>Builds a run of a hook.</summary>
    /// <param name="hookId">The hook identifier.</param>
    /// <param name="outcome">What happened.</param>
    /// <param name="durationMs">How long the hook ran.</param>
    /// <param name="stdout">What the hook printed, or null.</param>
    /// <param name="at">When it finished.</param>
    /// <returns>The run.</returns>
    public static HookRunItem Run(string hookId, HookRunOutcome outcome = HookRunOutcome.Succeeded, long durationMs = 120, string? stdout = null, DateTimeOffset? at = null) => new()
    {
        At = at ?? new DateTimeOffset(2026, 10, 27, 12, 0, 0, TimeSpan.Zero),
        SessionId = "s1",
        EventName = "beforeShellExecution",
        HookId = hookId,
        HookName = hookId,
        Scope = HookScopeKind.Project,
        Outcome = outcome,
        DurationMs = durationMs,
        StandardOutputExcerpt = stdout,
    };

    /// <summary>Builds a snapshot of hooks with the run hooks switch on.</summary>
    /// <param name="hooks">The hooks.</param>
    /// <returns>The snapshot.</returns>
    public static HooksSnapshot Snapshot(params HookItem[] hooks) => new() { RunHooks = true, WorkspaceRoot = "/work", Hooks = hooks };
}
