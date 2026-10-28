using System.Text.RegularExpressions;

namespace Autonogrammer.Harness.Plugins.Hooks;

/// <summary>
/// One hook ready to run: a <see cref="HookDefinition"/> with its scope, working directory, defaults and consent identity
/// applied. The effective set orders these in merge order.
/// </summary>
/// <param name="Id">The stable identifier shown on the Hooks page and used for enable state. It is unique in the set.</param>
/// <param name="PluginId">The plugin or pseudo plugin the hook is grouped under, for quarantine and the page.</param>
/// <param name="Scope">Where the hook comes from.</param>
/// <param name="SourceFile">The file that declares the hook, or the plugin manifest identifier.</param>
/// <param name="EventName">The event name as written.</param>
/// <param name="Event">The parsed event, or null for an unknown event or an omp section hook.</param>
/// <param name="IsOmpEvent">True for an <c>omp</c> section hook.</param>
/// <param name="Index">The position within its event array.</param>
/// <param name="Type">Command or prompt.</param>
/// <param name="Command">The shell string, or null.</param>
/// <param name="Args">The argument vector run without a shell, or null.</param>
/// <param name="Shell">The shell the command runs in.</param>
/// <param name="Prompt">The prompt text of a prompt hook.</param>
/// <param name="Model">The model override of a prompt hook.</param>
/// <param name="Timeout">The time the hook may run before its process tree is killed.</param>
/// <param name="LoopLimit">The follow-up cap for <c>stop</c> and <c>subagentStop</c>; null means no cap.</param>
/// <param name="FailClosed">True when a failure of the hook blocks the action.</param>
/// <param name="Matcher">The matcher text, or null.</param>
/// <param name="Enabled">False when the file switches the hook off.</param>
/// <param name="Description">A free-text description.</param>
/// <param name="Env">Extra environment variables for this hook.</param>
/// <param name="RunsIn">Where the hook may run.</param>
/// <param name="WorkingDirectory">The absolute directory the hook runs in.</param>
/// <param name="ScopeRoot">The absolute directory the consent identity is relative to.</param>
/// <param name="CommandHash">The consent identity of the hook.</param>
/// <param name="HashVersion">1 for a plugin manifest hook, 2 for a <c>hooks.json</c> hook.</param>
/// <param name="PreConsented">True for a managed hook whose file is tamper resistant; no user consent is needed.</param>
/// <param name="Blocking">
/// True when the hook guards an action: a hook on a permission event, or a plugin manifest hook declared blocking. Without
/// consent a blocking hook denies instead of being skipped.
/// </param>
/// <param name="CompiledMatcher">The compiled matcher, or null for a match-everything or invalid matcher.</param>
/// <param name="MatcherProblem">Why the matcher is invalid, or null.</param>
public sealed record EffectiveHook(
    string Id,
    string PluginId,
    HookScope Scope,
    string SourceFile,
    string EventName,
    HookEvent? Event,
    bool IsOmpEvent,
    int Index,
    HookType Type,
    string? Command,
    IReadOnlyList<string>? Args,
    HookShell Shell,
    string? Prompt,
    string? Model,
    TimeSpan Timeout,
    int? LoopLimit,
    bool FailClosed,
    string? Matcher,
    bool Enabled,
    string? Description,
    IReadOnlyDictionary<string, string> Env,
    HookLocations RunsIn,
    string WorkingDirectory,
    string ScopeRoot,
    string CommandHash,
    int HashVersion,
    bool PreConsented,
    bool Blocking,
    Regex? CompiledMatcher,
    string? MatcherProblem)
{
    /// <summary>
    /// True when the hook guards a permission event, so a failure of it must be treated by the permission rules.
    /// </summary>
    public bool IsPermissionHook => Event is { } hookEvent && HookEvents.IsPermission(hookEvent);
}
