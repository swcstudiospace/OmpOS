using Autonogrammer.Harness.Providers;

namespace Autonogrammer.Harness.OmpConfig;

/// <summary>
/// Turns one route of a <see cref="CatalogueRow"/> into the <see cref="OmpManagedProvider"/> the models editor writes to
/// <c>models.yml</c>. The key is never part of the result: <c>apiKey</c> is the NAME of an environment variable. The base URL is the
/// route's template with the user's values filled in and must be https (http only for a local runtime on loopback). Models come from
/// discovery (<c>openai-models-list</c>, OpenAI-style routes whose vendor documents a models list), from the models the vendor
/// documents, and from the ids the user adds; a route with none of the three is not written.
/// </summary>
public static class OmpCatalogueBlock
{
    /// <summary>Builds the managed provider for a route.</summary>
    /// <param name="row">The catalogue row.</param>
    /// <param name="route">A route of <paramref name="row"/>.</param>
    /// <param name="urlValues">The user's value for each URL variable of the route.</param>
    /// <param name="apiKeyEnvName">The environment variable that holds the key, or null to use the row's own <see cref="CatalogueRow.ApiKeyEnv"/>.</param>
    /// <param name="userModelIds">Model ids the user adds, or null.</param>
    /// <returns>The provider, or the problem.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="row"/>, <paramref name="route"/> or <paramref name="urlValues"/> is null.</exception>
    public static OmpCatalogueBlockResult Build(
        CatalogueRow row,
        CatalogueRoute route,
        IReadOnlyDictionary<string, string> urlValues,
        string? apiKeyEnvName = null,
        IReadOnlyList<string>? userModelIds = null)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(urlValues);

        if (!OmpProviderApiNames.TryParse(route.Api, out var api))
        {
            return Fail($"omp does not support the protocol '{route.Api}'.");
        }

        var resolution = CatalogueUrlTemplate.Resolve(route.BaseAddressTemplate, urlValues, allowLoopbackHttp: row.Region == CatalogueRegion.Local);
        if (resolution.Url is null)
        {
            return Fail(resolution.Problem ?? "The base URL is not valid.");
        }

        var noAuthentication = route.Auth == CatalogueAuthStyle.None;
        var keyName = noAuthentication ? null : apiKeyEnvName ?? row.ApiKeyEnv;
        if (!noAuthentication && !OmpManagedProviderValidator.IsEnvName(keyName))
        {
            return Fail("This provider needs the name of the environment variable that holds its API key.");
        }

        var models = new List<OmpManagedModel>();
        foreach (var model in route.Models)
        {
            models.Add(new OmpManagedModel(model.Id, model.Name, model.Reasoning, AcceptsImages: false, model.ContextWindow, model.MaxTokens));
        }

        foreach (var id in userModelIds ?? [])
        {
            var trimmed = id.Trim();
            if (trimmed.Length > 0 && !models.Exists(model => string.Equals(model.Id, trimmed, StringComparison.Ordinal)))
            {
                models.Add(new OmpManagedModel(trimmed));
            }
        }

        var discovery = route.Discovery && api is OmpProviderApi.OpenAiCompletions or OmpProviderApi.OpenAiResponses
            ? new OmpManagedDiscovery(OmpManagedDiscovery.OpenAiModelsList)
            : null;
        if (discovery is null && models.Count == 0)
        {
            return Fail("Enter at least one model id: this route has no models list Autonogrammer can read.");
        }

        var provider = new OmpManagedProvider(
            row.BlockProviderId(route),
            resolution.Url,
            api,
            keyName,
            noAuthentication,
            models,
            BearerAuthHeader: !noAuthentication && route.Auth == CatalogueAuthStyle.Bearer && api == OmpProviderApi.AnthropicMessages,
            KeyHeaderName: !noAuthentication && route.Auth == CatalogueAuthStyle.CustomHeader ? route.AuthHeader : null,
            Discovery: discovery);
        var invalid = OmpManagedProviderValidator.Validate(provider);
        return invalid is null ? new OmpCatalogueBlockResult(provider, null) : Fail(invalid);
    }

    private static OmpCatalogueBlockResult Fail(string problem) => new(null, problem);
}
