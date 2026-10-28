using System.Globalization;
using Autonogrammer.Harness.AgentLoop.Hooks;
using Autonogrammer.Harness.Plugins.Hooks;

namespace Autonogrammer.Harness.AgentLoop.Tests.Hooks;

/// <summary>A hooks service over temporary directories, an in-memory hook file and a runner that starts nothing.</summary>
internal sealed class HooksServiceRig : IAsyncDisposable
{
    /// <summary>The hook file most tests use: a guarding shell hook, a stop hook and an audit hook.</summary>
    internal const string DefaultHooksJson =
        "{\"version\":1,\"hooks\":{"
        + "\"beforeShellExecution\":[{\"command\":\"./guard.sh\",\"matcher\":\"git push\",\"failClosed\":true,\"id\":\"guard\"}],"
        + "\"afterAgentResponse\":[{\"command\":\"./audit.sh\",\"id\":\"audit\"}]"
        + "}}";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "autonogrammer-hooks-tests", Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));

    private HooksServiceRig(bool withPlugin)
    {
        WithPlugin = withPlugin;
    }

    /// <summary>Gets the workspace root.</summary>
    public string Project => Path.Combine(_root, "project");

    /// <summary>Gets the directory of the host's hook state.</summary>
    public string StateDirectory => Path.Combine(_root, "hooks-state");

    /// <summary>Gets the root of the session state.</summary>
    public string SessionStateRoot => Path.Combine(_root, "sessions");

    /// <summary>Gets the plugins root.</summary>
    public string PluginsRoot => Path.Combine(_root, "plugins");

    /// <summary>Gets the path of the project hook file.</summary>
    public string HooksFile => Path.Combine(Project, ".cursor", "hooks.json");

    /// <summary>Gets a value indicating whether the plugins root holds the hooks bridge extension.</summary>
    public bool WithPlugin { get; }

    /// <summary>Gets the in-memory hook files.</summary>
    public MemoryHookFiles Files { get; } = new();

    /// <summary>Gets the runner that starts nothing.</summary>
    public ScriptedHookRunner Runner { get; } = new();

    /// <summary>Gets the service under test.</summary>
    public HooksService Service { get; private set; } = null!;

    /// <summary>Creates the rig and opens the service.</summary>
    /// <param name="hooksJson">The content of the project hook file.</param>
    /// <param name="withPlugin">True to lay out the hooks bridge extension under the plugins root.</param>
    /// <param name="cancellationToken">Cancels the open.</param>
    /// <returns>The rig.</returns>
    public static async Task<HooksServiceRig> CreateAsync(string hooksJson, bool withPlugin, CancellationToken cancellationToken)
    {
        var rig = new HooksServiceRig(withPlugin);
        rig.Files.Set(rig.HooksFile, hooksJson);
        if (withPlugin)
        {
            var directory = Path.Combine(rig.PluginsRoot, "autonogrammer-hooks-omp", "src");
            Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(Path.Combine(rig.PluginsRoot, "autonogrammer-hooks-omp", "package.json"), "{\"name\":\"autonogrammer-hooks-omp\",\"omp\":{\"extensions\":[\"./src/index.mjs\"]}}", cancellationToken).ConfigureAwait(false);
            await File.WriteAllTextAsync(Path.Combine(directory, "index.mjs"), "export default function () {}\n", cancellationToken).ConfigureAwait(false);
        }

        await rig.ReopenAsync(cancellationToken).ConfigureAwait(false);
        return rig;
    }

    /// <summary>Disposes the service and opens a new one over the same state directory, as a restart of the application does.</summary>
    /// <param name="cancellationToken">Cancels the open.</param>
    /// <returns>A task that completes when the new service is open.</returns>
    public async Task ReopenAsync(CancellationToken cancellationToken)
    {
        if (Service is not null)
        {
            await Service.DisposeAsync().ConfigureAwait(false);
        }

        Service = await HooksService.OpenAsync(
            new HooksServiceOptions
            {
                StateDirectory = StateDirectory,
                SessionStateRoot = SessionStateRoot,
                PluginsRoot = WithPlugin ? PluginsRoot : null,
                HomeDirectory = null,
                Platform = HookPlatform.Linux,
                Files = Files,
                Runner = Runner,
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Builds a session request for the project.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="trustProjectHooks">Whether project hooks are trusted regardless of the trust list.</param>
    /// <returns>The request.</returns>
    public HooksSessionRequest Request(string sessionId = "s1", bool? trustProjectHooks = true) => new()
    {
        SessionId = sessionId,
        WorkspaceRoots = [Project],
        TrustProjectHooks = trustProjectHooks,
    };

    /// <summary>Reads the hooks of the project as the Hooks page does.</summary>
    /// <param name="cancellationToken">Cancels the load.</param>
    /// <param name="trustProjectHooks">Whether project hooks are trusted regardless of the trust list; null uses the list.</param>
    /// <returns>The overview.</returns>
    public Task<HooksOverview> OverviewAsync(CancellationToken cancellationToken, bool? trustProjectHooks = true) =>
        Service.LoadOverviewAsync(new HooksWorkspace { Roots = [Project], TrustProjectHooks = trustProjectHooks }, cancellationToken);

    /// <summary>Approves every hook of the project.</summary>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when every hook is approved.</returns>
    public async Task ApproveAllAsync(CancellationToken cancellationToken)
    {
        var overview = await OverviewAsync(cancellationToken).ConfigureAwait(false);
        await Service.ApproveAllAsync([.. overview.Hooks.Select(static h => h.Hook)], cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await Service.DisposeAsync().ConfigureAwait(false);
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A temporary directory that cannot be removed does not fail the test.
        }
    }
}
