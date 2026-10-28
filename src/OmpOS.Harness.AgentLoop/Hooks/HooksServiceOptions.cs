using Microsoft.Extensions.Logging;
using OmpOS.Harness.Plugins;
using OmpOS.Harness.Plugins.Hooks;

namespace OmpOS.Harness.AgentLoop.Hooks;

/// <summary>What <see cref="HooksService"/> needs from the head: where it keeps its state, where the extension lives, and the services behind the hooks.</summary>
public sealed record HooksServiceOptions
{
    /// <summary>Gets the compatibility version string hooks receive as <c>cursor_version</c>, for example <c>ompos-0.15.0</c>, derived from this assembly's version.</summary>
    public static string DefaultCursorVersion { get; } = "ompos-" + (typeof(HooksServiceOptions).Assembly.GetName().Version?.ToString(3) ?? "0.0.0");

    /// <summary>
    /// Gets the directory of the host's hook state: <c>hooks-consent.json</c> (consent, quarantine and enabled state, the file of
    /// <see cref="PluginStateStore"/>) and <c>hooks-host.json</c> (the <c>Run hooks</c> switch, trusted workspaces and approved versions). It
    /// belongs to the user, not to a session or a workspace.
    /// </summary>
    public required string StateDirectory { get; init; }

    /// <summary>Gets the root of all session state, the <see cref="Launch.AgentLoopSettings.StateRoot"/>. Each session's startup file and run log live under it.</summary>
    public required string SessionStateRoot { get; init; }

    /// <summary>
    /// Gets the plugins root (the <c>plugins</c> directory that holds <c>ompos-hooks-omp</c>), or null when there is none. Without it no bridge is
    /// started and the ACP tap is the only producer of hook events.
    /// </summary>
    public string? PluginsRoot { get; init; }

    /// <summary>Gets the user's home directory for the user scopes, or null to skip them. Defaults to the profile directory of the current user.</summary>
    public string? HomeDirectory { get; init; } = DefaultHome();

    /// <summary>Gets the operating system family, which decides the managed file path and the shell hooks run in. Defaults to the current one.</summary>
    public HookPlatform Platform { get; init; } = HookPlatforms.Current;

    /// <summary>Gets the compatibility version string hooks receive.</summary>
    public string CursorVersion { get; init; } = DefaultCursorVersion;

    /// <summary>Gets the reader of hook files for local workspaces, or null for <see cref="LocalHookFileSource"/>.</summary>
    public IHookFileSource? Files { get; init; }

    /// <summary>Gets the process runner behind command hooks, or null for <see cref="SystemHookCommandRunner"/>. Tests pass a fake that counts spawns.</summary>
    public IHookCommandRunner? Runner { get; init; }

    /// <summary>Gets the surface that asks the user when a hook answers <c>ask</c>, or null: such a request is then denied.</summary>
    public IHookApprovalPrompt? ApprovalPrompt { get; init; }

    /// <summary>Gets the evaluator behind <c>type: prompt</c> hooks, or null: prompt hooks then fail like a crashed hook.</summary>
    public IPromptHookEvaluator? PromptEvaluator { get; init; }

    /// <summary>Gets the provider of the hooks that plugin manifests declare (they run after every file source), or null for none.</summary>
    public Func<IReadOnlyList<HookRegistration>>? PluginHooks { get; init; }

    /// <summary>Gets the limits and the environment policy of the dispatcher, or null for the defaults.</summary>
    public HookDispatcherOptions? DispatcherOptions { get; init; }

    /// <summary>
    /// Gets the limits of the loopback bridge, or null for the defaults. Its event lists and startup file path are always set by the service
    /// for each session.
    /// </summary>
    public HookBridgeOptions? BridgeOptions { get; init; }

    /// <summary>Gets the clock behind timestamps, durations and timeouts, or null for the system clock.</summary>
    public TimeProvider? Clock { get; init; }

    /// <summary>Gets the factory of the service's loggers, or null to discard log output.</summary>
    public ILoggerFactory? LoggerFactory { get; init; }

    private static string? DefaultHome()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return string.IsNullOrWhiteSpace(home) ? null : home;
    }
}
