using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Autonogrammer.Api.Cloud.Options;
using Autonogrammer.Api.Options;
using Autonogrammer.Contracts.Cloud;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Sandbox;
using Autonogrammer.Sessions.Postgres;

namespace Autonogrammer.Api.Cloud;

/// <summary>Registers the cloud agent control plane. Cloud is off by default: with <c>Cloud:Enabled</c> false the services are registered but no route is mapped and the background services do nothing.</summary>
internal static class CloudServiceRegistration
{
    /// <summary>
    /// Registers the control plane's services, binds and validates <c>Cloud</c>, adds the cloud JSON context to the HTTP JSON chain, and routes the runs of
    /// cloud agents to their workers by wrapping the registered <see cref="IHarnessSessionFactory"/>. Call it once, after the harness factory is registered
    /// (after <c>AddSessionWebServices</c>). A host that registers its own <see cref="ICloudEnvironmentClient"/> registers it before this call, or sets
    /// <c>Cloud:Environment:Provider</c> to <c>External</c> and registers it afterwards.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddCloudAgentServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (services.Any(static descriptor => descriptor.ServiceType == typeof(CloudEndpointsMapped)))
        {
            return services;
        }

        _ = services.AddOptions<CloudOptions>().BindConfiguration(CloudOptions.SectionName).ValidateOnStart();
        services.AddSingleton<IValidateOptions<CloudOptions>, CloudOptionsValidator>();
        _ = services.ConfigureHttpJsonOptions(static options => options.SerializerOptions.TypeInfoResolverChain.Insert(0, CloudJsonContext.Default));
        services.TryAddSingleton(TimeProvider.System);
        services.AddHttpClient();
        services.AddSingleton<CloudEndpointsMapped>();

        // Durable records: Postgres when Sessions:Store is Postgres, so every replica shares one table; otherwise in process.
        services.AddSingleton<InMemoryCloudAgentStore>();
        services.AddSingleton<PostgresCloudAgentStore>();
        services.AddSingleton<ICloudAgentStore>(static provider =>
            SessionsOptions.IsPostgresStore(provider.GetRequiredService<IOptions<SessionsOptions>>().Value.Store)
                ? provider.GetRequiredService<PostgresCloudAgentStore>()
                : provider.GetRequiredService<InMemoryCloudAgentStore>());
        services.AddHostedService<CloudAgentStoreStartup>();

        // Secrets: write-only owner store first, operator-defined options second.
        services.AddSingleton<InMemoryCloudSecretStore>();
        services.AddSingleton<ICloudSecretStore>(static provider => provider.GetRequiredService<InMemoryCloudSecretStore>());
        services.AddSingleton<ICloudSecretProvider>(static provider => provider.GetRequiredService<InMemoryCloudSecretStore>());
        services.AddSingleton<ICloudSecretProvider, OptionsCloudSecretProvider>();
        services.AddSingleton<CloudSecretResolver>();
        services.AddSingleton<CloudRedactorFactory>();

        AddCloudGit(services);
        AddCloudWorkers(services);
        AddCloudControlPlane(services);

        return services.AddCloudHarnessRouting();
    }

    /// <summary>
    /// Wraps the currently registered <see cref="IHarnessSessionFactory"/> in a <see cref="CloudWorkerHarnessSessionFactory"/>. Call it once per factory
    /// registration, after that registration: a test that replaces the factory after <see cref="AddCloudAgentServices"/> calls it again.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddCloudHarnessRouting(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        var inner = services.LastOrDefault(static descriptor => descriptor.ServiceType == typeof(IHarnessSessionFactory) && !descriptor.IsKeyedService)
            ?? throw new InvalidOperationException("Register the IHarnessSessionFactory before AddCloudAgentServices.");
        _ = services.Remove(inner);
        services.Add(ServiceDescriptor.Describe(
            typeof(IHarnessSessionFactory),
            provider => new CloudWorkerHarnessSessionFactory(Create(provider, inner), provider),
            inner.Lifetime));
        return services;
    }

    private static void AddCloudGit(IServiceCollection services)
    {
        services.AddSingleton<InstallationTokenCache>();
        services.AddSingleton<GitHubAppClient>(static provider => new GitHubAppClient(
            provider.GetRequiredService<IHttpClientFactory>().CreateClient("autonogrammer-cloud-github"),
            provider.GetRequiredService<IOptionsMonitor<CloudOptions>>(),
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<InstallationTokenCache>()));
        services.AddSingleton<PatCredentialSource>();
        services.AddSingleton<IGitCredentialSource>(static provider =>
        {
            var app = provider.GetRequiredService<GitHubAppClient>();
            var pat = provider.GetRequiredService<PatCredentialSource>();
            if (app.IsConfigured)
            {
                return app;
            }

            return pat.IsConfigured ? (IGitCredentialSource)pat : new NotConfiguredCredentialSource();
        });
        services.AddSingleton<IGitHostClient>(static provider => new GitHubHostClient(
            provider.GetRequiredService<IHttpClientFactory>().CreateClient("autonogrammer-cloud-github"),
            provider.GetRequiredService<IGitCredentialSource>(),
            provider.GetRequiredService<IOptionsMonitor<CloudOptions>>()));
        services.AddSingleton<PullRequestService>();
    }

    private static void AddCloudWorkers(IServiceCollection services)
    {
        services.AddSingleton<WorkerTokenIssuer>();
        services.TryAddSingleton<ICloudWorkerEndpoints, WorkerEndpoints>();
        services.TryAddSingleton<ICloudEnvironmentClient>(static provider => CloudEnvironmentRegistration.Create(provider));
        services.TryAddSingleton<ICloudWorkerCatalog, EnvironmentClientWorkerCatalog>();
        services.AddSingleton<ICloudWorkerVariableSource, CloudWorkerVariables>();
    }

    private static void AddCloudControlPlane(IServiceCollection services)
    {
        services.AddSingleton<CloudMetrics>();
        services.AddSingleton<CloudBackgroundTasks>();
        services.AddSingleton<CloudRunTracker>();
        services.AddSingleton<CloudAgentLifecycle>();
        services.AddSingleton<CloudAgentRuntime>();
        services.AddSingleton<CloudCreateRequestValidator>();
        services.AddSingleton<CloudAgentService>();
        services.AddSingleton<CloudOrphanScanner>();
        services.AddSingleton<CloudIdleSweeper>();
        services.AddSingleton<IHostedService>(static provider => provider.GetRequiredService<CloudIdleSweeper>());
    }

    private static IHarnessSessionFactory Create(IServiceProvider provider, ServiceDescriptor descriptor)
    {
        if (descriptor.ImplementationInstance is IHarnessSessionFactory instance)
        {
            return instance;
        }

        if (descriptor.ImplementationFactory is { } factory)
        {
            return (IHarnessSessionFactory)factory(provider);
        }

        return (IHarnessSessionFactory)ActivatorUtilities.CreateInstance(provider, descriptor.ImplementationType!);
    }
}
