using Autonogrammer.Harness.Plugins;
using Autonogrammer.Native.Views.Hooks;

namespace Autonogrammer.Native.Tests.Hooks;

/// <summary>
/// Wraps a hooks source and replaces its load step, so a test can hold the registry in the loading state or make it fail.
/// Every other member goes to the wrapped source.
/// </summary>
internal sealed class TestHookSource(IHookRegistrySource inner, Func<CancellationToken, Task> load) : IHookRegistrySource
{
    /// <inheritdoc />
    public Task LoadAsync(CancellationToken cancellationToken) => load(cancellationToken);

    /// <inheritdoc />
    public IReadOnlyList<HookRegistration> Hooks => inner.Hooks;

    /// <inheritdoc />
    public bool IsConsented(string hookId) => inner.IsConsented(hookId);

    /// <inheritdoc />
    public PluginQuarantine? FindQuarantine(string pluginId) => inner.FindQuarantine(pluginId);

    /// <inheritdoc />
    public Task SetEnabledAsync(string hookId, bool enabled, CancellationToken cancellationToken) =>
        inner.SetEnabledAsync(hookId, enabled, cancellationToken);

    /// <inheritdoc />
    public Task MoveAsync(string hookId, int index, CancellationToken cancellationToken) =>
        inner.MoveAsync(hookId, index, cancellationToken);

    /// <inheritdoc />
    public Task GrantConsentAsync(string hookId, CancellationToken cancellationToken) =>
        inner.GrantConsentAsync(hookId, cancellationToken);
}
