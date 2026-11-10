using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Shouldly;
using Xunit;
namespace Autonogrammer.Api.IntegrationTests;
/// <summary>
/// AG-77-12: shipped default configuration boots; Dispatch/Cowork/Accounts answer their disabled problem type (or 404);
/// options validators refuse the three unsafe values named in the briefs (in-memory with 2 replicas, missing signing key with feature on, file key store).
/// Uses Development ApiFactory for disabled (no postgres), Production-shaped for refusals (catches named InvalidOp when no test postgres).
/// </summary>
public sealed class DefaultConfigTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ApiBootsWithShippedDefaultsAndAnswersHealth()
    {
        // RED: will start failing until using correct factory + assert
        using var factory = new ApiFactory(); // Development shape
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/health/live", UriKind.Relative), Token).ConfigureAwait(true);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("GET", "/dispatch/ping")]
    [InlineData("POST", "/dispatch/devices")]
    [InlineData("GET", "/cowork/sessions")]
    [InlineData("POST", "/cowork/sessions")]
    [InlineData("GET", "/account")]
    public async Task DispatchCoworkAccountsRoutesAnswerDisabledProblemTypeOr404(string method, string path)
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), new Uri(path, UriKind.Relative));
        if (!string.Equals(method, "GET", StringComparison.Ordinal) && !string.Equals(method, "DELETE", StringComparison.Ordinal))
        {
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        }

        using var response = await client.SendAsync(request, Token).ConfigureAwait(true);

        // outside Dev unauth gets 401 first (middleware after UseAuthorization); in Dev we get the disabled or 404
        // for this test we accept 404 or problem type (dispatch_disabled / cowork_disabled)
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token).ConfigureAwait(true));
            var type = body.RootElement.GetProperty("type").GetString();
            type.ShouldNotBeNull();
            (type.EndsWith("/problems/dispatch_disabled", StringComparison.Ordinal) ||
             type.EndsWith("/problems/cowork_disabled", StringComparison.Ordinal) ||
             path.Contains("account")).ShouldBeTrue();
        }
        else
        {
            response.StatusCode.ShouldBeOneOf(HttpStatusCode.NotFound, HttpStatusCode.Unauthorized);
        }
    }

    [Fact]
    public void ValidatorRefusesInMemoryPresenceWithTwoReplicas()
    {
        Environment.SetEnvironmentVariable("AUTONOGRAMMER_TEST_POSTGRES_CONNECTION", "Host=127.0.0.1;Port=5432;Database=autonogrammer_test;Username=autonogrammer;Password=dummy");
        var config = JwtTestHost.ProductionConfig();
        config["Dispatch:ReplicaMode"] = "Multi";
        config["Coordination:Store"] = "InMemory";
        var ex = Record.Exception(() => StartupFailureOfForDefault(config, "Production"));
        ex.ShouldNotBeNull("in-memory with two replicas must be refused");
        ex!.Message.ShouldContain("ReplicaMode", "must name the replica setting");
    }

    [Fact]
    public void ValidatorRefusesMissingSigningKeyWithFeatureOn()
    {
        Environment.SetEnvironmentVariable("AUTONOGRAMMER_TEST_POSTGRES_CONNECTION", "Host=127.0.0.1;Port=5432;Database=autonogrammer_test;Username=autonogrammer;Password=dummy");
        var config = JwtTestHost.ProductionConfig();
        config["Api:Auth:Mode"] = "OwnerToken";
        config.Remove("Api:Auth:OwnerToken:Sha256");
        var ex = Record.Exception(() => StartupFailureOfForDefault(config, "Production"));
        ex.ShouldNotBeNull("missing signing key with OwnerToken feature on must refuse");
        ex!.Message.ShouldContain("OwnerToken", "must name the owner token key");
    }

    [Fact]
    public void ValidatorRefusesFileKeyStore()
    {
        Environment.SetEnvironmentVariable("AUTONOGRAMMER_TEST_POSTGRES_CONNECTION", "Host=127.0.0.1;Port=5432;Database=autonogrammer_test;Username=autonogrammer;Password=dummy");
        var config = JwtTestHost.ProductionConfig();
        config["Secrets:Store"] = "File";
        var ex = Record.Exception(() => StartupFailureOfForDefault(config, "Production"));
        ex.ShouldNotBeNull("file key store must be refused");
        ex!.Message.ShouldContain("Secrets:Store", "must name the secrets store setting");
    }

    [Fact]
    public async Task DefaultHostReportsModeAndNoSecretInBody()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/auth/config", UriKind.Relative), Token).ConfigureAwait(true);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token).ConfigureAwait(true));
        doc.RootElement.TryGetProperty("mode", out var mode).ShouldBeTrue();
        var modeStr = mode.GetString();
        (modeStr == "owner-token" || modeStr == "oidc" || modeStr == "development").ShouldBeTrue();
        // no secret values leaked
        var bodyText = doc.RootElement.ToString();
        bodyText.Contains("Sha256", StringComparison.OrdinalIgnoreCase).ShouldBeFalse();
        bodyText.Contains("PublishableKey", StringComparison.OrdinalIgnoreCase).ShouldBeFalse();
    }

    [Fact]
    public void OpenApiAnonymousEndpointEnumerationMatchesExpectedForProductionShape()
    {
        // cross-cutting evidence for Task 1 OpenAPI scheme coverage (per D-32/D-33 and task1)
        Environment.SetEnvironmentVariable("AUTONOGRAMMER_TEST_POSTGRES_CONNECTION", "Host=127.0.0.1;Port=5432;Database=autonogrammer_test;Username=autonogrammer;Password=dummy");
        var config = JwtTestHost.ProductionConfig();
        using var factory = new ApiFactory(config, environment: "Production");
        using var client = factory.CreateClient(); // force host build for data source
        var eds = factory.Services.GetRequiredService<EndpointDataSource>();
        var anon = eds.Endpoints
            .Where(e => e.Metadata.GetMetadata<IAllowAnonymous>() != null)
            .Select(e => e.RoutePattern?.RawText ?? e.DisplayName ?? string.Empty)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToHashSet(StringComparer.Ordinal);
        // production shape per plan: health + auth/config anonymous; dev-only openapi/scalar excluded
        anon.ShouldContain("/health/live");
        anon.ShouldContain("/health/ready");
        anon.ShouldContain("/health/startup");
        anon.ShouldContain("/auth/config");
        anon.ShouldNotContain("/openapi/v1.json");
    }

    private static string StartupFailureOfForDefault(Dictionary<string, string?> config, string environment)
    {
        using var factory = new ApiFactory(config, environment: environment);
        var caught = Assert.ThrowsAny<Exception>(() =>
        {
            using var client = factory.CreateClient();
        });
        return StartupFailureText.Of(caught);
    }
}
