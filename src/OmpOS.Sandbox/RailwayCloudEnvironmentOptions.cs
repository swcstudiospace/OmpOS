namespace OmpOS.Sandbox;

/// <summary>
/// Settings for <see cref="RailwayCloudEnvironmentClient"/>. <see cref="ApiToken"/> and every variable value are secrets:
/// they are never logged and are redacted from every exception message.
/// </summary>
public sealed class RailwayCloudEnvironmentOptions
{
    /// <summary>Gets or sets the Railway GraphQL endpoint.</summary>
    public Uri Endpoint { get; set; } = new("https://backboard.railway.com/graphql/v2");

    /// <summary>Gets or sets the Railway API token sent as the bearer credential.</summary>
    public string ApiToken { get; set; } = string.Empty;

    /// <summary>Gets or sets the Railway project that holds the worker services.</summary>
    public string ProjectId { get; set; } = string.Empty;

    /// <summary>Gets or sets the Railway environment the worker services run in.</summary>
    public string EnvironmentId { get; set; } = string.Empty;

    /// <summary>Gets or sets the worker image, for example <c>ghcr.io/swcstudiospace/ompos-agent-worker:&lt;tag&gt;</c>.</summary>
    public string Image { get; set; } = string.Empty;

    /// <summary>Gets or sets the prefix of every service name; the session label is this prefix plus the sanitised session id.</summary>
    public string ServiceNamePrefix { get; set; } = "ompos-agent-";

    /// <summary>Gets or sets the non-secret service variables applied to every worker service.</summary>
    public IReadOnlyDictionary<string, string> Variables { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets the provider of per-session secret variable values (for example the worker public key). The argument is
    /// the session label. The returned values are never logged and are redacted from exception messages.
    /// </summary>
    public Func<string, CancellationToken, ValueTask<IReadOnlyDictionary<string, string>>>? SecretVariablesProvider { get; set; }

    /// <summary>Gets or sets the path inside the worker where the session volume is mounted.</summary>
    public string VolumeMountPath { get; set; } = "/data";

    /// <summary>Gets or sets the worker's anonymous readiness path, the wake target.</summary>
    public string HealthPath { get; set; } = "/healthz";

    /// <summary>Gets or sets the port the worker listens on.</summary>
    public int WorkerPort { get; set; } = 8080;

    /// <summary>Gets or sets the private-network host suffix; the worker base URI is <c>http://{serviceName}{suffix}:{port}/</c>.</summary>
    public string WorkerHostSuffix { get; set; } = ".railway.internal";

    /// <summary>Gets or sets how long <see cref="RailwayCloudEnvironmentClient.EnsureAwakeAsync"/> retries the readiness probe.</summary>
    public TimeSpan WakeTimeout { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>Gets or sets the pause between readiness probes.</summary>
    public TimeSpan WakeRetryInterval { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>Gets or sets a value indicating whether services are created with serverless sleep (<c>sleepApplication</c>) enabled.</summary>
    public bool EnableServerless { get; set; } = true;

    /// <summary>Gets or sets the health check path set on the service instance; empty means <see cref="HealthPath"/>.</summary>
    public string HealthcheckPath { get; set; } = string.Empty;
}
