using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Autonogrammer.Api.Cloud;
using Autonogrammer.Api.Coordination;
using Autonogrammer.Api.Metrics;
using Autonogrammer.Api.Options;
using Autonogrammer.Api.Persistence;
using Autonogrammer.Api.Security;
using Autonogrammer.Api.Serialization;
using Autonogrammer.Api.Services;
using Autonogrammer.Api.Sse;
using Autonogrammer.Api.Workflows;
using Autonogrammer.Contracts.Serialization;
using Autonogrammer.Contracts.Sessions;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Sandbox;
using Autonogrammer.Sandbox.Abstractions;
using Autonogrammer.SessionRuns;
using Autonogrammer.Sessions;
using Autonogrammer.Workflows;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shouldly;

namespace Autonogrammer.Api.IntegrationTests.Cloud;

/// <summary>
/// A host that is the API's session and run machinery plus the cloud control plane, on a test server. It registers the same services
/// <c>Program</c> registers for sessions and runs (the real session store, run leases, event streamer and prompt runner), the real cloud services
/// through <c>AddCloudAgentServices</c> and <c>MapCloudAgentEndpoints</c>, and replaces only the edges: the environment client, the worker (an
/// in-process <see cref="FakeCloudWorker"/>), GitHub (a <see cref="FakeGitHubHandler"/>), the clock and authentication (the owner is named by a header).
/// Nothing here depends on how <c>Program</c> wires the cloud services.
/// </summary>
internal sealed class CloudApiHost : IAsyncDisposable
{
    /// <summary>The default owner of <see cref="Client"/>.</summary>
    public const string Alice = "alice";

    /// <summary>Another owner, for isolation tests.</summary>
    public const string Bob = "bob";

    /// <summary>The first repository the tests use.</summary>
    public const string RepoUrl = "https://github.com/acme/widgets";

    private readonly WebApplication _app;

    private CloudApiHost(WebApplication app, CloudTestKeys keys, FakeCloudWorker worker, TestCloudEnvironment environment, FakeGitHubHandler github, ManualClock clock, CapturingLoggerProvider logs)
    {
        _app = app;
        Keys = keys;
        Worker = worker;
        Environment = environment;
        GitHub = github;
        Clock = clock;
        Logs = logs;
    }

    /// <summary>Gets the key pairs of the host.</summary>
    public CloudTestKeys Keys { get; }

    /// <summary>Gets the in-process worker.</summary>
    public FakeCloudWorker Worker { get; }

    /// <summary>Gets the environment client the control plane provisions through.</summary>
    public TestCloudEnvironment Environment { get; }

    /// <summary>Gets the fake GitHub.</summary>
    public FakeGitHubHandler GitHub { get; }

    /// <summary>Gets the clock every service reads.</summary>
    public ManualClock Clock { get; }

    /// <summary>Gets every log entry the host wrote.</summary>
    public CapturingLoggerProvider Logs { get; }

    /// <summary>Gets the host's services.</summary>
    public IServiceProvider Services => _app.Services;

    /// <summary>Starts a host with no git credential configured, unless <paramref name="config"/> carries a token.</summary>
    /// <param name="config">Configuration overrides under the <c>Cloud</c> section and elsewhere; they win over the defaults below.</param>
    /// <param name="cancellationToken">Cancels the start.</param>
    /// <returns>The running host.</returns>
    public static Task<CloudApiHost> StartAsync(Dictionary<string, string?>? config = null, CancellationToken cancellationToken = default) =>
        StartCoreAsync(config, githubApp: false, cancellationToken);

    /// <summary>Starts a host with a GitHub App configured (App id 123 and the host's generated RSA key).</summary>
    /// <param name="config">Configuration overrides under the <c>Cloud</c> section and elsewhere; they win over the defaults below.</param>
    /// <param name="cancellationToken">Cancels the start.</param>
    /// <returns>The running host.</returns>
    public static Task<CloudApiHost> StartWithGitHubAppAsync(Dictionary<string, string?>? config = null, CancellationToken cancellationToken = default) =>
        StartCoreAsync(config, githubApp: true, cancellationToken);

