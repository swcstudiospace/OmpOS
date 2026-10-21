using OmpOS.Harness.Abstractions;
using OmpOS.Harness.Rpc;
using OmpOS.Sandbox;
using OmpOS.Sandbox.Abstractions;
using Temporalio.Client;
using Temporalio.Common;
using Temporalio.Extensions.Hosting;
using Temporalio.Worker;

namespace OmpOS.Workflows.Worker;

/// <summary>
/// Reads <see cref="WorkerOptions"/> and registers the Temporal worker services.
/// </summary>
/// <remarks>
/// Options are read through the section indexer with manual parsing only, never
/// reflection binding, so the worker stays trim/AOT safe.
/// Validation fails fast with a message naming the missing variable.
/// </remarks>
internal static class WorkerHostConfiguration
{
    /// <summary>The configuration section carrying the worker options.</summary>
    internal const string SectionName = "Workflows";

    /// <summary>The worker build ID registered with Temporal (set at registration, never later).</summary>
    internal const string BuildId = "ompos-workflows-v1";

    /// <summary>The configuration key that names the sandbox provider the host must register.</summary>
    internal const string SandboxProviderKey = "Sandbox:Provider";

    /// <summary>The configuration key that selects the harness adapter whose session factory the host must register.</summary>
    internal const string HarnessAdapterKey = "Harness:Adapter";

    /// <summary>The only harness adapter the worker composes: the omp RPC client.</summary>
    private const string RpcAdapter = "rpc";

    /// <summary>The configuration key naming the omp executable the rpc adapter starts.</summary>
    private const string OmpPathKey = "Harness:OmpPath";

    /// <summary>The configuration key naming the working directory the rpc adapter runs omp in.</summary>
    private const string CwdKey = "Harness:Cwd";

    /// <summary>The only sandbox provider the worker host composes.</summary>
    private const string PodmanProvider = "podman";

    /// <summary>Determines whether the Temporal worker is enabled.</summary>
    /// <param name="configuration">The application configuration.</param>
    /// <returns><see langword="true"/> when <c>Workflows:Enabled</c> is set.</returns>
    internal static bool IsEnabled(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return ReadOptions(configuration).Enabled;
    }

    /// <summary>Reads the worker options from configuration.</summary>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The populated options.</returns>
    internal static WorkerOptions ReadOptions(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var section = configuration.GetSection(SectionName);
        return new WorkerOptions
        {
            Enabled = string.Equals(section["Enabled"], bool.TrueString, StringComparison.OrdinalIgnoreCase),
            Address = section["Address"] ?? WorkerOptions.DefaultAddress,
            Namespace = section["Namespace"] ?? WorkerOptions.DefaultNamespace,
            TaskQueue = section["TaskQueue"] ?? WorkerOptions.DefaultTaskQueue,
            ApiKey = NormalizeApiKey(section["ApiKey"]),
            Tls = string.Equals(section["Tls"], bool.TrueString, StringComparison.OrdinalIgnoreCase),
        };
    }

    /// <summary>Normalizes the optional API key; blank values count as absent.</summary>
    /// <param name="value">The raw configured value.</param>
    /// <returns>The trimmed key, or <see langword="null"/> when absent or blank.</returns>
    private static string? NormalizeApiKey(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    /// <summary>Validates the worker options, failing fast on missing values.</summary>
    /// <param name="options">The options to validate.</param>
    /// <exception cref="InvalidOperationException">Thrown when enabled with an empty address.</exception>
    internal static void ValidateOptions(WorkerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Enabled && string.IsNullOrWhiteSpace(options.Address))
        {
            throw new InvalidOperationException(
                "Workflows:Address is required when Workflows:Enabled is true.");
        }
    }

