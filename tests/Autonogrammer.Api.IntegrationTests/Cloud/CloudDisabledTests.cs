using System.Net;
using System.Text;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Cloud;

/// <summary>A default host has cloud off: every cloud route answers a 404 problem with a stable code and nothing is provisioned, yet the OpenAPI document still describes the routes.</summary>
public sealed class CloudDisabledTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("GET", "/cloud/agents")]
    [InlineData("POST", "/cloud/agents")]
    [InlineData("GET", "/cloud/agents/cloud-1/events")]
    [InlineData("DELETE", "/cloud/agents/cloud-1")]
    [InlineData("GET", "/cloud/secrets")]
    [InlineData("PUT", "/cloud/secrets/TOKEN")]
    [InlineData("GET", "/cloud/limits")]
    public async Task EveryCloudRouteAnswersCloudDisabledWhileCloudIsOff(string method, string path)
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), new Uri(path, UriKind.Relative));
        if (!string.Equals(method, "GET", StringComparison.Ordinal) && !string.Equals(method, "DELETE", StringComparison.Ordinal))
        {
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        }

        using var response = await client.SendAsync(request, Token).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token).ConfigureAwait(true));
        body.RootElement.GetProperty("type").GetString().ShouldBe("/problems/cloud_disabled");
    }

    [Fact]
    public async Task TheOpenApiDocumentDescribesTheCloudRoutesWhileCloudIsOff()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative), Token).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token).ConfigureAwait(true));
        var paths = document.RootElement.GetProperty("paths");
        paths.TryGetProperty("/cloud/agents", out var agents).ShouldBeTrue();
        agents.TryGetProperty("post", out _).ShouldBeTrue();
        paths.TryGetProperty("/cloud/agents/{id}/events", out _).ShouldBeTrue();
        paths.TryGetProperty("/cloud/secrets/{name}", out _).ShouldBeTrue();
    }
}
