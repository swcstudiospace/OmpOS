namespace Autonogrammer.Presentation.Mcp;

/// <summary>
/// The MCP servers an <see cref="McpSettingsViewModel"/> lists and changes. The presentation layer may not reference the harness (L2 to L2), so
/// a head adapts the harness catalog to this interface; expected failures are reported as <see cref="McpSourceException"/>.
/// </summary>
public interface IMcpSettingsSource
{
    /// <summary>Reads the configuration files and the user's choices again.</summary>
    /// <param name="cancellationToken">Cancels the load.</param>
    /// <returns>A task that completes when the source is ready, and faults with <see cref="McpSourceException"/> when loading fails.</returns>
    Task LoadAsync(CancellationToken cancellationToken);

    /// <summary>Reads the current servers, files and notices.</summary>
    /// <returns>A snapshot; later changes do not alter it.</returns>
    McpSettingsSnapshot Snapshot();

    /// <summary>Switches a server on or off. Neither change starts the server.</summary>
    /// <param name="name">The server name.</param>
    /// <param name="enabled">True to switch the server on.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the choice is saved.</returns>
    Task SetEnabledAsync(string name, bool enabled, CancellationToken cancellationToken);

    /// <summary>Approves the server's exact declaration. Nothing is started.</summary>
    /// <param name="name">The server name.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the approval is saved.</returns>
    Task GrantConsentAsync(string name, CancellationToken cancellationToken);

    /// <summary>Withdraws the approval of the server's exact declaration.</summary>
    /// <param name="name">The server name.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the change is saved.</returns>
    Task RevokeConsentAsync(string name, CancellationToken cancellationToken);

    /// <summary>Connects to the server once and lists its tools. It starts a process or opens a connection, so it runs only when the user asks.</summary>
    /// <param name="name">The server name.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>A task that completes when the check ended; its result is in the next snapshot.</returns>
    Task CheckAsync(string name, CancellationToken cancellationToken);

    /// <summary>Adds a placeholder server to the Autonogrammer-owned file; it stays switched off until the user has edited and enabled it.</summary>
    /// <param name="choice">The skeleton.</param>
    /// <param name="name">The new server's name.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The origin of the file that was written, to show.</returns>
    Task<string> AddTemplateAsync(McpTemplateChoice choice, string name, CancellationToken cancellationToken);

    /// <summary>Gets the ready-made servers the page offers to add. The list does not change while the source lives; a source with none returns an empty list.</summary>
    IReadOnlyList<McpPresetChoice> Presets { get; }

    /// <summary>Adds a preset to the Autonogrammer-owned file under its own name; it stays switched off until the user switches it on. The entry holds no credential and nothing is started.</summary>
    /// <param name="presetId">The <see cref="McpPresetChoice.Id"/> of a preset in <see cref="Presets"/>.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The origin of the file that was written, to show. Faults with <see cref="McpSourceException"/> when no workspace is open, the preset is unknown or its name is already configured.</returns>
    Task<string> AddPresetAsync(string presetId, CancellationToken cancellationToken);

    /// <summary>Asks the host to open a configuration file in the user's editor. The page never reads or edits the file.</summary>
    /// <param name="origin">The origin of the file, as the snapshot shows it.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A task that completes when the host was asked.</returns>
    Task OpenConfigFileAsync(string origin, CancellationToken cancellationToken);
}
