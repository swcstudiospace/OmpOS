using Autonogrammer.Cowork.Artifacts;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Autonogrammer.Api.Cowork.Artifacts;

/// <summary>Registers the artifact API module. Routes stay mapped while disabled so the OpenAPI document still describes them.</summary>
internal static class ArtifactsServiceRegistration
{
    /// <summary>Binds options, inserts the JSON context, and registers stores, tickets, metrics and limiters.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddCoworkArtifactServices(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        _ = configuration;
        _ = services.AddArtifacts();
        services.AddSingleton(static provider => provider.GetRequiredService<IOptions<ArtifactOptions>>().Value);
        _ = services.AddOptions<ArtifactApiOptions>().BindConfiguration(ArtifactApiOptions.SectionName).ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<ArtifactApiOptions>, ArtifactApiOptionsValidator>());
        _ = services.ConfigureHttpJsonOptions(static options => options.SerializerOptions.TypeInfoResolverChain.Insert(0, ArtifactsJsonContext.Default));
        services.TryAddSingleton<ArtifactAccountDirectory>();
        services.TryAddSingleton<ArtifactGrants>();
        services.TryAddSingleton<ArtifactRenderTickets>();
        services.TryAddSingleton<ArtifactMetrics>();
        services.TryAddSingleton<ArtifactAudit>(static provider => new ArtifactAudit(
            provider.GetRequiredService<ILogger<ArtifactAudit>>(),
            string.IsNullOrWhiteSpace(provider.GetRequiredService<IConfiguration>()["Sessions:Postgres:ConnectionString"])
                ? null
                : provider.GetService<NpgsqlDataSource>()));
        services.TryAddKeyedSingleton<IRateLimiterPolicy<string>, ArtifactOwnerRateLimiterPolicy>(ArtifactOwnerRateLimiterPolicy.PolicyName);
        services.TryAddKeyedSingleton<IRateLimiterPolicy<string>, ArtifactRenderRateLimiterPolicy>(ArtifactRenderRateLimiterPolicy.PolicyName);
        _ = services.AddOptions<RateLimiterOptions>().Configure<IServiceProvider>(static (options, provider) =>
        {
            options.AddPolicy(ArtifactOwnerRateLimiterPolicy.PolicyName, provider.GetRequiredKeyedService<IRateLimiterPolicy<string>>(ArtifactOwnerRateLimiterPolicy.PolicyName));
            options.AddPolicy(ArtifactRenderRateLimiterPolicy.PolicyName, provider.GetRequiredKeyedService<IRateLimiterPolicy<string>>(ArtifactRenderRateLimiterPolicy.PolicyName));
        });
        return services;
    }
}
