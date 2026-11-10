using System.Net;
using System.Security.Cryptography;
using Autonogrammer.Api.Dispatch;
using Autonogrammer.Api.Dispatch.Devices;
using Autonogrammer.Api.Hosting;
using Autonogrammer.Api.OpenApi;
using Autonogrammer.Api.Options;
using Autonogrammer.Api.Security;
using Autonogrammer.Coordination;
using Autonogrammer.Sessions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Autonogrammer.Api.IntegrationTests.Dispatch;

/// <summary>
/// A host that follows the Program.cs order for dispatch: hosting hardening, authentication, the rate limiter, authorization,
/// <see cref="DispatchDisabledMiddleware"/> and <see cref="DeviceEndpoints.MapDispatchEndpoints"/>. Phase 74-05 adds <c>UseWebSockets</c>.
/// </summary>
internal sealed class DispatchApiHost : IAsyncDisposable
{
    /// <summary>The pipeline steps, in the same order as <c>Program.cs</c>.</summary>
    /// <summary>The pipeline steps, in the same order as <c>Program.cs</c>.</summary>
    public static readonly string[] PipelineOrder =
    [
        "UseHostingHardening",
        "UseWebSockets",
        "UseAuthentication",
        "UseRateLimiter",
        "UseAuthorization",
        "DispatchDisabledMiddleware",
        "MapDispatchEndpoints",
    ];

    private readonly WebApplication _app;

    private DispatchApiHost(WebApplication app, ApiAuthOptions auth)
    {
        _app = app;
        Auth = auth;
    }

    /// <summary>Gets the authentication options the host is using. Tests may mutate the allow-list.</summary>
    public ApiAuthOptions Auth { get; }

    /// <summary>Gets the service provider.</summary>
    public IServiceProvider Services => _app.Services;

    /// <summary>Starts a Development host with dispatch enabled and test authentication.</summary>
    /// <param name="cancellationToken">Cancels startup.</param>
    /// <param name="enabled">Whether <c>Dispatch:Enabled</c> is true.</param>
    /// <param name="configure">Extra configuration.</param>
    /// <param name="registerServices">Extra service registrations, applied after the dispatch services; a test replaces a seam here.</param>
    /// <param name="network">Listen on loopback for a real desktop connector instead of using TestServer.</param>
    /// <returns>The host.</returns>
    public static async Task<DispatchApiHost> StartAsync(CancellationToken cancellationToken, bool enabled = true, Action<Dictionary<string, string?>>? configure = null, Action<IServiceCollection>? registerServices = null, bool network = false)
    {
        using var credential = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var command = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var auth = new ApiAuthOptions { Mode = ApiAuthMode.Oidc, AllowedSubjects = ["acct"] };
        var settings = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Dispatch:Enabled"] = enabled ? "true" : "false",
            ["Dispatch:Credentials:Issuer"] = "autonogrammer-dispatch",
            ["Dispatch:Credentials:SigningKeyPem"] = credential.ExportPkcs8PrivateKeyPem(),
            ["Dispatch:CommandSigning:KeyPem"] = command.ExportPkcs8PrivateKeyPem(),
            ["Coordination:Store"] = "InMemory",
            ["Sessions:Store"] = "InMemory",
            ["Dispatch:Sweeper:Enabled"] = "false",
            ["Hosting:RequestTimeoutSeconds"] = "30",
        };
        configure?.Invoke(settings);

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development, ApplicationName = "Autonogrammer.Api" });
        builder.Configuration.AddInMemoryCollection(settings);
        ConfigureServer(builder, network);
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton<ISessionRepository, InMemorySessionRepository>();
        builder.AddHostingHardening();
        builder.Services.AddSingleton<IOptions<ApiAuthOptions>>(Microsoft.Extensions.Options.Options.Create(auth));
        builder.Services.AddSingleton<IAuthorizationHandler, AllowedSubjectAuthorizationHandler>();
        builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, ApiAuthorizationResultHandler>();
        builder.Services.AddAuthentication(DispatchTestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, DispatchTestAuthHandler>(DispatchTestAuthHandler.SchemeName, static _ => { });
        builder.Services.AddAuthorizationBuilder().SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().AddRequirements(new AllowedSubjectRequirement()).Build());
        builder.Services.AddHttpContextAccessor();
        builder.Services.TryAddScoped<IUserContext, AuthenticatedUserContext>();
        builder.Services.AddInMemoryCoordination();
        builder.Services.AddDispatchServices(builder.Environment);
        builder.Services.AddDispatchHub();
        builder.Services.TryAddSingleton<ITenantHasher>(new Autonogrammer.Coordination.Dragonfly.TenantHasher(new Autonogrammer.Coordination.Dragonfly.TenantHasherOptions { Pepper = "test-pepper" }));
        builder.Services.TryAddSingleton<Autonogrammer.Api.Audit.IAuditLedger, Autonogrammer.Api.Audit.NullAuditLedger>();
        registerServices?.Invoke(builder.Services);
        builder.Services.AddAutonogrammerOpenApi();
        builder.Services.AddRateLimiter(_ => { });

        var app = builder.Build();
        app.UseDeveloperExceptionPage();
        app.UseHostingHardening();
        app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(20) });
        UseTestRemoteAddress(app);
        app.UseAuthentication();
        app.UseRateLimiter();
        app.UseAuthorization();
        app.UseMiddleware<DispatchDisabledMiddleware>();
        app.MapDispatchEndpoints();
        app.MapOpenApi().AllowAnonymous();
        await app.StartAsync(cancellationToken).ConfigureAwait(true);
        return new DispatchApiHost(app, auth);
    }

    /// <summary>Gets a client that talks to the test server.</summary>
    /// <returns>The client.</returns>
    public HttpClient GetTestClient() => _app.Services.GetRequiredService<IServer>() is TestServer
        ? _app.GetTestClient()
        : new HttpClient { BaseAddress = new Uri(_app.Urls.Single()) };

    /// <summary>Gets the loopback origin for a network host.</summary>
    public Uri Origin => new(_app.Urls.Single());

    /// <summary>Creates a WebSocket client that talks to the test server.</summary>
    /// <returns>The client.</returns>
    public WebSocketClient CreateWebSocketClient() => ((TestServer)_app.Services.GetRequiredService<IServer>()).CreateWebSocketClient();

    private static void ConfigureServer(WebApplicationBuilder builder, bool network)
    {
        if (network)
        {
            builder.WebHost.UseUrls("http://127.0.0.1:0");
        }
        else
        {
            builder.WebHost.UseTestServer();
        }
    }

    /// <summary>Test-only: takes the remote address from the X-Test-Remote-Address header, so a test chooses the source address of a device connection.</summary>
    /// <param name="app">The application.</param>
    private static void UseTestRemoteAddress(WebApplication app)
    {
        app.Use((context, next) =>
        {
            if (context.Request.Headers.TryGetValue("X-Test-Remote-Address", out var address) && IPAddress.TryParse(address.ToString(), out var remote))
            {
                context.Connection.RemoteIpAddress = remote;
            }

            return next(context);
        });
    }

    /// <summary>Posts a JSON body and returns the response. The caller disposes it.</summary>
    /// <param name="client">The client.</param>
    /// <param name="path">The relative path.</param>
    /// <param name="json">The JSON body.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The response.</returns>
    public static async Task<HttpResponseMessage> PostJsonAsync(HttpClient client, string path, string json, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        return await client.PostAsync(new Uri(path, UriKind.Relative), content, cancellationToken).ConfigureAwait(true);
    }


    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await _app.DisposeAsync().ConfigureAwait(true);
}