    /// <summary>
    /// Selects the sandbox provider and harness adapter from configuration, then registers the Temporal client and
    /// hosted session-run worker.
    /// </summary>
    /// <param name="services">The service collection to populate.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the worker is disabled, when <c>Sandbox:Provider</c> is missing or unsupported, or when
    /// <c>Harness:Adapter</c> is missing or unsupported, or when <c>Harness:OmpPath</c> or <c>Harness:Cwd</c> is missing
    /// for the <c>rpc</c> adapter.
    /// </exception>
    internal static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        var options = ReadOptions(configuration);
        RequireEnabled(options);
        RegisterSandboxProvider(services, configuration[SandboxProviderKey]);
        RegisterHarnessAdapter(services, configuration);
        ConfigureServices(services, options);
    }

    /// <summary>Registers the Temporal client and hosted session-run worker.</summary>
    /// <param name="services">The service collection to populate.</param>
    /// <param name="options">The validated worker options.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the worker is disabled, the address is empty, or no sandbox provider or harness session factory is
    /// registered.
    /// </exception>
    internal static void ConfigureServices(IServiceCollection services, WorkerOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        RequireEnabled(options);
        ValidateOptions(options);

        // The composed activities need the host to register a sandbox provider and a harness
        // session factory. A missing registration fails startup; no stand-in activities are registered.
        RequireRegistration<ISandboxProvider>(services, SandboxProviderKey, "a sandbox provider");
        RequireRegistration<IHarnessSessionFactory>(services, HarnessAdapterKey, "a harness session factory");
        services.AddSingleton<ISessionRunActivities, ComposedSessionRunActivities>();
        services.AddTemporalClient(clientOptions =>
        {
            clientOptions.TargetHost = options.Address;
            clientOptions.Namespace = options.Namespace;
            clientOptions.DataConverter = SessionRunDataConverter.Instance;
            if (options.ApiKey is { } apiKey)
            {
                clientOptions.ApiKey = apiKey;
            }

            if (options.Tls)
            {
                clientOptions.Tls = new TlsOptions();
            }
        });
        services.AddHostedTemporalWorker(
                options.TaskQueue,
                new WorkerDeploymentOptions(new WorkerDeploymentVersion("ompos", BuildId), useWorkerVersioning: false))
            .AddWorkflow<TemporalSessionRunWorkflow>()
            .AddSingletonActivities<TemporalSessionRunActivities>();
    }

    /// <summary>Registers the sandbox provider named by <c>Sandbox:Provider</c>.</summary>
    /// <remarks>
    /// Only <c>podman</c> is composed. OpenShell, agentOS and Railway are not composed: their status is partial and
    /// unverified. The provider takes its options from <see cref="SandboxProviderOptions"/> defaults, as the provider's
    /// own tests construct it.
    /// </remarks>
    /// <param name="services">The service collection to populate.</param>
    /// <param name="provider">The raw configured provider name.</param>
    /// <exception cref="InvalidOperationException">Thrown when the value is missing or names an unsupported provider.</exception>
    internal static void RegisterSandboxProvider(IServiceCollection services, string? provider)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (string.IsNullOrWhiteSpace(provider))
        {
            throw new InvalidOperationException(
                $"{SandboxProviderKey} is required when Workflows:Enabled is true. Set {SandboxProviderKey} to '{PodmanProvider}'.");
        }

        if (!string.Equals(provider, PodmanProvider, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Unsupported {SandboxProviderKey} '{provider}'. The worker host supports only '{PodmanProvider}'.");
        }

        services.AddOptions<SandboxProviderOptions>();
        services.AddSandboxRunner();
        services.AddSingleton<ISandboxProvider, LocalPodmanComposeProvider>();
    }

    /// <summary>Registers the harness session factory selected by <c>Harness:Adapter</c>.</summary>
    /// <remarks>
    /// Only <c>rpc</c> is composed. It registers an <see cref="RpcHarnessSessionFactory"/> over an <see cref="OmpRpcClient"/>
    /// that starts omp from <c>Harness:OmpPath</c> in the working directory <c>Harness:Cwd</c>. The client enforces the omp
    /// version pin on every session start. The in-memory scripted harness is never composed in the worker.
    /// </remarks>
    /// <param name="services">The service collection to populate.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <c>Harness:Adapter</c> is missing or names a value other than <c>rpc</c>, or when the rpc adapter's
    /// <c>Harness:OmpPath</c> or <c>Harness:Cwd</c> is missing or blank.
    /// </exception>
    internal static void RegisterHarnessAdapter(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        string? adapter = configuration[HarnessAdapterKey];
        if (string.IsNullOrWhiteSpace(adapter))
        {
            throw new InvalidOperationException(
                $"{HarnessAdapterKey} is required when Workflows:Enabled is true. Set {HarnessAdapterKey} to '{RpcAdapter}'.");
        }

        if (!string.Equals(adapter, RpcAdapter, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Unsupported {HarnessAdapterKey} '{adapter}'. The worker host supports only '{RpcAdapter}'.");
        }

        var client = new OmpRpcClient(new OmpRpcClientOptions
        {
            OmpPath = RequireRpcSetting(configuration, OmpPathKey),
            Cwd = RequireRpcSetting(configuration, CwdKey),
        });
        services.AddSingleton<IHarnessSessionFactory>(new RpcHarnessSessionFactory(client));
    }

    /// <summary>Reads a setting the rpc adapter needs, failing when it is missing or blank.</summary>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="key">The configuration key to read.</param>
    /// <returns>The trimmed value.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the value is missing or blank.</exception>
    private static string RequireRpcSetting(IConfiguration configuration, string key)
    {
        string? value = configuration[key]?.Trim();
        return string.IsNullOrEmpty(value)
            ? throw new InvalidOperationException($"{key} is required when {HarnessAdapterKey} is '{RpcAdapter}'.")
            : value;
    }

    /// <summary>Refuses to register anything when the worker is disabled.</summary>
    /// <param name="options">The options to check.</param>
    /// <exception cref="InvalidOperationException">Thrown when <c>Workflows:Enabled</c> is not <see langword="true"/>.</exception>
    private static void RequireEnabled(WorkerOptions options)
    {
        if (!options.Enabled)
        {
            // The disabled path must exit before registration (Program.Main returns 0);
            // refusing here pins that guarantee structurally.
            throw new InvalidOperationException("WorkerHostConfiguration requires Workflows:Enabled=true. The disabled worker must exit before registering Temporal services.");
        }
    }

    /// <summary>Fails startup when a dependency the composed activities need is not registered.</summary>
    /// <typeparam name="TService">The required service type.</typeparam>
    /// <param name="services">The service collection to inspect.</param>
    /// <param name="configurationKey">The configuration key that names the missing dependency.</param>
    /// <param name="description">The dependency the host must register.</param>
    /// <exception cref="InvalidOperationException">Thrown when no registration for <typeparamref name="TService"/> exists.</exception>
    private static void RequireRegistration<TService>(IServiceCollection services, string configurationKey, string description)
    {
        if (services.Any(static descriptor => descriptor.ServiceType == typeof(TService)))
        {
            return;
        }

        throw new InvalidOperationException(
            $"{typeof(TService).Name} is not registered, so the session-run worker cannot start. Register {description} in the host (configuration key: {configurationKey}).");
    }
}
