using System.Net;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests;

/// <summary>The served OpenAPI document lists the hosted run endpoints and their shapes, and none of them opts out of authentication.</summary>
public sealed class CloudRunOpenApiTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>The document lists create, status, output and cancel with their statuses, the five run states, and no anonymous run operation.</summary>
    [Fact]
    public async Task OpenApiDocumentListsTheRunEndpoints()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative), Token).ConfigureAwait(true);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token).ConfigureAwait(true));
        var root = document.RootElement;
        var paths = root.GetProperty("paths");

        AssertOperation(paths, "/sessions/{id}/runs", "post", ["202", "400", "404", "409", "413", "415", "429"]);
        AssertOperation(paths, "/sessions/{id}/runs/{runId}", "get", ["200", "404"]);
        AssertOperation(paths, "/sessions/{id}/runs/{runId}/output", "get", ["200", "404"]);
        AssertOperation(paths, "/sessions/{id}/runs/{runId}/cancel", "post", ["202", "404", "409"]);

        var schemas = root.GetProperty("components").GetProperty("schemas");
        schemas.GetProperty("RunStatus").GetProperty("enum").EnumerateArray().Select(static item => item.GetString())
            .ShouldBe(["Queued", "Running", "Completed", "Failed", "Cancelled"]);
        var outputProperties = schemas.GetProperty("RunOutput").GetProperty("properties");
        outputProperties.TryGetProperty("transcript", out _).ShouldBeTrue();
        outputProperties.TryGetProperty("changes", out _).ShouldBeTrue();
        schemas.GetProperty("RunOutput").GetProperty("description").GetString().ShouldNotBeNull()
            .ShouldContain("transcript only", Case.Sensitive);
        root.GetProperty("tags").EnumerateArray().Select(static tag => tag.GetProperty("name").GetString()).ShouldContain("RunEndpoints", StringComparer.Ordinal);
    }

    private static void AssertOperation(JsonElement paths, string path, string method, string[] statuses)
    {
        paths.TryGetProperty(path, out var item).ShouldBeTrue(path);
        item.TryGetProperty(method, out var operation).ShouldBeTrue($"{method} {path}");
        operation.TryGetProperty("security", out var security).ShouldBeFalse($"{method} {path} must not override the default authentication with {security}");
        var responses = operation.GetProperty("responses");
        foreach (var status in statuses)
        {
            responses.TryGetProperty(status, out _).ShouldBeTrue($"{method} {path} lists {status}");
        }
    }
}
