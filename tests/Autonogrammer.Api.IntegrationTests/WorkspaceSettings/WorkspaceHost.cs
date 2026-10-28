using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Autonogrammer.Api.Services;
using Autonogrammer.Api.Services.WorkspaceSettings;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Harness.Plugins.Hooks;
using Shouldly;

namespace Autonogrammer.Api.IntegrationTests.WorkspaceSettings;

/// <summary>Builds hosts for the workspace settings tests: a Development or token host, a workspace folder, no real home, and the spies that prove nothing is started.</summary>
internal static class WorkspaceHost
{
    /// <summary>Creates a Development host (development principal on) with the workspace folder set and no process home.</summary>
    /// <param name="workspace">The workspace folder, or null for a host with none.</param>
    /// <param name="config">Further configuration overrides.</param>
    /// <param name="more">Further service replacements, applied after the defaults.</param>
    /// <returns>The host factory.</returns>
    public static ApiFactory Dev(TempWorkspace? workspace, Dictionary<string, string?>? config = null, Action<IServiceCollection>? more = null) =>
        WebAppTestHost.Dev(config: Merge(workspace, config), more: services => Configure(services, more));

    /// <summary>Creates a host that validates bearer tokens, with the same defaults as <see cref="Dev"/>.</summary>
    /// <param name="workspace">The workspace folder, or null for a host with none.</param>
    /// <param name="config">Further configuration overrides.</param>
    /// <returns>The host factory.</returns>
    public static ApiFactory Jwt(TempWorkspace? workspace, Dictionary<string, string?>? config = null) =>
        WebAppTestHost.Jwt(config: Merge(workspace, config), more: services => Configure(services, null));

    /// <summary>Sends a GET request with a relative address.</summary>
    /// <param name="client">The client.</param>
    /// <param name="path">The relative address.</param>
    /// <param name="ct">The test cancellation token.</param>
    /// <returns>The response.</returns>
    public static Task<HttpResponseMessage> GetAsync(HttpClient client, string path, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(client);
        return client.GetAsync(new Uri(path, UriKind.Relative), ct);
    }

    /// <summary>Asserts a problem response and returns nothing: its status and the stable code in its <c>type</c>.</summary>
    /// <param name="response">The response.</param>
    /// <param name="status">The expected status.</param>
    /// <param name="code">The expected code.</param>
    /// <param name="ct">The test cancellation token.</param>
    /// <returns>A task that completes after the assertions.</returns>
    public static Task ShouldBeProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code, CancellationToken ct) =>
        WebAppTestHost.ShouldBeProblemAsync(response, status, code, ct);

    private static Dictionary<string, string?> Merge(TempWorkspace? workspace, Dictionary<string, string?>? config)
    {
        var merged = new Dictionary<string, string?>(StringComparer.Ordinal);
        if (workspace is not null)
        {
            merged["Api:Workspace:Root"] = workspace.Root;
        }

        if (config is not null)
        {
            foreach (var (key, value) in config)
            {
                merged[key] = value;
            }
        }

        return merged;
    }

    private static void Configure(IServiceCollection services, Action<IServiceCollection>? more)
    {
        services.AddSingleton<IUserHomeProvider>(new FixedUserHome(null));
        more?.Invoke(services);
    }

    /// <summary>Registers the spies that count hook processes and engine sessions, and returns them.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The hook runner spy and the engine session spy.</returns>
    public static (HookRunnerSpy Hooks, HarnessSessionSpy Engines) AddSpies(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        var hooks = new HookRunnerSpy();
        var engines = new HarnessSessionSpy();
        services.AddSingleton<IHookCommandRunner>(hooks);
        services.AddSingleton<IHarnessSessionFactory>(engines);
        _ = services.AddLiveHarnessTracking();
        return (hooks, engines);
    }

    /// <summary>Asserts that a response body names no absolute path of the workspace folder.</summary>
    /// <param name="body">The response body.</param>
    /// <param name="workspace">The workspace folder.</param>
    public static void ShouldNotLeakPaths(string body, TempWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        body.ShouldNotContain(workspace.Root, Case.Sensitive);
        body.ShouldNotContain(Path.GetFileName(workspace.Root), Case.Sensitive);
    }
}
