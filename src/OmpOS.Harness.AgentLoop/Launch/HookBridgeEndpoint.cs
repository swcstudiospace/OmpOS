using OmpOS.Harness.AgentLoop.Hooks;
using OmpOS.Harness.AgentLoop.Plugins;

namespace OmpOS.Harness.AgentLoop.Launch;

/// <summary>
/// What the launch planner needs to connect omp to a session's hook bridge: the resolved extension and the environment variables the
/// extension reads (<c>OMPOS_HOOKS_URL</c>, <c>OMPOS_HOOKS_TOKEN</c> and, when a startup file exists, <c>OMPOS_HOOKS_STARTUP_FILE</c>).
/// The environment holds the bearer token, so the value is only handed to the omp child of this session and <see cref="ToString"/> never shows it.
/// </summary>
public sealed class HookBridgeEndpoint
{
    /// <summary>Initializes a new instance of the <see cref="HookBridgeEndpoint"/> class.</summary>
    /// <param name="environment">The variables of <see cref="HookBridgeServer.Environment"/>.</param>
    /// <param name="plugin">The resolved hooks bridge extension (see <see cref="HookBridgePlugin.Resolve"/>).</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentException">The environment lacks the URL or the token variable.</exception>
    public HookBridgeEndpoint(IReadOnlyDictionary<string, string> environment, ResolvedPlugin plugin)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(plugin);
        if (!environment.ContainsKey(HookBridgeEnvironment.Url) || !environment.ContainsKey(HookBridgeEnvironment.Token))
        {
            throw new ArgumentException($"The environment must hold {HookBridgeEnvironment.Url} and {HookBridgeEnvironment.Token}.", nameof(environment));
        }

        Environment = new Dictionary<string, string>(environment, StringComparer.Ordinal);
        Plugin = plugin;
    }

    /// <summary>Gets the variables to set on the omp child. They include the bearer token: never log the values.</summary>
    public IReadOnlyDictionary<string, string> Environment { get; }

    /// <summary>Gets the resolved extension. The planner loads it only when its health is ready.</summary>
    public ResolvedPlugin Plugin { get; }

    /// <summary>Gets a value indicating whether the planner can load the extension.</summary>
    public bool IsLoadable => Plugin.Health == PluginHealth.Ready;

    /// <summary>Describes the endpoint without any value: the variable names and whether the extension is loadable.</summary>
    /// <returns>The description.</returns>
    public override string ToString() =>
        $"HookBridgeEndpoint {{ Environment = [{string.Join(", ", Environment.Keys.Order(StringComparer.Ordinal))}], Loadable = {IsLoadable} }}";
}