    private static async Task<CloudApiHost> StartCoreAsync(Dictionary<string, string?>? config, bool githubApp, CancellationToken cancellationToken)
    {
        var keys = new CloudTestKeys();
        var clock = new ManualClock(TimeProvider.System.GetUtcNow());
        var worker = new FakeCloudWorker(keys.WorkerVerificationKey, clock);
        var environment = new TestCloudEnvironment();
        var github = new FakeGitHubHandler(keys.AppVerificationKey, clock);
        var logs = new CapturingLoggerProvider();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        _ = builder.WebHost.UseTestServer();
        _ = builder.Configuration.AddInMemoryCollection(Defaults(keys, config, githubApp));
        _ = builder.Logging.ClearProviders().AddProvider(logs);
        Register(builder.Services, builder.Configuration, clock, environment, github, worker);
        var app = builder.Build();
        app.UseAuthentication();
        app.UseRateLimiter();
        app.UseAuthorization();
        _ = app.MapCloudAgentEndpoints();
        var host = new CloudApiHost(app, keys, worker, environment, github, clock, logs);
        environment.VariableSource = () => app.Services.GetRequiredService<ICloudWorkerVariableSource>();
        await app.StartAsync(cancellationToken).ConfigureAwait(false);
        return host;
    }

    /// <summary>Creates a client that acts as <paramref name="owner"/>.</summary>
    /// <param name="owner">The owner's name, the <c>sub</c> of the caller.</param>
    /// <returns>The client. The caller disposes it.</returns>
    public HttpClient Client(string owner = Alice)
    {
        var client = _app.GetTestClient();
        client.DefaultRequestHeaders.Add(TestOwnerAuthHandler.Header, owner);
        return client;
    }

