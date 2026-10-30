using Autonogrammer.Presentation.Mcp;

namespace Autonogrammer.Presentation.Settings;

/// <summary>
/// The MCP section of the settings window: a page that holds the <see cref="McpSettingsViewModel"/> its view draws. The view loads the model
/// itself when it gets it, and the model owns its draft-free actions (enable, consent, check, add from template), so this page is never dirty
/// and never loads again: it stays Ready so no loading or failure state hides the model's own states.
/// </summary>
public sealed class McpSettingsPage : SettingsPageBase
{
    /// <summary>Initializes a new instance of the <see cref="McpSettingsPage"/> class.</summary>
    /// <param name="model">The model the view draws.</param>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> is null.</exception>
    public McpSettingsPage(McpSettingsViewModel model)
        : base(SettingsSection.Mcp, McpCopy.Title, SettingsPageState.Ready)
    {
        ArgumentNullException.ThrowIfNull(model);
        Model = model;
    }

    /// <summary>Gets the model the view draws.</summary>
    public McpSettingsViewModel Model { get; }

    /// <inheritdoc/>
    public override Task LoadAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
