namespace Autonogrammer.Harness.Plugins.Hooks;

/// <summary>
/// One hook as written in a configuration file, before the scope, working directory and consent are applied.
/// </summary>
/// <param name="EventName">
/// The event name as written. For a Cursor hook it is usually one of the 21 names; an unknown name is kept so the page can
/// show it. For an <c>omp</c> section hook it is the omp extension event name.
/// </param>
/// <param name="Event">The parsed event, or null for an unknown name and for an omp section hook.</param>
/// <param name="IsOmpEvent">True when the hook came from the <c>omp</c> section.</param>
/// <param name="Index">The zero-based position within its event array.</param>
/// <param name="Type">Command or prompt.</param>
/// <param name="Command">The shell string. Null for the <c>args</c> form and for prompt hooks.</param>
/// <param name="Args">The argument vector run without a shell, or null.</param>
/// <param name="Prompt">The prompt text of a prompt hook.</param>
/// <param name="Model">The model override of a prompt hook.</param>
/// <param name="TimeoutSeconds">The timeout in seconds, or null for the default.</param>
/// <param name="LoopLimit">The follow-up cap for <c>stop</c> and <c>subagentStop</c>. Null means no cap when <paramref name="LoopLimitSpecified"/>.</param>
/// <param name="LoopLimitSpecified">True when the file names <c>loop_limit</c>; otherwise the source's default applies.</param>
/// <param name="FailClosed">True when a failure of the hook blocks the action.</param>
/// <param name="Matcher">The matcher regex, or null.</param>
/// <param name="Id">The stable id, or null for the default <c>scope:event:index</c>.</param>
/// <param name="Enabled">False when the file switches the hook off.</param>
/// <param name="Description">A free-text description.</param>
/// <param name="Env">Extra environment variables for this hook.</param>
/// <param name="RunsIn">Where the hook may run.</param>
public sealed record HookDefinition(
    string EventName,
    HookEvent? Event,
    bool IsOmpEvent,
    int Index,
    HookType Type,
    string? Command,
    IReadOnlyList<string>? Args,
    string? Prompt,
    string? Model,
    double? TimeoutSeconds,
    int? LoopLimit,
    bool LoopLimitSpecified,
    bool FailClosed,
    string? Matcher,
    string? Id,
    bool Enabled,
    string? Description,
    IReadOnlyDictionary<string, string> Env,
    HookLocations RunsIn);
