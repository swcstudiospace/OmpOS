using Autonogrammer.Api.Services;
using Autonogrammer.Contracts.Cowork;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Autonogrammer.Api.Cowork;

/// <summary>Registers the Cowork summary services. The routes are always on: there is no disabled middleware.</summary>
internal static class CoworkServiceRegistration
{
    private sealed class Registered;

    /// <summary>
    /// Binds and validates <c>Cowork</c>, inserts <see cref="CoworkJsonContext"/> into the HTTP JSON chain, registers
    /// <see cref="CodeBoardSource"/> and the <c>cowork-sessions</c> limiter. Call it once, after <c>AddAutomationServices</c>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddCoworkServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (services.Any(static descriptor => descriptor.ServiceType == typeof(Registered)))
        {
            return services;
        }

        _ = services.AddOptions<CoworkOptions>().BindConfiguration(CoworkOptions.SectionName).ValidateOnStart();
        services.AddSingleton<IValidateOptions<CoworkOptions>, CoworkOptionsValidator>();
        _ = services.ConfigureHttpJsonOptions(static options => options.SerializerOptions.TypeInfoResolverChain.Insert(0, CoworkJsonContext.Default));
        services.AddHttpContextAccessor();
        services.AddSingleton<Registered>();
        services.AddSingleton<CoworkSessionEndpoints.Mapped>();
        services.TryAddSingleton<CodeBoardSource>();
        services.TryAddSingleton<CoworkSessionService>();
        services.TryAddKeyedSingleton<IRateLimiterPolicy<string>, CoworkSessionRateLimiterPolicy>(CoworkSessionRateLimiterPolicy.PolicyName);
        _ = services.AddOptions<RateLimiterOptions>().Configure<IServiceProvider>(static (options, provider) =>
        {
            options.AddPolicy(
                CoworkSessionRateLimiterPolicy.PolicyName,
                provider.GetRequiredKeyedService<IRateLimiterPolicy<string>>(CoworkSessionRateLimiterPolicy.PolicyName));
        });
        return services;
    }
}
