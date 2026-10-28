using System.Net;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace OmpOS.Api.IntegrationTests.WorkspaceSettings;

/// <summary>The served OpenAPI document describes the settings and workspace routes and none of them opts out of authentication.</summary>
public sealed class WorkspaceSettingsOpenApiTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>Each route is listed as a GET under its tag with the statuses it can answer.</summary>
    [Fact]
    public async Task TheDocument_ListsTheRoutes()
    {
        using var factory = WorkspaceHost.Dev(null);
        using var client = factory.CreateClient();

        using var response = await WorkspaceHost.GetAsync(client, "/openapi/v1.json", Token).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token).ConfigureAwait(true));
        var paths = document.RootElement.GetProperty("paths");
        foreach (var path in new[] { "/workspace/conventions", "/settings/runtime", "/settings/plugins", "/settings/network", "/hooks", "/automations" })
        {
            AssertOperation(paths, path, ["200", "404", "429"]);
        }

        AssertOperation(paths, "/workspace/tree", ["200", "400", "404", "422", "429"]);
        AssertOperation(paths, "/workspace/file", ["200", "400", "404", "413", "415", "422", "429"]);
        document.RootElement.GetProperty("tags").EnumerateArray().Select(static tag => tag.GetProperty("name").GetString()).ShouldContain("Workspace settings", StringComparer.Ordinal);
    }

    private static void AssertOperation(JsonElement paths, string path, string[] statuses)
    {
        paths.TryGetProperty(path, out var item).ShouldBeTrue(path);
        item.TryGetProperty("get", out var operation).ShouldBeTrue($"GET {path}");
        operation.TryGetProperty("security", out var security).ShouldBeFalse($"GET {path} must not override the default authentication with {security}");
        var responses = operation.GetProperty("responses");
        foreach (var status in statuses)
        {
            responses.TryGetProperty(status, out _).ShouldBeTrue($"GET {path} lists {status}");
        }
    }
}
