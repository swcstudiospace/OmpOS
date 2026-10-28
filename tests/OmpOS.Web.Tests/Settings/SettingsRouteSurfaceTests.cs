using System.Net.Http.Json;
using System.Text.Json;
using OmpOS.Presentation.Settings.Models;
using OmpOS.Web.Services.Settings;
using Shouldly;
using Xunit;
using Wire = OmpOS.Contracts.Providers;

namespace OmpOS.Web.Tests.Settings;

/// <summary>
/// The routes the Settings clients call: the Demo answers every one, and every one is documented in <c>docs/api/openapi.json</c>, so the Demo and
/// the API stay contract-identical for the Settings pages as they do for the sessions.
/// </summary>
public sealed class SettingsRouteSurfaceTests : IDisposable
{
    private static readonly string[] ExpectedRoutes =
    [
        "GET /workspace/conventions",
        "GET /settings/runtime",
        "GET /settings/plugins",
        "GET /settings/network",
        "GET /hooks",
        "GET /automations",
        "GET /providers",
        "GET /providers/{}",
        "PUT /providers/{}/credential",
        "POST /providers/{}/test",
        "PUT /models/default",
        "POST /providers/custom",
        "DELETE /providers/custom/{}",
    ];

    private readonly DemoTestHost _host = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>The workspace client and the Models source reach exactly the expected routes, and the Demo answers each.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task EverySettingsRouteIsAnsweredByTheDemo()
    {
        var routes = await ExerciseAsync().ConfigureAwait(true);

        routes.OrderBy(static route => route, StringComparer.Ordinal).ShouldBe(ExpectedRoutes.OrderBy(static route => route, StringComparer.Ordinal));
    }

    /// <summary>Every Settings route the clients use is a documented operation of the API.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task EverySettingsRouteIsInTheOpenApiDocument()
    {
        var documented = await ReadDocumentedOperationsAsync().ConfigureAwait(true);

        var missing = (await ExerciseAsync().ConfigureAwait(true)).Where(route => !documented.Contains(route)).ToList();

        missing.ShouldBeEmpty("the Settings clients call operations that docs/api/openapi.json does not document: " + string.Join(", ", missing));
    }

    /// <inheritdoc />
    public void Dispose() => _host.Dispose();

    private static async Task<HashSet<string>> ReadDocumentedOperationsAsync()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "global.json")))
        {
            root = root.Parent;
        }

        root.ShouldNotBeNull("Could not locate the repository root from the test output directory.");
        var text = await File.ReadAllTextAsync(Path.Combine(root.FullName, "docs", "api", "openapi.json"), Token).ConfigureAwait(true);
        using var document = JsonDocument.Parse(text);
        var operations = new HashSet<string>(StringComparer.Ordinal);
        foreach (var path in document.RootElement.GetProperty("paths").EnumerateObject())
        {
            var template = "/" + string.Join('/', path.Name.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(static segment => segment.StartsWith('{') ? "{}" : segment));
            foreach (var operation in path.Value.EnumerateObject())
            {
                _ = operations.Add(operation.Name.ToUpperInvariant() + " " + template);
            }
        }

        return operations;
    }

    private static string Normalise(string request)
    {
        var space = request.IndexOf(' ', StringComparison.Ordinal);
        var segments = request[(space + 1)..].Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length >= 2 && string.Equals(segments[0], "providers", StringComparison.Ordinal))
        {
            if (string.Equals(segments[1], "custom", StringComparison.Ordinal))
            {
                if (segments.Length == 3)
                {
                    segments[2] = "{}";
                }
            }
            else
            {
                segments[1] = "{}";
            }
        }

        return request[..space] + " /" + string.Join('/', segments);
    }

    private async Task<IReadOnlyList<string>> ExerciseAsync()
    {
        var client = WorkspaceFixtures.Client(_host.Http);
        _ = await client.GetConventionsAsync(Token).ConfigureAwait(true);
        _ = await client.GetRuntimeAsync(Token).ConfigureAwait(true);
        _ = await client.GetPluginsAsync(Token).ConfigureAwait(true);
        _ = await client.GetNetworkAsync(Token).ConfigureAwait(true);
        _ = await client.GetHooksAsync(Token).ConfigureAwait(true);
        _ = await client.GetAutomationsAsync(Token).ConfigureAwait(true);

        var models = new ApiModelsSettingsSource(_host.Http, isDemo: true);
        _ = await models.LoadAsync(Token).ConfigureAwait(true);
        _ = await models.TestAsync("anthropic", Token).ConfigureAwait(true);
        _ = await models.SetDefaultModelAsync("anthropic/claude-sonnet-4-5", Token).ConfigureAwait(true);
        var form = new CustomProviderForm(
            "Acme gateway",
            "acme-gateway",
            CustomProviderProtocol.OpenAiCompatible,
            new Uri("https://llm.acme.example/v1", UriKind.Absolute),
            CustomProviderAuth.None,
            ["acme-large"],
            CustomProviderCapabilities.Chat,
            AllowRemotePlainHttp: false);
        (await models.SaveCustomProviderAsync(form, Token).ConfigureAwait(true)).Succeeded.ShouldBeTrue();
        (await models.RemoveCustomProviderAsync("acme-gateway", Token).ConfigureAwait(true)).Succeeded.ShouldBeTrue();

        // The source never sends a key the service will refuse, so the key route is called directly, as the page would if the operator enabled key writes.
        using var credential = await _host.Http.PutAsJsonAsync(
            new Uri("providers/anthropic/credential", UriKind.Relative),
            new Wire.SetCredentialRequest { ApiKey = "sk-surface-check", PlanRuleAccepted = false },
            Wire.ProvidersJsonContext.Default.SetCredentialRequest,
            Token).ConfigureAwait(true);
        credential.StatusCode.ShouldBe(System.Net.HttpStatusCode.Forbidden);
        _ = await _host.Http.GetFromJsonAsync(new Uri("providers/anthropic", UriKind.Relative), Wire.ProvidersJsonContext.Default.ProviderSummary, Token).ConfigureAwait(true);

        var routes = new List<string>();
        foreach (var route in _host.Recorder.Requests.Select(Normalise))
        {
            if (!routes.Contains(route, StringComparer.Ordinal))
            {
                routes.Add(route);
            }
        }

        return routes;
    }
}
