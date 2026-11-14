using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests;

/// <summary>Exercises default-host responses and the startup checks for enabled Dispatch.</summary>
public sealed class DefaultConfigTests
{
    private static readonly string[] ExpectedAnonymousPaths =
    [
        "/auth/config",
        "/cowork/artifact-render/{ticket}",
        "/health/live",
        "/health/ready",
        "/health/startup",
        "/hooks/automations/{id}",
        "/source",
    ];

    private const string CredentialKeyRefusal = "Dispatch:Credentials:SigningKeyPem must be a PKCS#8 PEM private key on the P-256 curve.";
    private const string CommandKeyRefusal = "Dispatch:CommandSigning:KeyPem must be a PKCS#8 PEM private key on the P-256 curve.";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task DevelopmentHostWithDefaultModulesAnswersLiveness()
    {
        // ApiFactory opts into the Development principal and extends the request timeout.
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/health/live", UriKind.Relative), Token).ConfigureAwait(true);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("POST", "/dispatch/devices", "dispatch_disabled")]
    [InlineData("GET", "/cowork/artifacts", "cowork_disabled")]
    public async Task MappedDisabledModuleRoutesReturnTheirProblemType(string method, string path, string code)
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), new Uri(path, UriKind.Relative));

        using var response = await client.SendAsync(request, Token).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token).ConfigureAwait(true));
        body.RootElement.GetProperty("type").GetString().ShouldEndWith("/problems/" + code);
    }

    [Theory]
    [InlineData("/account")]
    [InlineData("/account/sessions")]
    [InlineData("/account/sessions/missing")]
    [InlineData("/account/board")]
    public async Task DefaultAccountRoutesAreDisabledForAuthenticatedDevelopmentPrincipal(string path)
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(new Uri(path, UriKind.Relative), Token).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token).ConfigureAwait(true));
        body.RootElement.GetProperty("type").GetString().ShouldEndWith("/problems/account_disabled");
    }

    [Theory]
    [InlineData("/account")]
    [InlineData("/account/sessions")]
    [InlineData("/account/sessions/missing")]
    [InlineData("/account/board")]
    public async Task DefaultAccountRoutesRejectAnonymousBeforeDisabledHandling(string path)
    {
        using var factory = new ApiFactory(JwtTestHost.ProductionConfig(), JwtTestHost.InjectStaticMetadata, environment: "Production");
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(new Uri(path, UriKind.Relative), Token).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>Production settings with dispatch enabled, two distinct valid P-256 keys and an https hub origin. Each unsafe test varies one setting from it.</summary>
    private static Dictionary<string, string?> EnabledDispatchBaseline()
    {
        using var credential = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var command = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var config = JwtTestHost.ProductionConfig();
        config["Dispatch:Enabled"] = "true";
        config["Dispatch:Credentials:SigningKeyPem"] = credential.ExportPkcs8PrivateKeyPem();
        config["Dispatch:CommandSigning:KeyPem"] = command.ExportPkcs8PrivateKeyPem();
        config["Dispatch:HubOrigin"] = "https://hub.example.test";
        return config;
    }

    [Fact]
    public void EnabledDispatchWithValidKeysStillRefusesTheInMemoryStoreInProduction()
    {
        var failure = StartupFailureTextFor(EnabledDispatchBaseline());

        failure.ShouldContain("Dispatch:Enabled refuses the in-memory IDispatchStore outside Development.", Case.Sensitive);
        failure.ShouldNotContain(CredentialKeyRefusal, Case.Sensitive);
        failure.ShouldNotContain(CommandKeyRefusal, Case.Sensitive);
        failure.ShouldNotContain("Dispatch:HubOrigin", Case.Sensitive);
    }

    [Fact]
    public void EnabledDispatchWithMultiReplicaInMemoryCoordinationFailsStartup()
    {
        var config = EnabledDispatchBaseline();
        config["Dispatch:ReplicaMode"] = "Multi";
        config["Coordination:Store"] = "InMemory";

        var failure = StartupFailureTextFor(config);

        failure.ShouldContain("Dispatch:ReplicaMode=Multi requires Coordination:Store=Dragonfly", Case.Sensitive);
        failure.ShouldNotContain(CredentialKeyRefusal, Case.Sensitive);
        failure.ShouldNotContain(CommandKeyRefusal, Case.Sensitive);
    }

    [Fact]
    public void EnabledDispatchWithoutCredentialSigningKeyFailsStartup()
    {
        var config = EnabledDispatchBaseline();
        config["Dispatch:Credentials:SigningKeyPem"] = string.Empty;

        var failure = StartupFailureTextFor(config);

        failure.ShouldContain(CredentialKeyRefusal, Case.Sensitive);
        failure.ShouldNotContain(CommandKeyRefusal, Case.Sensitive);
        failure.ShouldNotContain("Dispatch:ReplicaMode=Multi", Case.Sensitive);
    }

    [Fact]
    public void EnabledDispatchWithoutCommandSigningKeyFailsStartup()
    {
        var config = EnabledDispatchBaseline();
        config["Dispatch:CommandSigning:KeyPem"] = string.Empty;

        var failure = StartupFailureTextFor(config);

        failure.ShouldContain(CommandKeyRefusal, Case.Sensitive);
        failure.ShouldNotContain(CredentialKeyRefusal, Case.Sensitive);
        failure.ShouldNotContain("Dispatch:ReplicaMode=Multi", Case.Sensitive);
    }

    [Fact]
    public async Task DevelopmentAuthConfigReportsModeWithoutDispatchSigningKey()
    {
        const string canary = "dispatch-signing-key-canary";
        using var factory = new ApiFactory(new Dictionary<string, string?>(StringComparer.Ordinal) { ["Dispatch:Credentials:SigningKeyPem"] = canary });
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/auth/config", UriKind.Relative), Token).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var text = await response.Content.ReadAsStringAsync(Token).ConfigureAwait(true);
        text.ShouldNotContain(canary, Case.Sensitive);
        using var body = JsonDocument.Parse(text);
        body.RootElement.GetProperty("mode").GetString().ShouldBe("development");
    }

    [Fact]
    public void ProductionShapeEnumeratesAllAnonymousEndpoints()
    {
        var config = JwtTestHost.ProductionConfig();
        using var factory = new ApiFactory(config, environment: "Production");
        using var client = factory.CreateClient();
        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>();
        var anonymousPaths = endpoints.Endpoints
            .Where(static endpoint => endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            .OfType<RouteEndpoint>()
            .Select(static endpoint => endpoint.RoutePattern.RawText ?? string.Empty)
            .OrderBy(static path => path, StringComparer.Ordinal)
            .ToArray();

        anonymousPaths.ShouldBe(ExpectedAnonymousPaths);
    }

    private static string StartupFailureTextFor(Dictionary<string, string?> config)
    {
        using var factory = new ApiFactory(config, environment: "Production");
        var caught = Assert.ThrowsAny<Exception>(() =>
        {
            using var client = factory.CreateClient();
        });
        return StartupFailureText.Of(caught);
    }
}
