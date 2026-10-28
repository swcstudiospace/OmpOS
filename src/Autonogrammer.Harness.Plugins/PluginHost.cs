namespace Autonogrammer.Harness.Plugins;

/// <summary>
/// Loads plugin manifests. For each manifest the host validates it, checks its API version, checks for quarantine,
/// activates it, and registers its hooks. A rejected or failing plugin is reported and the rest of the load continues.
/// </summary>
public sealed class PluginHost : IDisposable
{
    private readonly PluginStateStore _state;
    private readonly IPluginActivator _activator;
    private readonly PluginApiVersionRange _supportedApiVersions;
    private readonly SemaphoreSlim _loadGate = new(1, 1);
    private readonly HashSet<string> _pluginIds = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a host.
    /// </summary>
    /// <param name="state">The persisted quarantine and consent state.</param>
    /// <param name="activator">Activates each validated plugin.</param>
    /// <param name="supportedApiVersions">The API versions this host accepts.</param>
    public PluginHost(PluginStateStore state, IPluginActivator activator, PluginApiVersionRange supportedApiVersions)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(activator);
        ArgumentNullException.ThrowIfNull(supportedApiVersions);
        _state = state;
        _activator = activator;
        _supportedApiVersions = supportedApiVersions;
        Hooks = new HookRegistry(state);
    }

    /// <summary>
    /// The hooks of every activated plugin.
    /// </summary>
    public HookRegistry Hooks { get; }

    /// <summary>
    /// Loads manifests in the order given. Loads do not run concurrently.
    /// </summary>
    /// <param name="sources">The manifests to load.</param>
    /// <param name="cancellationToken">Cancels the load.</param>
    /// <returns>What was loaded, rejected and quarantined.</returns>
    public async Task<PluginLoadReport> LoadAsync(IReadOnlyList<PluginManifestSource> sources, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sources);
        await _loadGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var loaded = new List<LoadedPlugin>();
            var rejected = new List<RejectedPlugin>();
            var quarantined = new List<QuarantinedPlugin>();

            foreach (var source in sources)
            {
                var manifest = Validate(source, rejected);
                if (manifest is null)
                {
                    continue;
                }

                if (!_pluginIds.Add(manifest.Id))
                {
                    rejected.Add(new RejectedPlugin(
                        source.Location,
                        manifest.Id,
                        PluginRejectionKind.DuplicatePluginId,
                        $"Plugin '{manifest.Id}' is already loaded from another manifest."));
                    continue;
                }

                var blocked = await ActivateOrQuarantineAsync(manifest, cancellationToken).ConfigureAwait(false);
                if (blocked is not null)
                {
                    quarantined.Add(blocked);
                    continue;
                }

                await Hooks.RegisterAsync(manifest.Id, manifest.Hooks, cancellationToken).ConfigureAwait(false);
                loaded.Add(new LoadedPlugin(manifest.Id, manifest.Version, manifest.Hooks.Count));
            }

            return new PluginLoadReport(loaded, rejected, quarantined);
        }
        finally
        {
            _loadGate.Release();
        }
    }

    /// <summary>
    /// Lifts a plugin's quarantine and persists the operator's reason. Hooks that were registered before the quarantine
    /// run again once the lift is recorded. A plugin quarantined at load has no registered hooks; it is activated by a
    /// host that loads it after the lift.
    /// </summary>
    /// <param name="pluginId">The quarantined plugin.</param>
    /// <param name="reason">Why the operator lifted the quarantine. It must not be empty or whitespace.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>True when a quarantine was lifted; false when the plugin was not quarantined.</returns>
    /// <exception cref="ArgumentException">The plugin identifier or the reason is empty or whitespace.</exception>
    public async Task<bool> LiftQuarantineAsync(string pluginId, string reason, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        await _loadGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await _state.LiftQuarantineAsync(pluginId, reason, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _loadGate.Release();
        }
    }

    /// <summary>
    /// Releases the hook registry and the load lock.
    /// </summary>
    public void Dispose()
    {
        Hooks.Dispose();
        _loadGate.Dispose();
    }

    private PluginManifest? Validate(PluginManifestSource source, List<RejectedPlugin> rejected)
    {
        PluginManifest manifest;
        try
        {
            manifest = PluginManifestParser.Parse(source.Content);
        }
        catch (PluginManifestException ex)
        {
            rejected.Add(new RejectedPlugin(source.Location, ex.PluginId, ex.Kind, ex.Message));
            return null;
        }

        if (!_supportedApiVersions.Contains(manifest.ApiVersion))
        {
            rejected.Add(new RejectedPlugin(
                source.Location,
                manifest.Id,
                PluginRejectionKind.UnsupportedApiVersion,
                _supportedApiVersions.DescribeRejection(manifest)));
            return null;
        }

        return manifest;
    }

    // Returns the quarantine that applies to the plugin, or null when it was activated.
    private async Task<QuarantinedPlugin?> ActivateOrQuarantineAsync(PluginManifest manifest, CancellationToken cancellationToken)
    {
        var existing = _state.FindQuarantine(manifest.Id);
        if (existing is not null)
        {
            return new QuarantinedPlugin(manifest.Id, existing.Reason);
        }

        // Task.Run turns a synchronous throw from the activator into a faulted task, so every activation failure takes one path.
        var activation = Task.Run(() => _activator.ActivateAsync(manifest, cancellationToken), cancellationToken);
        var fault = await TaskFault.AwaitAsync(activation).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (fault is null)
        {
            return null;
        }

        // The activator threw. Quarantine the plugin with the reason and keep loading the rest.
        var reason = FaultReason.Describe($"Activation of plugin '{manifest.Id}'", fault);
        await _state.QuarantineAsync(manifest.Id, reason, cancellationToken).ConfigureAwait(false);
        return new QuarantinedPlugin(manifest.Id, reason);
    }
}
