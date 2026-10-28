using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OmpOS.Contracts.WorkspaceSettings;

namespace OmpOS.Api.Services.WorkspaceSettings;

/// <summary>Registers the services behind the read-only settings and workspace endpoints.</summary>
internal static class WorkspaceSettingsServiceRegistration
{
    /// <summary>
    /// Registers the readers, the options (<c>Api:Workspace</c>), the rate-limit policy and the source-generated JSON context of the settings and workspace
    /// endpoints. The seams a host fills (<see cref="IWorkspacePluginCheck"/>, <see cref="IWorkspacePluginInventory"/>,
    /// <see cref="IWorkspaceAutomationsProvider"/>, <see cref="IUserHomeProvider"/>) get their honest defaults through <c>TryAdd</c>, so a registration made before
    /// this call wins. It needs <c>AddRateLimiter</c> and the <c>Worktrees</c> options, which the host registers.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddWorkspaceSettingsServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _ = services.ConfigureHttpJsonOptions(static options =>
            options.SerializerOptions.TypeInfoResolverChain.Insert(0, WorkspaceSettingsJsonContext.Default));
        _ = services.AddOptions<SessionWorkspaceOptions>().BindConfiguration(SessionWorkspaceOptions.SectionName);
        _ = services.AddOptions<WorkspaceSettingsOptions>()
            .BindConfiguration(WorkspaceSettingsOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.TryAddSingleton<WorkspaceLocator>();
        services.TryAddSingleton<IUserHomeProvider, ProcessUserHomeProvider>();
        services.TryAddSingleton<IWorkspacePluginCheck, UnavailablePluginCheck>();
        services.TryAddSingleton<IWorkspacePluginInventory, EmptyPluginInventory>();
        services.TryAddSingleton<IWorkspaceAutomationsProvider, NoAutomationsProvider>();
        services.TryAddSingleton<WorkspaceAccess>();
        services.TryAddSingleton<ConventionsReader>();
        services.TryAddSingleton<RuntimeSettingsReader>();
        services.TryAddSingleton<PluginsSettingsReader>();
        services.TryAddSingleton<NetworkSettingsReader>();
        services.TryAddSingleton<HooksReader>();
        services.TryAddSingleton<AutomationsReader>();
        services.TryAddSingleton<WorkspaceTreeReader>();
        services.TryAddSingleton<WorkspaceFileReader>();
        services.TryAddKeyedSingleton<IRateLimiterPolicy<string>, WorkspaceSettingsRateLimiterPolicy>(WorkspaceSettingsRateLimiterPolicy.PolicyName);
        _ = services.AddOptions<RateLimiterOptions>().Configure<IServiceProvider>(static (options, provider) =>
            options.AddPolicy(
                WorkspaceSettingsRateLimiterPolicy.PolicyName,
                provider.GetRequiredKeyedService<IRateLimiterPolicy<string>>(WorkspaceSettingsRateLimiterPolicy.PolicyName)));
        return services;
    }
}
