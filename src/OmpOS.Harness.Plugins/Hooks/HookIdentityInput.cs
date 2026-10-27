namespace OmpOS.Harness.Plugins.Hooks;

/// <summary>
/// Everything that identifies a hook for consent, version 2. It never contains an absolute path.
/// </summary>
/// <param name="EventName">The event name as written.</param>
/// <param name="IsOmpEvent">True for an <c>omp</c> section hook.</param>
/// <param name="Scope">The scope the hook comes from.</param>
/// <param name="Type">Command or prompt.</param>
/// <param name="Command">The shell string, or null.</param>
/// <param name="Args">The argument vector, or null.</param>
/// <param name="Shell">The shell the command runs in.</param>
/// <param name="RelativeWorkingDirectory">The working directory relative to the scope root, <c>.</c> for the root itself.</param>
/// <param name="Env">The hook's own environment variables.</param>
/// <param name="Prompt">The prompt text of a prompt hook.</param>
/// <param name="Model">The model override of a prompt hook.</param>
/// <param name="FailClosed">Whether a failure blocks the action.</param>
/// <param name="Matcher">The matcher text.</param>
/// <param name="TimeoutSeconds">The effective timeout in seconds.</param>
/// <param name="LoopLimit">The effective follow-up cap.</param>
/// <param name="RunsIn">Where the hook may run.</param>
/// <param name="Executables">The digests of the scripts the command names inside the scope root.</param>
public sealed record HookIdentityInput(
    string EventName,
    bool IsOmpEvent,
    HookScope Scope,
    HookType Type,
    string? Command,
    IReadOnlyList<string>? Args,
    HookShell Shell,
    string RelativeWorkingDirectory,
    IReadOnlyDictionary<string, string> Env,
    string? Prompt,
    string? Model,
    bool FailClosed,
    string? Matcher,
    double TimeoutSeconds,
    int? LoopLimit,
    HookLocations RunsIn,
    IReadOnlyList<HookExecutableDigest> Executables);
