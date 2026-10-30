namespace Autonogrammer.Presentation.Hooks;

/// <summary>One hook of the effective set, as a source reports it to the Hooks page: what it is, where it comes from, and its state.</summary>
public sealed record HookItem
{
    /// <summary>Gets the stable identifier of the hook.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the name shown for the hook: its description, or its event and position.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Gets the event name as written in the file.</summary>
    public required string EventName { get; init; }

    /// <summary>Gets a value indicating whether the hook is in the <c>omp</c> section of its file.</summary>
    public bool IsOmpEvent { get; init; }

    /// <summary>Gets a value indicating whether Autonogrammer knows the event name. An unknown event is kept and never runs.</summary>
    public bool IsKnownEvent { get; init; } = true;

    /// <summary>Gets a value indicating whether Autonogrammer can produce the event. The Cursor Tab events cannot be produced.</summary>
    public bool IsApplicable { get; init; } = true;

    /// <summary>Gets where the hook comes from.</summary>
    public required HookScopeKind Scope { get; init; }

    /// <summary>Gets the file that declares the hook, or the plugin that declares it.</summary>
    public required string SourceFile { get; init; }

    /// <summary>Gets a value indicating whether the hook is a prompt hook that asks a model instead of running a process.</summary>
    public bool IsPrompt { get; init; }

    /// <summary>Gets the shell string, or null for an argument-vector hook and a prompt hook.</summary>
    public string? Command { get; init; }

    /// <summary>Gets the argument vector run without a shell, or null.</summary>
    public IReadOnlyList<string>? Arguments { get; init; }

    /// <summary>Gets the name of the shell a command runs in, or null when it runs without one.</summary>
    public string? ShellName { get; init; }

    /// <summary>Gets the prompt text of a prompt hook, or null.</summary>
    public string? Prompt { get; init; }

    /// <summary>Gets the matcher text, or null for a hook that matches everything.</summary>
    public string? Matcher { get; init; }

    /// <summary>Gets why the matcher is not a valid regular expression, or null.</summary>
    public string? MatcherProblem { get; init; }

    /// <summary>Gets the time the hook may run, in seconds.</summary>
    public int TimeoutSeconds { get; init; }

    /// <summary>Gets a value indicating whether a failure of the hook blocks the action.</summary>
    public bool FailClosed { get; init; }

    /// <summary>Gets the follow-up cap of a <c>stop</c> hook, or null for none.</summary>
    public int? LoopLimit { get; init; }

    /// <summary>Gets the names of the hook's extra environment variables. The values are never shown.</summary>
    public IReadOnlyList<string> EnvKeys { get; init; } = [];

    /// <summary>Gets the working directory of the hook relative to its scope root.</summary>
    public string WorkingDirectory { get; init; } = ".";

    /// <summary>Gets the free-text description, or null.</summary>
    public string? Description { get; init; }

    /// <summary>Gets a value indicating whether the hook may run: its file and you have not switched it off.</summary>
    public bool IsEnabled { get; init; } = true;

    /// <summary>Gets a value indicating whether the hook guards an action, so that without consent it blocks the action instead of being skipped.</summary>
    public bool IsGuarding { get; init; }

    /// <summary>Gets a value indicating whether consent is recorded for the exact hook, or the hook is pre-consented.</summary>
    public bool IsConsented { get; init; }

    /// <summary>Gets a value indicating whether the hook is pre-consented (a managed hook whose file nobody else can write). It needs no consent and offers no revoke.</summary>
    public bool IsPreConsented { get; init; }

    /// <summary>Gets the identifier of the hook source that is quarantined, or null.</summary>
    public string? QuarantinedSource { get; init; }

    /// <summary>Gets why the source is quarantined, or null.</summary>
    public string? QuarantineReason { get; init; }

    /// <summary>Gets the most recent run of the hook, or null when it has not been considered yet.</summary>
    public HookRunItem? LastRun { get; init; }

    /// <summary>Gets the version you last approved, or null when you never did.</summary>
    public HookApprovedVersion? LastApproved { get; init; }
}
