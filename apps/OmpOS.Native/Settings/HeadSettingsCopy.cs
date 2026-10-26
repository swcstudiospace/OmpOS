using System.Globalization;
using OmpOS.Harness.AgentLoop.Plugins;

namespace OmpOS.Native.Settings;

/// <summary>
/// The texts the head adds to the Settings window that the shared presentation copy does not hold: why a settings file was refused,
/// what each plugin does and where it was pinned. Sentences are plain and carry no key material.
/// </summary>
internal static class HeadSettingsCopy
{
    /// <summary>The environment variable that holds the completion provider address, named on the Tab page.</summary>
    public const string CompletionEndpointSource = "the environment variable OMPOS_COMPLETION_ENDPOINT";

    /// <summary>The configuration file variable of completion, named on the Tab page.</summary>
    public const string CompletionFileSource = "the file named by the environment variable OMPOS_COMPLETION_CONFIG";

    /// <summary>The sentence shown when the completion configuration cannot be read; the loader's own reason follows it.</summary>
    public const string CompletionUnreadable = "The completion configuration could not be read.";

    /// <summary>The sentence shown when the workspace files cannot be read.</summary>
    public const string WorkspaceUnreadable = "The workspace files could not be read.";

    /// <summary>Builds the reason a settings file cannot be read at all.</summary>
    /// <param name="path">The file path.</param>
    /// <returns>The sentence.</returns>
    public static string SettingsUnreadable(string path) => string.Create(
        CultureInfo.InvariantCulture,
        $"The settings file '{path}' could not be read. Check that it and its folder can be read.");

    /// <summary>Builds the reason a settings file is too large to be a settings file.</summary>
    /// <param name="path">The file path.</param>
    /// <returns>The sentence.</returns>
    public static string SettingsTooLarge(string path) => string.Create(
        CultureInfo.InvariantCulture,
        $"The settings file '{path}' is too large to be a settings file. Fix it or move it away; OmpOS does not overwrite it.");

    /// <summary>Builds the reason a settings file is not valid JSON of the settings shape.</summary>
    /// <param name="path">The file path.</param>
    /// <returns>The sentence.</returns>
    public static string SettingsInvalid(string path) => string.Create(
        CultureInfo.InvariantCulture,
        $"The settings file '{path}' is not valid. Fix it or move it away; OmpOS does not overwrite it.");

    /// <summary>Builds the reason a settings file comes from a newer major schema.</summary>
    /// <param name="path">The file path.</param>
    /// <param name="version">The schema version the file declares.</param>
    /// <returns>The sentence.</returns>
    public static string SettingsNewer(string path, int version) => string.Create(
        CultureInfo.InvariantCulture,
        $"The settings file '{path}' was written by a newer OmpOS (schema {version}). This OmpOS does not change it.");

    /// <summary>Builds the reason a settings file holds an unusable value.</summary>
    /// <param name="path">The file path.</param>
    /// <param name="member">The name of the member.</param>
    /// <returns>The sentence.</returns>
    public static string SettingsBadMember(string path, string member) => string.Create(
        CultureInfo.InvariantCulture,
        $"The settings file '{path}' holds a value OmpOS cannot use for '{member}'. Fix it or move it away; OmpOS does not overwrite it.");

    /// <summary>Builds the reason a settings file could not be written.</summary>
    /// <param name="path">The file path.</param>
    /// <returns>The sentence.</returns>
    public static string SettingsNotWritten(string path) => string.Create(
        CultureInfo.InvariantCulture,
        $"The settings file '{path}' could not be written. Check that its folder can be written. The change was not saved.");

    /// <summary>Builds the sentence that says what a plugin does.</summary>
    /// <param name="role">The plugin's role.</param>
    /// <returns>The sentence.</returns>
    public static string PluginRole(OmpPluginRole role) => role switch
    {
        OmpPluginRole.PromptPlanner => "Rewrites a prompt into a specification, a graph of thought and workflow waves.",
        OmpPluginRole.SubagentOrchestrator => "Runs subagents after the planner.",
        OmpPluginRole.SessionSubstrate => "Shares memory, leases, handoffs and the event ledger across sessions.",
        OmpPluginRole.WorkflowEngine => "Drives the phases and plans a session works through.",
        _ => "Extends omp.",
    };

    /// <summary>Builds the detail line of a plugin that was found: where it is pinned.</summary>
    /// <param name="descriptor">The plugin.</param>
    /// <returns>The sentence.</returns>
    public static string PluginFoundDetail(OmpPluginDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return descriptor.SubmoduleName is { } submodule
            ? string.Create(CultureInfo.InvariantCulture, $"Pinned submodule plugins/{submodule}.")
            : string.Create(CultureInfo.InvariantCulture, $"Tracked by OmpOS at plugins/{descriptor.RelativeDirectory}.");
    }

    /// <summary>Builds the detail line of a plugin that is missing: where it should be and how to repair it.</summary>
    /// <param name="descriptor">The plugin.</param>
    /// <returns>The sentence.</returns>
    public static string PluginMissingDetail(OmpPluginDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        var where = descriptor.SubmoduleName is { } submodule
            ? string.Create(CultureInfo.InvariantCulture, $"plugins/{submodule}")
            : string.Create(CultureInfo.InvariantCulture, $"plugins/{descriptor.RelativeDirectory}");
        return descriptor.OptionalFixCommand is { } fix
            ? string.Create(CultureInfo.InvariantCulture, $"Expected at {where}. Run: {fix}")
            : string.Create(CultureInfo.InvariantCulture, $"Expected at {where}.");
    }
}
