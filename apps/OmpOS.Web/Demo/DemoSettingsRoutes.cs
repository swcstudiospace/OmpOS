using System.Net;
using OmpOS.Contracts.Providers;
using OmpOS.Contracts.WorkspaceSettings;

namespace OmpOS.Web.Demo;

/// <summary>
/// The Settings routes of the Demo handler: the provider routes (<c>GET /providers</c> with its filters, <c>GET /providers/{id}</c>, the refused key
/// write, the skipped live test, custom endpoints and the default model) and the read-only workspace settings routes (conventions, runtime,
/// plugins, network, hooks, automations). They answer in the shapes and with the problems of the real routes.
/// </summary>
internal static class DemoSettingsRoutes
{
    /// <summary>Answers a request when it is one of the Settings routes.</summary>
    /// <param name="request">The request.</param>
    /// <param name="path">The unescaped path segments.</param>
    /// <param name="providers">The provider state of the Demo.</param>
    /// <param name="cancellationToken">Cancels the read of a body.</param>
    /// <returns>The response, or null when the request is not a Settings route.</returns>
    internal static async Task<HttpResponseMessage?> TryRouteAsync(HttpRequestMessage request, string[] path, DemoProviders providers, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(providers);
        var method = request.Method;
        if (method == HttpMethod.Get)
        {
            return RouteGet(request, path, providers);
        }

        if (path.Length == 0)
        {
            return null;
        }

        var provider = string.Equals(path[0], "providers", StringComparison.Ordinal);
        if (method == HttpMethod.Put && DemoApiHandler.Match(path, "providers", null, "credential"))
        {
            return DemoApiHandler.JsonResponse(request, DemoProviders.RefuseCredential(), ProvidersJsonContext.Default.CredentialResponse);
        }

        if (method == HttpMethod.Put && DemoApiHandler.Match(path, "models", "default"))
        {
            var body = await DemoApiHandler.ReadBodyAsync(request, ProvidersJsonContext.Default.SetDefaultModelRequest, cancellationToken).ConfigureAwait(false);
            return DemoApiHandler.JsonResponse(request, providers.SetDefault(body), ProvidersJsonContext.Default.DefaultModelResponse);
        }

        if (method == HttpMethod.Post && DemoApiHandler.Match(path, "providers", "custom"))
        {
            var body = await DemoApiHandler.ReadBodyAsync(request, ProvidersJsonContext.Default.CustomProviderRequest, cancellationToken).ConfigureAwait(false);
            return DemoApiHandler.JsonResponse(request, providers.AddCustom(body), ProvidersJsonContext.Default.CustomProviderInfo);
        }

        if (method == HttpMethod.Post && provider && DemoApiHandler.Match(path, "providers", null, "test"))
        {
            return DemoApiHandler.JsonResponse(request, providers.Test(path[1]), ProvidersJsonContext.Default.ProviderTestResponse);
        }

        if (method == HttpMethod.Delete && DemoApiHandler.Match(path, "providers", "custom", null))
        {
            providers.RemoveCustom(path[2]);
            return new HttpResponseMessage(HttpStatusCode.NoContent) { Content = new ByteArrayContent([]), RequestMessage = request };
        }

        return null;
    }

    private static HttpResponseMessage? RouteGet(HttpRequestMessage request, string[] path, DemoProviders providers)
    {
        if (DemoApiHandler.Match(path, "providers"))
        {
            var query = Query(request.RequestUri!);
            return DemoApiHandler.JsonResponse(
                request,
                providers.List(query.GetValueOrDefault("region"), query.GetValueOrDefault("kind"), query.GetValueOrDefault("search")),
                ProvidersJsonContext.Default.ProvidersResponse);
        }

        if (DemoApiHandler.Match(path, "providers", null))
        {
            return DemoApiHandler.JsonResponse(request, DemoProviders.Get(path[1]), ProvidersJsonContext.Default.ProviderSummary);
        }

        if (DemoApiHandler.Match(path, "workspace", "conventions"))
        {
            return DemoApiHandler.JsonResponse(request, DemoWorkspaceSettings.Conventions, WorkspaceSettingsJsonContext.Default.ConventionsResponse);
        }

        if (DemoApiHandler.Match(path, "settings", "runtime"))
        {
            return DemoApiHandler.JsonResponse(request, DemoWorkspaceSettings.Runtime, WorkspaceSettingsJsonContext.Default.RuntimeSettingsResponse);
        }

        if (DemoApiHandler.Match(path, "settings", "plugins"))
        {
            return DemoApiHandler.JsonResponse(request, DemoWorkspaceSettings.Plugins, WorkspaceSettingsJsonContext.Default.PluginsSettingsResponse);
        }

        if (DemoApiHandler.Match(path, "settings", "network"))
        {
            return DemoApiHandler.JsonResponse(request, DemoWorkspaceSettings.Network, WorkspaceSettingsJsonContext.Default.NetworkSettingsResponse);
        }

        if (DemoApiHandler.Match(path, "hooks"))
        {
            return DemoApiHandler.JsonResponse(request, DemoWorkspaceSettings.Hooks, WorkspaceSettingsJsonContext.Default.HooksResponse);
        }

        return DemoApiHandler.Match(path, "automations")
            ? DemoApiHandler.JsonResponse(request, DemoWorkspaceSettings.Automations, WorkspaceSettingsJsonContext.Default.AutomationsResponse)
            : null;
    }

    private static Dictionary<string, string> Query(Uri uri)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var equals = pair.IndexOf('=', StringComparison.Ordinal);
            var name = Uri.UnescapeDataString((equals < 0 ? pair : pair[..equals]).Replace('+', ' '));
            var value = equals < 0 ? string.Empty : Uri.UnescapeDataString(pair[(equals + 1)..].Replace('+', ' '));
            values[name] = value;
        }

        return values;
    }
}
