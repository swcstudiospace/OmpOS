using Autonogrammer.Harness.Plugins.Hooks;

namespace Autonogrammer.Harness.AgentLoop.Hooks;

/// <summary>
/// What the user approved the last time they gave consent to a hook: every field that is part of the hook's consent identity, in a form
/// the Hooks page can show and compare. When the hook changes, the page shows the difference between the hook as it is now and this record.
/// </summary>
public sealed record HookApprovalSnapshot
{
    /// <summary>Gets the hook's identifier.</summary>
    public required string HookId { get; init; }

    /// <summary>Gets the event name as written in the file.</summary>
    public required string EventName { get; init; }

    /// <summary>Gets the scope the hook came from, as the name of the scope.</summary>
    public required string Scope { get; init; }

    /// <summary>Gets the file that declared the hook.</summary>
    public required string SourceFile { get; init; }

    /// <summary>Gets the shell string, or null for an argument-vector hook and a prompt hook.</summary>
    public string? Command { get; init; }

    /// <summary>Gets the argument vector run without a shell, or null.</summary>
    public IReadOnlyList<string>? Args { get; init; }

    /// <summary>Gets the shell the command ran in, as the name of the shell.</summary>
    public required string Shell { get; init; }

    /// <summary>Gets the prompt text of a prompt hook, or null.</summary>
    public string? Prompt { get; init; }

    /// <summary>Gets the matcher text, or null.</summary>
    public string? Matcher { get; init; }

    /// <summary>Gets the timeout in seconds.</summary>
    public required int TimeoutSeconds { get; init; }

    /// <summary>Gets a value indicating whether a failure of the hook blocks the action.</summary>
    public bool FailClosed { get; init; }

    /// <summary>Gets the names of the extra environment variables of the hook. The values are not stored.</summary>
    public IReadOnlyList<string> EnvKeys { get; init; } = [];

    /// <summary>Gets the working directory of the hook relative to its scope root, or the absolute directory when it is outside it.</summary>
    public required string WorkingDirectory { get; init; }

    /// <summary>Gets the consent identity the approval was recorded for.</summary>
    public required string CommandHash { get; init; }

    /// <summary>Gets when the user approved.</summary>
    public required DateTimeOffset ApprovedAt { get; init; }

    /// <summary>Describes a hook as it is now, for the approval record and for the page's comparison with the approved version.</summary>
    /// <param name="hook">The hook.</param>
    /// <param name="approvedAt">The time stamped on the record.</param>
    /// <returns>The snapshot.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="hook"/> is null.</exception>
    public static HookApprovalSnapshot From(EffectiveHook hook, DateTimeOffset approvedAt)
    {
        ArgumentNullException.ThrowIfNull(hook);
        var relative = Path.GetRelativePath(hook.ScopeRoot, hook.WorkingDirectory);
        var inside = !relative.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathRooted(relative);
        return new HookApprovalSnapshot
        {
            HookId = hook.Id,
            EventName = hook.EventName,
            Scope = hook.Scope.ToString(),
            SourceFile = hook.SourceFile,
            Command = hook.Command,
            Args = hook.Args is null ? null : [.. hook.Args],
            Shell = hook.Shell.ToString(),
            Prompt = hook.Prompt,
            Matcher = hook.Matcher,
            TimeoutSeconds = (int)hook.Timeout.TotalSeconds,
            FailClosed = hook.FailClosed,
            EnvKeys = [.. hook.Env.Keys.Order(StringComparer.Ordinal)],
            WorkingDirectory = inside ? relative : hook.WorkingDirectory,
            CommandHash = hook.CommandHash,
            ApprovedAt = approvedAt,
        };
    }
}
