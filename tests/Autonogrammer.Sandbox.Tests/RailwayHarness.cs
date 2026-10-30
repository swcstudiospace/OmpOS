namespace Autonogrammer.Sandbox.Tests;

/// <summary>
/// A <see cref="RailwayCloudEnvironmentClient"/> wired to recorded fixtures: a fake GraphQL endpoint, a scripted worker probe,
/// a clock whose delays complete at once, and a capturing logger. Also holds the canary secrets the tests look for.
/// </summary>
internal sealed class RailwayHarness : IDisposable
{
    /// <summary>The canary API token: it must never appear in a log or exception.</summary>
    public const string Token = "rw-canary-token-9f3a7c";

    /// <summary>The canary value of a non-secret variable: variable values are redacted too.</summary>
    public const string VariableValue = "canary-variable-value-4417";

    /// <summary>The canary value returned by the secret variables provider.</summary>
    public const string SecretValue = "canary-secret-value-8820";

    /// <summary>The project identifier used by the harness.</summary>
    public const string ProjectId = "proj-1";

    /// <summary>The environment identifier used by the harness.</summary>
    public const string EnvironmentId = "env-1";

    /// <summary>The service label of the session <c>s1</c>.</summary>
    public const string Label = "autonogrammer-agent-s1";

    private readonly HttpClient _apiClient;
    private readonly HttpClient _probeClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="RailwayHarness"/> class.
    /// </summary>
    /// <param name="configure">Optional changes to the default options.</param>
    public RailwayHarness(Action<RailwayCloudEnvironmentOptions>? configure = null)
    {
        Clock = new AutoAdvanceTimeProvider(new DateTimeOffset(2026, 10, 27, 8, 0, 0, TimeSpan.Zero));
        _apiClient = new HttpClient(Api, disposeHandler: false);
        _probeClient = new HttpClient(Probe, disposeHandler: false);
        var options = DefaultOptions();
        configure?.Invoke(options);
        Client = new RailwayCloudEnvironmentClient(_apiClient, _probeClient, options, Clock, Logger);
    }

    /// <summary>Gets the fake GraphQL endpoint.</summary>
    public RailwayFixtureHandler Api { get; } = new();

    /// <summary>Gets the scripted worker probe handler.</summary>
    public ScriptedProbeHandler Probe { get; } = new();

    /// <summary>Gets the clock.</summary>
    public AutoAdvanceTimeProvider Clock { get; }

    /// <summary>Gets the capturing logger.</summary>
    public CapturingLogger<RailwayCloudEnvironmentClient> Logger { get; } = new();

    /// <summary>Gets the client under test.</summary>
    public RailwayCloudEnvironmentClient Client { get; }

    /// <summary>Builds the default options carrying the canary secrets.</summary>
    /// <returns>New options.</returns>
    public static RailwayCloudEnvironmentOptions DefaultOptions() => new()
    {
        ApiToken = Token,
        ProjectId = ProjectId,
        EnvironmentId = EnvironmentId,
        Image = "ghcr.io/swcstudiospace/autonogrammer-agent-worker:test",
        Variables = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["AUTONOGRAMMER_MODE"] = "worker",
            ["AUTONOGRAMMER_CANARY"] = VariableValue,
        },
        SecretVariablesProvider = (_, _) => ValueTask.FromResult<IReadOnlyDictionary<string, string>>(
            new Dictionary<string, string>(StringComparer.Ordinal) { ["WORKER_PUBLIC_KEY"] = SecretValue }),
    };

    /// <summary>Builds a <c>projectServices</c> data object.</summary>
    /// <param name="services">The service id and name pairs.</param>
    /// <returns>The JSON of the data member.</returns>
    public static string Services(params (string Id, string Name)[] services) =>
        "{\"project\":{\"services\":{\"edges\":[" + string.Join(',', services.Select(s => "{\"node\":{\"id\":\"" + s.Id + "\",\"name\":\"" + s.Name + "\"}}")) + "]}}}";

    /// <summary>Builds a <c>projectVolumes</c> data object.</summary>
    /// <param name="volumes">The volume id and name pairs.</param>
    /// <returns>The JSON of the data member.</returns>
    public static string Volumes(params (string Id, string Name)[] volumes) =>
        "{\"project\":{\"volumes\":{\"edges\":[" + string.Join(',', volumes.Select(v => "{\"node\":{\"id\":\"" + v.Id + "\",\"name\":\"" + v.Name + "\"}}")) + "]}}}";

    /// <summary>Builds a <c>deployments</c> data object holding one deployment, or none.</summary>
    /// <param name="status">The status of the latest deployment, or null for no deployment.</param>
    /// <returns>The JSON of the data member.</returns>
    public static string Deployments(string? status) =>
        status is null
            ? "{\"deployments\":{\"edges\":[]}}"
            : "{\"deployments\":{\"edges\":[{\"node\":{\"id\":\"dep-1\",\"status\":\"" + status + "\",\"createdAt\":\"2026-10-27T07:00:00Z\"}}]}}";

    /// <summary>Registers the fixtures of a successful full container creation for the session <c>s1</c>.</summary>
    public void RegisterCreateFlow()
    {
        Api.Data("projectServices", Services());
        Api.Data("serviceCreate", "{\"serviceCreate\":{\"id\":\"svc-1\",\"name\":\"" + Label + "\"}}");
        Api.Data("variableCollectionUpsert", "{\"variableCollectionUpsert\":true}");
        Api.Data("serviceInstanceUpdate", "{\"serviceInstanceUpdate\":true}");
        Api.Data("serviceInstanceDeployV2", "{\"serviceInstanceDeployV2\":\"dep-2\"}");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _apiClient.Dispose();
        _probeClient.Dispose();
        Api.Dispose();
        Probe.Dispose();
    }
}
