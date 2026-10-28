using OmpOS.Contracts.WorkspaceSettings;

namespace OmpOS.UI.Components.Settings;

/// <summary>
/// Reads the read-only workspace settings an API serves: the runtime values, the hook files and the convention files (of which the MCP files
/// are part). The web head implements it over the API; a test implements it with fixed answers.
/// </summary>
public interface IWorkspaceSettingsReader
{
    /// <summary>Reads the runtime values of the service.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The values.</returns>
    /// <exception cref="OmpOS.Presentation.Settings.SettingsSourceException">The values cannot be read; the message says why.</exception>
    Task<RuntimeSettingsResponse> GetRuntimeAsync(CancellationToken cancellationToken);

    /// <summary>Reads the hooks the workspace defines.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The hooks, their source files and the problems found.</returns>
    /// <exception cref="OmpOS.Presentation.Settings.SettingsSourceException">The hooks cannot be read; the message says why.</exception>
    Task<HooksResponse> GetHooksAsync(CancellationToken cancellationToken);

    /// <summary>Reads the conventions of the workspace (commands, skills, rules, subagents and the hook and MCP files).</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The conventions.</returns>
    /// <exception cref="OmpOS.Presentation.Settings.SettingsSourceException">The conventions cannot be read; the message says why.</exception>
    Task<ConventionsResponse> GetConventionsAsync(CancellationToken cancellationToken);
}
