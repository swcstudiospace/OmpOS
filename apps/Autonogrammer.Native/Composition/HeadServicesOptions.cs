using Avalonia.Controls;
using Microsoft.Extensions.Logging;

namespace Autonogrammer.Native.Composition;

/// <summary>Where <see cref="HeadServices"/> keeps its state and the seams a test replaces.</summary>
internal sealed record HeadServicesOptions
{
    /// <summary>Gets the root of all session state (<c>AgentLoopEnvironment.StateRoot</c>). Hooks, checkpoints and worktrees live below it.</summary>
    public required string StateRoot { get; init; }

    /// <summary>Gets the plugins root that holds <c>autonogrammer-hooks-omp</c>, or null when there is none: hooks then run through the ACP tap only.</summary>
    public string? PluginsRoot { get; init; }

    /// <summary>Gets the absolute workspace root of the window: the repository the checkpoints and the worktrees belong to.</summary>
    public required string WorkspaceRoot { get; init; }

    /// <summary>Gets the user's home directory (the permission files, the MCP files), or null for the profile directory of the current user.</summary>
    public string? HomeDirectory { get; init; }

    /// <summary>Gets the directory of the per-user Autonogrammer files (<c>environments.json</c>, <c>mcp-state.json</c>, the workspace sets), or null for <c>Autonogrammer</c> under the application data folder.</summary>
    public string? ConfigDirectory { get; init; }

    /// <summary>Gets the reader of environment variables, or null for the process environment. It names the API address and token variables.</summary>
    public Func<string, string?>? GetVariable { get; init; }

    /// <summary>Gets the window the system browser is asked through, or null: a link then reports that it could not be opened.</summary>
    public Func<TopLevel?>? TopLevel { get; init; }

    /// <summary>Gets the clock, or null for the system clock.</summary>
    public TimeProvider? TimeProvider { get; init; }

    /// <summary>Gets the factory of the services' loggers, or null to discard their output.</summary>
    public ILoggerFactory? LoggerFactory { get; init; }

    /// <summary>Gets the handler behind the API client of the cloud agents, or null for a plain socket handler. Tests pass a scripted one.</summary>
    public HttpMessageHandler? ApiHandler { get; init; }

    /// <summary>Gets how podman is run, or null for <c>podman</c> from <c>PATH</c>. Tests pass a script that stands in for it.</summary>
    public Autonogrammer.Harness.Remote.Containers.PodmanClientOptions? Podman { get; init; }

    /// <summary>Gets how wsl.exe is run, or null for <c>wsl.exe</c> from <c>PATH</c>.</summary>
    public Autonogrammer.Harness.Remote.Containers.WslClientOptions? Wsl { get; init; }

    /// <summary>Gets the configured path of omp, or null to search the environment, <c>PATH</c> and the known install places. It decides whether local automations can run.</summary>
    public string? OmpPath { get; init; }

    /// <summary>Gets a function that says whether omp is installed, or null to look for it. Tests replace it.</summary>
    public Func<bool>? OmpAvailable { get; init; }
}
