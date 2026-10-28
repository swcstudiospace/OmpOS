using OmpOS.Contracts.WorkspaceSettings;
using OmpOS.Harness.Plugins.Hooks;

namespace OmpOS.Api.Services.WorkspaceSettings;

/// <summary>
/// Reads the effective hooks of the server workspace with <see cref="HooksConfigLoader"/> over a <see cref="ContainedHookFileSource"/>. The loader only
/// parses files and hashes the scripts they name: this class has no way to start a hook, and the API project cannot reference a process type at all.
/// </summary>
internal sealed class HooksReader
{
    private readonly WorkspaceAccess _access;

    /// <summary>Initializes a new instance of the <see cref="HooksReader"/> class.</summary>
    /// <param name="access">What the endpoints may read.</param>
    public HooksReader(WorkspaceAccess access)
    {
        _access = access;
    }

    /// <summary>Loads the hooks now. Each call reads again.</summary>
    /// <param name="cancellationToken">Cancels the load.</param>
    /// <returns>The hooks, or an unavailable answer that says why nothing was read.</returns>
    public async Task<HooksResponse> ReadAsync(CancellationToken cancellationToken)
    {
        if (_access.ExistingRoot is not { } root)
        {
            return Unavailable(_access.UnavailableReason() ?? "The workspace folder of the API cannot be read.");
        }

        var home = _access.UserHome;
        EffectiveHookSet set;
        try
        {
            var loader = new HooksConfigLoader(new ContainedHookFileSource(root, home));
            set = await loader.LoadAsync(new HookLocatorContext([root], home, HookPlatforms.Current), null, cancellationToken).ConfigureAwait(false);
        }
        catch (DirectoryNotFoundException)
        {
            return Unavailable("The workspace folder of the API does not exist.");
        }

        return new HooksResponse
        {
            Available = true,
            ConsentRecorded = false,
            Hooks = [.. set.Hooks.Select(hook => ToInfo(hook, root, home))],
            Sources = [.. set.Sources.Select(source => new WorkspaceHookSourceInfo
            {
                Scope = ScopeName(source.Scope),
                File = PathRedactor.Label(source.Path, root, home),
                Format = source.Format == HookConfigFormat.ClaudeSettings ? "claude-settings" : "hooks-json",
                Priority = source.Priority,
            })],
            Diagnostics = [.. set.Diagnostics.Select(diagnostic => new WorkspaceHookDiagnosticInfo
            {
                Severity = diagnostic.Severity == HookDiagnosticSeverity.Error ? "error" : "warning",
                File = Path.IsPathFullyQualified(diagnostic.File) ? PathRedactor.Label(diagnostic.File, root, home) : diagnostic.File,
                Location = diagnostic.Location,
                Message = PathRedactor.Redact(diagnostic.Message, root, home),
            })],
        };
    }

    private static HooksResponse Unavailable(string reason) => new()
    {
        Available = false,
        Reason = reason,
        ConsentRecorded = false,
        Hooks = [],
        Sources = [],
        Diagnostics = [],
    };

    private static WorkspaceHookInfo ToInfo(EffectiveHook hook, string root, string? home) => new()
    {
        Id = hook.Id,
        PluginId = hook.PluginId,
        Scope = ScopeName(hook.Scope),
        EventName = hook.EventName,
        IsOmpEvent = hook.IsOmpEvent,
        Type = hook.Type == HookType.Prompt ? "prompt" : "command",
        Command = hook.Command is null ? null : PathRedactor.Redact(hook.Command, root, home),
        Args = hook.Args is null ? null : [.. hook.Args.Select(argument => PathRedactor.Redact(argument, root, home))],
        Matcher = hook.Matcher,
        MatcherProblem = hook.MatcherProblem,
        Description = hook.Description is null ? null : PathRedactor.Redact(hook.Description, root, home),
        Enabled = hook.Enabled,
        FailClosed = hook.FailClosed,
        Blocking = hook.Blocking,
        PreConsented = hook.PreConsented,
        TimeoutSeconds = hook.Timeout.TotalSeconds,
        WorkingDirectory = PathRedactor.Label(hook.WorkingDirectory, root, home),
        SourceFile = PathRedactor.Label(hook.SourceFile, root, home),
    };

    private static string ScopeName(HookScope scope) => scope switch
    {
        HookScope.Managed => "managed",
        HookScope.Project => "project",
        HookScope.User => "user",
        HookScope.ClaudeLocal => "claude-local",
        HookScope.ClaudeProject => "claude-project",
        HookScope.ClaudeUser => "claude-user",
        HookScope.Plugin => "plugin",
        _ => "unknown",
    };
}
