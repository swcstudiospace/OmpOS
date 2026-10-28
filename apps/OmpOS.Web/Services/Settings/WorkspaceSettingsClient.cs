using System.Text.Json.Serialization.Metadata;
using OmpOS.Contracts.WorkspaceSettings;

namespace OmpOS.Web.Services.Settings;

/// <summary>
/// Typed client for the read-only settings and workspace endpoints (<c>GET /workspace/conventions</c>, <c>/settings/runtime</c>, <c>/settings/plugins</c>,
/// <c>/settings/network</c>, <c>/hooks</c>, <c>/automations</c>). It never sends a body and never reads an error body: a refused call becomes an
/// <see cref="ApiClientException"/> that names the status only.
/// </summary>
internal sealed class WorkspaceSettingsClient
{
    private readonly HttpClient _http;

    /// <summary>Initializes a new instance of the <see cref="WorkspaceSettingsClient"/> class.</summary>
    /// <param name="http">An HTTP client whose base address is the API origin and that carries the caller's token.</param>
    internal WorkspaceSettingsClient(HttpClient http)
    {
        ArgumentNullException.ThrowIfNull(http);
        _http = http;
    }

    /// <summary>Reads the commands, skills, rules, subagents and configuration files of the server workspace.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The conventions.</returns>
    internal Task<ConventionsResponse> GetConventionsAsync(CancellationToken cancellationToken = default) =>
        GetAsync("workspace/conventions", WorkspaceSettingsJsonContext.Default.ConventionsResponse, cancellationToken);

    /// <summary>Reads the effective limits and options of the API.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The values.</returns>
    internal Task<RuntimeSettingsResponse> GetRuntimeAsync(CancellationToken cancellationToken = default) =>
        GetAsync("settings/runtime", WorkspaceSettingsJsonContext.Default.RuntimeSettingsResponse, cancellationToken);

    /// <summary>Reads the plugin check and inventory of the API.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The plugins.</returns>
    internal Task<PluginsSettingsResponse> GetPluginsAsync(CancellationToken cancellationToken = default) =>
        GetAsync("settings/plugins", WorkspaceSettingsJsonContext.Default.PluginsSettingsResponse, cancellationToken);

    /// <summary>Reads the upstreams the API is configured for.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The upstreams.</returns>
    internal Task<NetworkSettingsResponse> GetNetworkAsync(CancellationToken cancellationToken = default) =>
        GetAsync("settings/network", WorkspaceSettingsJsonContext.Default.NetworkSettingsResponse, cancellationToken);

    /// <summary>Reads the effective hooks of the server workspace.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The hooks.</returns>
    internal Task<HooksResponse> GetHooksAsync(CancellationToken cancellationToken = default) =>
        GetAsync("hooks", WorkspaceSettingsJsonContext.Default.HooksResponse, cancellationToken);

    /// <summary>Reads the automations of the API host.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The automations.</returns>
    internal Task<AutomationsResponse> GetAutomationsAsync(CancellationToken cancellationToken = default) =>
        GetAsync("automations", WorkspaceSettingsJsonContext.Default.AutomationsResponse, cancellationToken);

    private async Task<T> GetAsync<T>(string relativeUrl, JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken)
        where T : class
    {
        using var response = await _http.GetAsync(new Uri(relativeUrl, UriKind.Relative), cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new ApiClientException(response.StatusCode, null);
        }

        var value = await BoundedContent.ReadJsonAsync(response.Content, typeInfo, BoundedContent.MaxSuccessBytes, cancellationToken).ConfigureAwait(false);
        return value ?? throw new ApiClientException("The API returned an empty body.");
    }
}
