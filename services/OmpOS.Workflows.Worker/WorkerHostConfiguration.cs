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
            EnableFakeActivities = string.Equals(section["EnableFakeActivities"], bool.TrueString, StringComparison.OrdinalIgnoreCase),
            Address = section["Address"] ?? WorkerOptions.DefaultAddress,
            Namespace = section["Namespace"] ?? WorkerOptions.DefaultNamespace,
            TaskQueue = section["TaskQueue"] ?? WorkerOptions.DefaultTaskQueue,
        };
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

    /// <summary>Registers the Temporal client and hosted session-run worker.</summary>
    /// <param name="services">The service collection to populate.</param>
    /// <param name="configuration">The application configuration.</param>
    internal static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ConfigureServices(services, ReadOptions(configuration));
    }

    /// <summary>Registers the Temporal client and hosted session-run worker.</summary>
    /// <param name="services">The service collection to populate.</param>
    /// <param name="options">The validated worker options.</param>
    internal static void ConfigureServices(IServiceCollection services, WorkerOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        if (!options.Enabled)
        {
            // The disabled path must exit before registration (Program.Main returns 0);
            // refusing here pins that guarantee structurally.
            throw new InvalidOperationException("WorkerHostConfiguration requires Workflows:Enabled=true. The disabled worker must exit before registering Temporal services.");
        }

        ValidateOptions(options);

        // Fake activities silently fabricate successful runs; they are opt-in only.
        // By default a fail-loud guard is registered so a misconfigured worker fails
        // on first use instead of reporting fabricated success.
        if (options.EnableFakeActivities)
        {
            services.AddSingleton<ISessionRunActivities>(new FakeSessionRunActivities());
        }
        else
        {
            services.AddSingleton<ISessionRunActivities>(new GuardedSessionRunActivities());
        }
        services.Configure<TemporalClientConnectOptions>(clientOptions =>
        {
            clientOptions.DataConverter = SessionRunDataConverter.Instance;
        });
        services.AddTemporalClient(options.Address, options.Namespace);
        services.AddHostedTemporalWorker(
                options.TaskQueue,
                new WorkerDeploymentOptions(new WorkerDeploymentVersion("ompos", BuildId), useWorkerVersioning: false))
            .AddWorkflow<TemporalSessionRunWorkflow>()
            .AddSingletonActivities<TemporalSessionRunActivities>();
    }
}
