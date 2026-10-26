using OmpOS.Harness.Plugins;
using OmpOS.Native.Views.Hooks;

namespace OmpOS.Native.Tests.Hooks;

/// <summary>
/// A real plugin host over a temporary state file, with its hooks loaded. Its process runner is a counting fake, so a test
/// can tell whether any hook process was started. The state file and its directory are removed on disposal.
/// </summary>
internal sealed class HookWorkbench : IDisposable
{
    private readonly string _root;

    private HookWorkbench(string root, PluginStateStore state, PluginHost host, CountingHookProcessRunner processRunner)
    {
        _root = root;
        State = state;
        Host = host;
        ProcessRunner = processRunner;
        Source = new HookRegistrySource(host.Hooks, state, _ => Task.CompletedTask);
    }

    /// <summary>Gets the persisted consent and quarantine state.</summary>
    public PluginStateStore State { get; }

    /// <summary>Gets the host that loaded the plugins.</summary>
    public PluginHost Host { get; }

    /// <summary>Gets the hook registry of the host.</summary>
    public HookRegistry Registry => Host.Hooks;

    /// <summary>Gets the fake that counts every hook process it is asked to start.</summary>
    public CountingHookProcessRunner ProcessRunner { get; }

    /// <summary>Gets the hooks source the view reads. The plugins are already loaded, so loading it does nothing more.</summary>
    public IHookRegistrySource Source { get; }

    /// <summary>Opens a temporary state file and loads the manifests into a new host.</summary>
    /// <param name="cancellationToken">Cancels the load.</param>
    /// <param name="activator">Activates each plugin. The declarative activator when null.</param>
    /// <param name="manifests">The manifests to load, in order.</param>
    /// <returns>The opened workbench.</returns>
    public static async Task<HookWorkbench> OpenAsync(
        CancellationToken cancellationToken,
        IPluginActivator? activator = null,
        params PluginManifestSource[] manifests)
    {
        var root = Path.Combine(Path.GetTempPath(), "ompos-hooks-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var state = await PluginStateStore.OpenAsync(Path.Combine(root, "plugin-state.json"), null, cancellationToken)
            .ConfigureAwait(true);
        var host = new PluginHost(state, activator ?? new DeclarativePluginActivator(), PluginApiVersionRange.Current);
        await host.LoadAsync(manifests, cancellationToken).ConfigureAwait(true);
        return new HookWorkbench(root, state, host, new CountingHookProcessRunner());
    }

    /// <summary>Runs the hooks for a trigger through the production runner, using the counting fake.</summary>
    /// <param name="trigger">The lifecycle event.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>The run result.</returns>
    public Task<HookRunResult> RunAsync(HookTrigger trigger, CancellationToken cancellationToken) =>
        new HookRunner(Registry, State, ProcessRunner).RunAsync(trigger, cancellationToken);

    /// <summary>Releases the host, the state store and the temporary directory.</summary>
    public void Dispose()
    {
        Host.Dispose();
        State.Dispose();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