    /// <summary>Creates a cloud agent as <paramref name="owner"/> and returns its response.</summary>
    /// <param name="body">The request body, or null for one repository.</param>
    /// <param name="owner">The caller.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The agent as created (provisioning).</returns>
    public async Task<JsonElement> CreateAgentAsync(string? body = null, string owner = Alice, CancellationToken cancellationToken = default)
    {
        using var client = Client(owner);
        using var response = await PostJsonAsync(client, "/cloud/agents", body ?? DefaultCreateBody(), cancellationToken).ConfigureAwait(false);
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted, await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
        return await ReadAsync(response, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Creates an agent and waits until it is running.</summary>
    /// <param name="body">The request body, or null for one repository.</param>
    /// <param name="owner">The caller.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>The running agent.</returns>
    public async Task<JsonElement> CreateRunningAgentAsync(string? body = null, string owner = Alice, CancellationToken cancellationToken = default)
    {
        var created = await CreateAgentAsync(body, owner, cancellationToken).ConfigureAwait(false);
        return await WaitForStatusAsync(created.GetProperty("id").GetString()!, "running", owner, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Polls an agent until it has the status, failing the test when it does not within fifteen seconds.</summary>
    /// <param name="id">The agent identifier.</param>
    /// <param name="status">The wire status.</param>
    /// <param name="owner">The caller.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>The agent with that status.</returns>
    public async Task<JsonElement> WaitForStatusAsync(string id, string status, string owner = Alice, CancellationToken cancellationToken = default)
    {
        using var client = Client(owner);
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(TimeSpan.FromSeconds(15));
        var last = string.Empty;
        while (true)
        {
            using var response = await client.GetAsync(new Uri($"/cloud/agents/{id}", UriKind.Relative), limit.Token).ConfigureAwait(false);
            var agent = await ReadAsync(response, limit.Token).ConfigureAwait(false);
            last = agent.GetProperty("status").GetString()!;
            if (string.Equals(last, status, StringComparison.Ordinal))
            {
                return agent;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(25), limit.Token).ConfigureAwait(false);
        }
    }

    /// <summary>Polls the session until it has the status.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="status">The session status.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>The session.</returns>
    public async Task<Session> WaitForSessionAsync(string sessionId, SessionStatus status, CancellationToken cancellationToken = default)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(TimeSpan.FromSeconds(15));
        var repository = Services.GetRequiredService<ISessionRepository>();
        Session? last = null;
        try
        {
            while (true)
            {
                last = await repository.GetAsync(sessionId, limit.Token).ConfigureAwait(false);
                if (last?.Status == status)
                {
                    return last;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(25), limit.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException exception) when (limit.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Session {sessionId} did not reach {status} in 15 seconds; it was {last?.Status.ToString() ?? "absent"}. Warnings: {string.Join(" | ", Logs.Entries.Where(static entry => entry.Level >= LogLevel.Warning).Select(static entry => entry.Category + ": " + entry.Message))}", exception);
        }
    }

    /// <summary>Sends a prompt to an agent and returns the response status.</summary>
    /// <param name="id">The agent identifier.</param>
    /// <param name="prompt">The prompt text.</param>
    /// <param name="owner">The caller.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The response; the caller disposes it.</returns>
    public async Task<HttpResponseMessage> PromptAsync(string id, string prompt, string owner = Alice, CancellationToken cancellationToken = default)
    {
        using var client = Client(owner);
        return await PostJsonAsync(client, $"/cloud/agents/{id}/prompts", new JsonObject { ["prompt"] = prompt }.ToJsonString(), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Sends a POST without a body to an agent route and returns the response.</summary>
    /// <param name="path">The route.</param>
    /// <param name="owner">The caller.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The response; the caller disposes it.</returns>
    public async Task<HttpResponseMessage> PostAsync(string path, string owner = Alice, CancellationToken cancellationToken = default)
    {
        using var client = Client(owner);
        return await client.PostAsync(new Uri(path, UriKind.Relative), content: null, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reads the agent's session from the store.</summary>
    /// <param name="agent">The agent JSON.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The session.</returns>
    public async Task<Session> SessionOfAsync(JsonElement agent, CancellationToken cancellationToken = default) =>
        await Services.GetRequiredService<ISessionRepository>().GetAsync(agent.GetProperty("sessionId").GetString()!, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The agent's session is missing.");

    /// <summary>Runs one sweep of the idle sweeper now.</summary>
    /// <param name="cancellationToken">Cancels the sweep.</param>
    /// <returns>What the sweep did.</returns>
    internal Task<CloudSweepResult> SweepAsync(CancellationToken cancellationToken = default) =>
        Services.GetRequiredService<CloudIdleSweeper>().SweepOnceAsync(cancellationToken);

    /// <summary>The body of a create request for <see cref="RepoUrl"/> with the given extras.</summary>
    /// <param name="extra">Members to add inside the object, as JSON text without the braces (for example <c>"idleMinutes":1</c>), or null.</param>
    /// <returns>The JSON.</returns>
    public static string DefaultCreateBody(string? extra = null) =>
        "{\"repositories\":[{\"url\":\"" + RepoUrl + "\"}]" + (extra is null ? string.Empty : "," + extra) + "}";

    /// <summary>Posts JSON.</summary>
    /// <param name="client">The client.</param>
    /// <param name="path">The route.</param>
    /// <param name="json">The body.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <param name="headers">Extra request headers.</param>
    /// <returns>The response; the caller disposes it.</returns>
    public static async Task<HttpResponseMessage> PostJsonAsync(HttpClient client, string path, string json, CancellationToken cancellationToken, params (string Name, string Value)[] headers)
    {
        ArgumentNullException.ThrowIfNull(client);
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(path, UriKind.Relative)) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        foreach (var (name, value) in headers)
        {
            request.Headers.Add(name, value);
        }

        return await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reads a response body as JSON.</summary>
    /// <param name="response">The response.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The root element, cloned so it outlives the document.</returns>
    public static async Task<JsonElement> ReadAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(response);
        var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        using var document = JsonDocument.Parse(text);
        return document.RootElement.Clone();
    }

    /// <summary>Reads the stable code of a problem response from its <c>type</c> member.</summary>
    /// <param name="response">The response.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The code, for example <c>agent_not_found</c>.</returns>
    public static async Task<string> ProblemCodeAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var problem = await ReadAsync(response, cancellationToken).ConfigureAwait(false);
        return problem.GetProperty("type").GetString()!.Replace("/problems/", string.Empty, StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync().ConfigureAwait(false);
        await _app.DisposeAsync().ConfigureAwait(false);
        await Worker.DisposeAsync().ConfigureAwait(false);
        Keys.Dispose();
    }

    private static Dictionary<string, string?> Defaults(CloudTestKeys keys, Dictionary<string, string?>? overrides, bool githubApp)
    {
        var values = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Cloud:Enabled"] = "true",
            ["Cloud:Environment:Provider"] = "External",
            ["Cloud:Worker:SigningKeyPem"] = keys.SigningKeyPem,
            ["Cloud:Git:ApiBase"] = "https://api.github.test",
            ["Sessions:Store"] = "InMemory",
            ["Coordination:Store"] = "InMemory",
        };
        if (githubApp)
        {
            values["Cloud:Git:App:AppId"] = "123";
            values["Cloud:Git:App:PrivateKeyPem"] = keys.AppKeyPem;
        }

        foreach (var (key, value) in overrides ?? [])
        {
            values[key] = value;
        }

        return values;
    }

    private static void Register(IServiceCollection services, IConfiguration configuration, ManualClock clock, TestCloudEnvironment environment, FakeGitHubHandler github, FakeCloudWorker worker)
    {
        services.AddSingleton<TimeProvider>(clock);
        services.ConfigureHttpJsonOptions(static options =>
        {
            options.SerializerOptions.TypeInfoResolverChain.Insert(0, ApiJsonContext.Default);
            options.SerializerOptions.TypeInfoResolverChain.Insert(0, AutonogrammerJsonContext.Default);
        });
        services.AddOptions<ApiOptions>().BindConfiguration(ApiOptions.SectionName);
        services.AddOptions<DebugOptions>().BindConfiguration(DebugOptions.SectionName);
        services.AddOptions<SessionsOptions>().BindConfiguration(SessionsOptions.SectionName);
        services.AddApiSessionStore(configuration);
        services.AddApiCoordination(configuration);
        services.AddSingleton<SessionService>();
        services.AddSingleton<SessionRegistry>();
        services.AddSingleton<IHarnessSessionFactory>(new LocalRunRefusedFactory());
        services.AddSingleton<PromptRunner>();
        services.AddSingleton<ISandboxProvider, FakeSandboxProvider>();
        services.AddSingleton<ISessionRunObserver, ApiSessionRunObserver>();
        services.AddSingleton<ISessionRunActivities, SessionRunActivities>();
        services.AddSingleton<ISessionRunWorkflow, DirectSessionRunner>();
        services.AddSingleton<DirectSessionRunDispatcher>();
        services.AddSingleton<SessionEventStreamer>();
        services.AddSingleton<SessionChangePublisher>();
        services.AddSingleton<SessionSignalSubscriber>();
        services.AddSingleton<IHostedService>(static provider => provider.GetRequiredService<SessionSignalSubscriber>());
        services.AddSingleton<ApiMetrics>();
        services.AddHttpContextAccessor();
        services.AddScoped<IUserContext>(static provider => new AuthenticatedUserContext(provider.GetRequiredService<IHttpContextAccessor>()));
        services.AddAuthentication(TestOwnerAuthHandler.SchemeName).AddScheme<AuthenticationSchemeOptions, TestOwnerAuthHandler>(TestOwnerAuthHandler.SchemeName, null);
        services.AddAuthorizationBuilder().SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
        services.AddRateLimiter(options =>
        {
            options.AddPolicy(SessionEndpointsPolicies.Prompts, static _ => System.Threading.RateLimiting.RateLimitPartition.GetNoLimiter("all"));
            options.AddPolicy(SessionEndpointsPolicies.Creation, static _ => System.Threading.RateLimiting.RateLimitPartition.GetNoLimiter("all"));
        });

        // The cloud control plane, then the edges replaced by the test.
        services.AddCloudAgentServices();

        // The sweeper's own loop sweeps once at start and then on a timer; tests drive sweeps by hand (SweepAsync) so the first orphan scan and every idle
        // decision happen when the test asks, not in a background race. Its registration is the only factory-built hosted service the cloud services add.
        var sweeperLoop = services.Single(static descriptor => descriptor.ServiceType == typeof(IHostedService) && descriptor.ImplementationFactory is not null
            && descriptor.ServiceKey is null && descriptor.Lifetime == ServiceLifetime.Singleton
            && !descriptor.ImplementationFactory.Method.DeclaringType!.FullName!.Contains("CloudApiHost", StringComparison.Ordinal)
            && descriptor.ImplementationFactory.Method.DeclaringType.FullName!.Contains("CloudServiceRegistration", StringComparison.Ordinal));
        _ = services.Remove(sweeperLoop);
        services.AddSingleton<ICloudEnvironmentClient>(environment);
        services.AddSingleton<ICloudWorkerCatalog>(environment);
        services.AddSingleton<ICloudWorkerEndpoints>(new FixedWorkerEndpoints(worker.Endpoint));
        services.AddHttpClient("autonogrammer-cloud-github").ConfigurePrimaryHttpMessageHandler(() => github);
    }

    /// <summary>The rate limiter policy names the cloud routes require; the test host registers them without a limit.</summary>
    private static class SessionEndpointsPolicies
    {
        public const string Prompts = Autonogrammer.Api.Endpoints.SessionEndpoints.PromptsPolicy;

        public const string Creation = Autonogrammer.Api.Endpoints.SessionEndpoints.CreationPolicy;
    }

    /// <summary>Points every worker at the in-process fake.</summary>
    private sealed class FixedWorkerEndpoints(Uri endpoint) : ICloudWorkerEndpoints
    {
        /// <inheritdoc />
        public Uri Resolve(CloudContainer container) => endpoint;
    }
}
