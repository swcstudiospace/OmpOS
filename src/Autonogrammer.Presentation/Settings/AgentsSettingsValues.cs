namespace Autonogrammer.Presentation.Settings;

/// <summary>The saved values of the Agents settings page. The terminal auto-run switch was removed (Phase 81): the page carries no
/// editable value, so every proposed shell-terminal command waits for an approval (approval-always).</summary>
public sealed record AgentsSettingsValues()
{
    /// <summary>Gets the page defaults: every terminal command waits for an approval.</summary>
    public static AgentsSettingsValues Defaults { get; } = new();
}
