namespace Autonogrammer.Presentation.Hooks;

/// <summary>What you approved the last time you gave consent to a hook, field by field, so the page can show what changed since.</summary>
/// <param name="Command">The shell string, or null.</param>
/// <param name="Arguments">The argument vector run without a shell, or null.</param>
/// <param name="ShellName">The shell the command ran in, or null.</param>
/// <param name="Prompt">The prompt text of a prompt hook, or null.</param>
/// <param name="Matcher">The matcher, or null.</param>
/// <param name="TimeoutSeconds">The timeout in seconds.</param>
/// <param name="FailClosed">Whether a failure of the hook blocks the action.</param>
/// <param name="EnvKeys">The names of the hook's extra environment variables.</param>
/// <param name="WorkingDirectory">The working directory relative to the scope root.</param>
/// <param name="ApprovedAt">When you approved.</param>
public sealed record HookApprovedVersion(
    string? Command,
    IReadOnlyList<string>? Arguments,
    string? ShellName,
    string? Prompt,
    string? Matcher,
    int TimeoutSeconds,
    bool FailClosed,
    IReadOnlyList<string> EnvKeys,
    string WorkingDirectory,
    DateTimeOffset ApprovedAt);
