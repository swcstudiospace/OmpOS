using Autonogrammer.Contracts.Agents;

namespace Autonogrammer.Harness.AgentLoop.Plugins;

/// <summary>
/// Describes one omp plugin the agent loop composes: where it lives under the plugins root and which files omp loads.
/// </summary>
public sealed record OmpPluginDescriptor
{
    /// <summary>Gets the stable identifier: <c>ultrathink</c>, <c>swarm</c>, <c>substrate</c> or <c>gsd</c>.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the name shown to the user.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Gets what the plugin does for the agent loop.</summary>
    public required OmpPluginRole Role { get; init; }

    /// <summary>Gets how omp loads the plugin.</summary>
    public OmpPluginLoading Loading { get; init; } = OmpPluginLoading.Extension;

    /// <summary>Gets the directory of the plugin under the plugins root, with <c>/</c> separators. It may be nested inside a submodule.</summary>
    public required string RelativeDirectory { get; init; }

    /// <summary>
    /// Gets the files, relative to <see cref="RelativeDirectory"/>, that omp loads one by one. When empty, the plugin is a
    /// package: omp loads the directory and the extensions its <c>package.json</c> declares are checked instead.
    /// </summary>
    public IReadOnlyList<string> EntryRelativePaths { get; init; } = [];

    /// <summary>Gets a value indicating whether the agent loop needs the plugin to do its job. Its absence is reported with that wording.</summary>
    public bool Required { get; init; }

    /// <summary>Gets the name of the git submodule under the plugins root that holds the plugin, or null when Autonogrammer tracks the plugin itself.</summary>
    public string? SubmoduleName { get; init; }

    /// <summary>Gets the command that repairs a missing plugin, or null when there is none.</summary>
    public string? OptionalFixCommand { get; init; }

    /// <summary>
    /// Gets the tool names omp lists once the plugin is loaded. The connection report is checked against them. Only names
    /// found in the plugin's own source are listed.
    /// </summary>
    public IReadOnlyList<string> ExpectedTools { get; init; } = [];

    /// <summary>
    /// Gets the slash-command names (without the leading <c>/</c>) omp lists once the plugin is loaded. The connection report
    /// is checked against them when omp could list its commands. Only names found in the plugin's own source are listed.
    /// </summary>
    public IReadOnlyList<string> ExpectedCommands { get; init; } = [];

    /// <summary>Gets the stage source that reports on this plugin.</summary>
    public StageSource StageSource => Role switch
    {
        OmpPluginRole.PromptPlanner => StageSource.Ultrathink,
        OmpPluginRole.SubagentOrchestrator => StageSource.Swarm,
        OmpPluginRole.SessionSubstrate => StageSource.Substrate,
        OmpPluginRole.WorkflowEngine => StageSource.Gsd,
        _ => StageSource.Omp,
    };
}
