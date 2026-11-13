using Autonogrammer.Api.Cloud;
using Autonogrammer.Api.Cloud.Options;
using Autonogrammer.Api.Options;
using Autonogrammer.Api.Services.WorkspaceSettings;
using Autonogrammer.Contracts.Automations;
using Autonogrammer.Harness.Automations.Hosted;
using Autonogrammer.Sessions.Postgres;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Autonogrammer.Api.Automations;

/// <summary>Registers the automation services. Automations are off by default: with <c>Automations:Enabled</c> false the services are registered, every route answers 404 and the hosted service does nothing.</summary>
internal static class AutomationServiceRegistration
{
    /// <summary>
    /// Registers the stores, the runner, the scheduler, the webhook receiver, the cloud target and the rate-limit policies, binds and validates
    /// <c>Automations</c>, adds the automation JSON context to the HTTP JSON chain and answers the settings seam
    /// <c>IWorkspaceAutomationsProvider</c> with the caller's automations. Call it once, after <c>AddCloudAgentServices</c> and
    /// <c>AddWorkspaceSettingsServices</c> (the order is not significant for correctness: the registrations here replace the defaults).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddAutomationServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (services.Any(static descriptor => descriptor.ServiceType == typeof(AutomationEndpointsMapped)))
        {
            return services;
        }

        _ = services.AddOptions<AutomationOptions>().BindConfiguration(AutomationOptions.SectionName).ValidateOnStart();
        services.AddSingleton<IValidateOptions<AutomationOptions>, AutomationOptionsValidator>();
        _ = services.ConfigureHttpJsonOptions(static options => options.SerializerOptions.TypeInfoResolverChain.Insert(0, AutomationsJsonContext.Default));
        services.TryAddSingleton(TimeProvider.System);
        services.AddHttpContextAccessor();
        services.AddSingleton<AutomationEndpointsMapped>();

        // The automation store follows the session store: PostgreSQL when Sessions:Store is Postgres, so every replica shares one table; otherwise the file or memory store.
        // The PostgreSQL store is built in SelectAutomationStore only when Sessions:Store is Postgres, at resolution time, so file and memory hosts never need its data source.
        services.TryAddSingleton<IAutomationStore>(SelectAutomationStore);
        services.TryAddSingleton<IAutomationSecretStore>(static provider =>
        {
            var path = provider.GetRequiredService<IOptions<AutomationOptions>>().Value.SecretStorePath;
            return path.Length == 0 ? new InMemoryAutomationSecretStore() : new FileAutomationSecretStore(path);
        });
        services.TryAddSingleton(static provider => new WebhookTrigger(
            provider.GetRequiredService<TimeProvider>(),
            new WebhookTriggerOptions { ReplayWindow = TimeSpan.FromSeconds(provider.GetRequiredService<IOptions<AutomationOptions>>().Value.WebhookReplayWindowSeconds) }));
        services.TryAddSingleton<AutomationService>();
        services.TryAddSingleton<AutomationRunner>();
        services.TryAddSingleton<AutomationScheduler>();
        services.TryAddSingleton<AutomationWebhookReceiver>();

        // Targets: the cloud target always; a host that has a local target registers its own IAutomationTarget of kind Local.
        services.TryAddSingleton<ICloudAgentLauncher>(static provider =>
            provider.GetRequiredService<IOptions<CloudOptions>>().Value.Enabled
                ? new CloudServiceAgentLauncher(
                    provider.GetRequiredService<CloudAgentService>(),
                    provider.GetRequiredService<IOptions<Autonogrammer.Api.Options.ApiOptions>>(),
                    provider.GetRequiredService<TimeProvider>())
                : new UnavailableCloudAgentLauncher());
        services.AddSingleton<IAutomationTarget, CloudAutomationTarget>();

        services.AddHostedService<AutomationHostedService>();

        // The settings page answers GET /automations from the same store. Last registration wins over the "none configured" default.
        services.AddSingleton<IWorkspaceAutomationsProvider, HostedAutomationsSettingsProvider>();

        services.TryAddKeyedSingleton<IRateLimiterPolicy<string>, AutomationWebhookRateLimiterPolicy>(AutomationWebhookRateLimiterPolicy.PolicyName);
        services.TryAddKeyedSingleton<IRateLimiterPolicy<string>, AutomationApiRateLimiterPolicy>(AutomationApiRateLimiterPolicy.PolicyName);
        _ = services.AddOptions<RateLimiterOptions>().Configure<IServiceProvider>(static (options, provider) =>
        {
            options.AddPolicy(
                AutomationWebhookRateLimiterPolicy.PolicyName,
                provider.GetRequiredKeyedService<IRateLimiterPolicy<string>>(AutomationWebhookRateLimiterPolicy.PolicyName));
            options.AddPolicy(
                AutomationApiRateLimiterPolicy.PolicyName,
                provider.GetRequiredKeyedService<IRateLimiterPolicy<string>>(AutomationApiRateLimiterPolicy.PolicyName));
        });
        return services;
    }

    /// <summary>Selects the automation store: PostgreSQL when the session store is Postgres, otherwise the file store, or memory when no store path is configured.</summary>
    /// <param name="provider">The service provider.</param>
    /// <returns>The automation store.</returns>
    private static IAutomationStore SelectAutomationStore(IServiceProvider provider)
    {
        if (SessionsOptions.IsPostgresStore(provider.GetRequiredService<IOptions<SessionsOptions>>().Value.Store))
        {
            return new PostgresAutomationStore(
                provider.GetRequiredService<NpgsqlDataSource>(),
                provider.GetRequiredService<PostgresResilience>());
        }

        var path = provider.GetRequiredService<IOptions<AutomationOptions>>().Value.StorePath;
        return path.Length == 0 ? new InMemoryAutomationStore() : new FileAutomationStore(path);
    }
}
