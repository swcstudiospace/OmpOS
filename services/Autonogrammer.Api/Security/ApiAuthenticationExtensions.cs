using Autonogrammer.Api.Endpoints;
using Autonogrammer.Api.Middleware;
using Autonogrammer.Api.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Autonogrammer.Api.Security;

/// <summary>
/// Wires API authentication: the options and their start-time validation, the authentication scheme of the configured mode, the
/// fallback authorization policy (authenticated and on the allow-list), the <see cref="IUserContext"/> choice, and the anonymous
/// <c>/auth/config</c> endpoint.
/// </summary>
internal static class ApiAuthenticationExtensions
{
    /// <summary>
    /// Registers API authentication. The default scheme is a policy scheme that forwards, per request, to exactly one handler: the
    /// owner-token handler in <c>OwnerToken</c> mode, JwtBearer otherwise. The mode is read from the bound options when the request
    /// runs, so configuration added after registration is honoured.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="environment">The host environment.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddApiAuthentication(this IServiceCollection services, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(environment);

        services.AddOptions<ApiAuthOptions>()
            .BindConfiguration(ApiAuthOptions.SectionName)
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<ApiAuthOptions>, ApiAuthOptionsValidator>();

        services.AddAuthentication(OwnerTokenDefaults.PolicyScheme)
            .AddPolicyScheme(OwnerTokenDefaults.PolicyScheme, displayName: null, static options =>
                options.ForwardDefaultSelector = static context =>
                    context.RequestServices.GetRequiredService<IOptions<ApiAuthOptions>>().Value.Mode == ApiAuthMode.OwnerToken
                        ? OwnerTokenDefaults.AuthenticationScheme
                        : JwtBearerDefaults.AuthenticationScheme)
            .AddScheme<AuthenticationSchemeOptions, OwnerTokenAuthenticationHandler>(OwnerTokenDefaults.AuthenticationScheme, displayName: null, static _ => { })
            .AddJwtBearer();
        services.AddSingleton<IPostConfigureOptions<JwtBearerOptions>, JwtBearerSetup>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<OwnerTokenFailureLimiter>();

        // Default policy: every endpoint without an explicit policy requires an authenticated user who is allowed on this deployment.
        // Health and (in Development) OpenAPI opt out in the pipeline.
        services.AddSingleton<IAuthorizationHandler, AllowedSubjectAuthorizationHandler>();
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, ApiAuthorizationResultHandler>();
        services.AddAuthorizationBuilder().SetFallbackPolicy(
            new AuthorizationPolicyBuilder().RequireAuthenticatedUser().AddRequirements(new AllowedSubjectRequirement()).Build());

        services.AddHttpContextAccessor();
        services.AddScoped<IUserContext>(static provider =>
            provider.GetRequiredService<IOptions<ApiAuthOptions>>().Value.AllowDevelopmentPrincipal
                ? new SingleDevPrincipal()
                : new AuthenticatedUserContext(provider.GetRequiredService<IHttpContextAccessor>()));

        services.AddRateLimiter(options => options.AddPolicy<string, AuthConfigRateLimiterPolicy>(AuthConfigEndpoints.PolicyName));
        return services;
    }

    /// <summary>
    /// Adds authentication and, when <c>Api:Auth:AllowDevelopmentPrincipal</c> is set (Development only), the development boundary.
    /// It does not add authorization or the rate limiter.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The application builder.</returns>
    public static IApplicationBuilder UseApiAuthentication(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.UseAuthentication();
        if (app.ApplicationServices.GetRequiredService<IOptions<ApiAuthOptions>>().Value.AllowDevelopmentPrincipal)
        {
            app.UseMiddleware<AuthBoundaryMiddleware>();
        }

        return app;
    }

    /// <summary>Maps the anonymous <c>GET /auth/config</c> endpoint.</summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The route builder.</returns>
    public static IEndpointRouteBuilder MapAuthConfigEndpoints(this IEndpointRouteBuilder endpoints) =>
        AuthConfigEndpoints.Map(endpoints);
}
