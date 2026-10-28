using System.Globalization;
using Microsoft.Extensions.Options;
using Autonogrammer.Api.Cloud.Options;
using Autonogrammer.Sandbox;

namespace Autonogrammer.Api.Cloud;

/// <summary>
/// Builds the <see cref="ICloudEnvironmentClient"/> that <c>Cloud:Environment:Provider</c> names. The Railway client is given the worker's static
/// settings (the public keys it verifies tokens with, the issuer, the workspace root) and a variable provider that supplies each worker's secrets and
/// checkout instructions at creation (<see cref="ICloudWorkerVariableSource"/>). A provider of <c>External</c> means the host registered its own client;
/// asking for one that was not registered is an error that names the key.
/// </summary>
internal static class CloudEnvironmentRegistration
{
    /// <summary>The name of the HTTP client for the Railway GraphQL API.</summary>
    public const string RailwayHttpClient = "autonogrammer-cloud-railway";

    /// <summary>The name of the HTTP client that probes worker health.</summary>
    public const string ProbeHttpClient = "autonogrammer-cloud-worker-probe";

    /// <summary>Creates the client.</summary>
    /// <param name="services">The service provider.</param>
    /// <returns>The environment client; while cloud is off, a client that refuses every call.</returns>
    /// <exception cref="InvalidOperationException">The provider is not configured, or is <c>External</c> and no client was registered.</exception>
    public static ICloudEnvironmentClient Create(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        var options = services.GetRequiredService<IOptions<CloudOptions>>().Value;
        if (!options.Enabled)
        {
            return new DisabledCloudEnvironmentClient();
        }

        var environment = options.Environment;
        var factory = services.GetRequiredService<IHttpClientFactory>();
        var time = services.GetRequiredService<TimeProvider>();
        if (string.Equals(environment.Provider, CloudOptions.EnvironmentSettings.RailwayProvider, StringComparison.OrdinalIgnoreCase))
        {
            var variables = services.GetRequiredService<ICloudWorkerVariableSource>();
            return new RailwayCloudEnvironmentClient(
                factory.CreateClient(RailwayHttpClient),
                factory.CreateClient(ProbeHttpClient),
                RailwayOptions(options, variables),
                time,
                services.GetService<ILogger<RailwayCloudEnvironmentClient>>());
        }

        if (string.Equals(environment.Provider, CloudOptions.EnvironmentSettings.StaticPoolProvider, StringComparison.OrdinalIgnoreCase))
        {
            return new StaticPoolEnvironmentClient(
                factory.CreateClient(ProbeHttpClient),
                new StaticPoolEnvironmentOptions
                {
                    Workers = [.. environment.StaticPool.Workers.Select(static worker => new StaticPoolWorker(worker.WorkerId, new Uri(worker.BaseUri), worker.VolumeMountPath))],
                },
                time);
        }

        throw new InvalidOperationException(
            "Cloud:Environment:Provider must be Railway or StaticPool, or the host must register an ICloudEnvironmentClient when it is External.");
    }

    private static RailwayCloudEnvironmentOptions RailwayOptions(CloudOptions options, ICloudWorkerVariableSource variables)
    {
        var railway = options.Environment.Railway;
        var fixedVariables = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Worker__Auth__Issuer"] = options.Worker.Issuer,
            ["Worker__WorkspaceRoot"] = options.Worker.WorkspaceRoot,
        };
        for (var index = 0; index < railway.WorkerPublicKeysPem.Count; index++)
        {
            fixedVariables[string.Create(CultureInfo.InvariantCulture, $"Worker__Auth__PublicKeysPem__{index}")] = railway.WorkerPublicKeysPem[index];
        }

        var result = new RailwayCloudEnvironmentOptions
        {
            ApiToken = railway.ApiToken,
            ProjectId = railway.ProjectId,
            EnvironmentId = railway.EnvironmentId,
            Image = railway.Image,
            Variables = fixedVariables,
            SecretVariablesProvider = (sessionId, cancellationToken) => variables.ResolveAsync(sessionId, cancellationToken),
            VolumeMountPath = options.Environment.VolumeMountPath,
        };
        if (!string.IsNullOrWhiteSpace(railway.Endpoint))
        {
            result.Endpoint = new Uri(railway.Endpoint);
        }

        return result;
    }
}
