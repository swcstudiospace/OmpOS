using Autonogrammer.Harness.Plugins;

namespace Autonogrammer.Native.Views.Hooks;

/// <summary>
/// Adapts the production <see cref="HookRegistry"/> and its <see cref="PluginStateStore"/> to the view's
/// <see cref="IHookRegistrySource"/>. Loading is delegated to <paramref name="load"/>, which the shell supplies as the host's
/// load of the plugin manifests.
/// </summary>
internal sealed class HookRegistrySource(
    HookRegistry registry,
    PluginStateStore state,
    Func<CancellationToken, Task> load) : IHookRegistrySource
{
    private readonly HookRegistry _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    private readonly PluginStateStore _state = state ?? throw new ArgumentNullException(nameof(state));
    private readonly Func<CancellationToken, Task> _load = load ?? throw new ArgumentNullException(nameof(load));

    /// <inheritdoc />
    public Task LoadAsync(CancellationToken cancellationToken) => _load(cancellationToken);

    /// <inheritdoc />
    public IReadOnlyList<HookRegistration> Hooks => _registry.List();

    /// <inheritdoc />
    public bool IsConsented(string hookId) => _registry.IsConsented(hookId);

    /// <inheritdoc />
    public PluginQuarantine? FindQuarantine(string pluginId) => _state.FindQuarantine(pluginId);

    /// <inheritdoc />
    public Task SetEnabledAsync(string hookId, bool enabled, CancellationToken cancellationToken) =>
        _registry.SetEnabledAsync(hookId, enabled, cancellationToken);

    /// <inheritdoc />
    public Task MoveAsync(string hookId, int index, CancellationToken cancellationToken) =>
        _registry.MoveAsync(hookId, index, cancellationToken);

    /// <inheritdoc />
    public Task GrantConsentAsync(string hookId, CancellationToken cancellationToken) =>
        _registry.GrantConsentAsync(hookId, cancellationToken);
}
