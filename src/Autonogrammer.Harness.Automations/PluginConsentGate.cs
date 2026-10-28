using Autonogrammer.Harness.Plugins;

namespace Autonogrammer.Harness.Automations;

/// <summary>
/// The plugin consent gate: answers consent from the plugin host's <see cref="HookRegistry"/>, which records consent per
/// hook command.
/// </summary>
public sealed class PluginConsentGate : IAutomationConsentGate
{
    private readonly HookRegistry _hooks;

    /// <summary>
    /// Creates a gate over a plugin host's hook registry.
    /// </summary>
    /// <param name="hooks">The hook registry that holds consent.</param>
    /// <exception cref="ArgumentNullException"><paramref name="hooks"/> is null.</exception>
    public PluginConsentGate(HookRegistry hooks)
    {
        ArgumentNullException.ThrowIfNull(hooks);
        _hooks = hooks;
    }

    /// <inheritdoc />
    public bool IsConsented(string hookId) => _hooks.IsConsented(hookId);
}
