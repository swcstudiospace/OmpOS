using Autonogrammer.Api.Accounts;
using Autonogrammer.Api.Cowork;
using Autonogrammer.Api.IntegrationTests.Cloud;
using Autonogrammer.Api.Options;
using Autonogrammer.Api.Security;
using Autonogrammer.Api.Serialization;
using Autonogrammer.Api.Services;
using Autonogrammer.Contracts.Catalog;
using Autonogrammer.Contracts.Serialization;
using Autonogrammer.Sessions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Autonogrammer.Api.IntegrationTests.Cowork;

/// <summary>A test server for the Cowork summary routes and the Code board, with the owner named by <c>X-Test-Owner</c>.</summary>
internal sealed class CoworkApiHost : IAsyncDisposable
{
    /// <summary>The default owner.</summary>
    public const string Alice = "alice";

    /// <summary>Another owner.</summary>
    public const string Bob = "bob";

    private readonly WebApplication _app;

    private CoworkApiHost(WebApplication app) => _app = app;

    /// <summary>Gets the host's services.</summary>
    public IServiceProvider Services => _app.Services;

    /// <summary>Starts a host with the in-memory session store.</summary>
    /// <param name="config">Configuration overrides. They win over the defaults.</param>
    /// <param name="cancellationToken">Cancels the start.</param>
    /// <returns>The running host.</returns>
    public static async Task<CoworkApiHost> StartAsync(Dictionary<string, string?>? config = null, CancellationToken cancellationToken = default)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        _ = builder.WebHost.UseTestServer();
        var settings = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Cowork:RequestsPerMinute"] = "1000",
            ["Accounts:RequestsPerMinute"] = "1000",
        };
        if (config is not null)
        {
            foreach (var (key, value) in config)
            {
                settings[key] = value;
            }
        }

        _ = builder.Configuration.AddInMemoryCollection(settings);
        var services = builder.Services;
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ISessionRepository, InMemorySessionRepository>();
        _ = services.AddOptions<ApiOptions>().BindConfiguration(ApiOptions.SectionName);
        services.AddAuthentication(TestOwnerAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestOwnerAuthHandler>(TestOwnerAuthHandler.SchemeName, null);
        services.AddAuthorizationBuilder().SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
        services.AddRateLimiter(options => options.RejectionStatusCode = StatusCodes.Status429TooManyRequests);
        services.AddCoworkServices();
        services.AddAccountsServices();
        services.AddScoped<IUserContext>(static provider => new AuthenticatedUserContext(provider.GetRequiredService<IHttpContextAccessor>()));
        _ = services.ConfigureHttpJsonOptions(static options =>
        {
            options.SerializerOptions.TypeInfoResolverChain.Insert(0, ApiJsonContext.Default);
            options.SerializerOptions.TypeInfoResolverChain.Insert(0, AutonogrammerJsonContext.Default);
            options.SerializerOptions.TypeInfoResolverChain.Insert(0, CatalogJsonContext.Default);
        });
        var app = builder.Build();
        app.UseAuthentication();
        app.UseRateLimiter();
        app.UseAuthorization();
        _ = app.MapCoworkSessionEndpoints();
        _ = app.MapGet("/sessions", ListSessionsAsync);
        _ = app.MapAccountEndpoints();
        await app.StartAsync(cancellationToken).ConfigureAwait(false);
        return new CoworkApiHost(app);
    }

    /// <summary>Gets a client authenticated as <paramref name="owner"/>.</summary>
    /// <param name="owner">The owner name.</param>
    /// <returns>The client.</returns>
    public HttpClient Client(string owner = Alice)
    {
        var client = _app.GetTestClient();
        client.DefaultRequestHeaders.Add(TestOwnerAuthHandler.Header, owner);
        return client;
    }

    private static async Task<IResult> ListSessionsAsync(CodeBoardSource source, IUserContext user, TimeProvider time, CancellationToken cancellationToken)
    {
        var sessions = await source.ListAsync(user.Name, cancellationToken).ConfigureAwait(false);
        var now = time.GetUtcNow();
        var entries = sessions.Select(session => CatalogProjector.ToEntry(session, session.ClosedAt is not null, now, null, null)).ToArray();
        return Results.Ok(entries);
    }


    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await _app.DisposeAsync().ConfigureAwait(false);
}
