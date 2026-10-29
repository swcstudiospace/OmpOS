using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Autonogrammer.Api.Services;
using Autonogrammer.Contracts.Sessions;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Harness.InMemory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;

namespace Autonogrammer.Api.IntegrationTests;

/// <summary>Shared builders for the web-app endpoint tests: hosts with a tracked harness, principals, JSON reads and a run held at a tool approval.</summary>
internal static class WebAppTestHost
{
    /// <summary>The wait for a held run to reach its approval.</summary>
    public static readonly TimeSpan SettleTimeout = TimeSpan.FromSeconds(15);

    /// <summary>The tool name the held run asks to use.</summary>
    public const string HeldToolName = "Bash";

    /// <summary>Gets a script whose run pauses at a tool approval that no decision answers until the test decides it.</summary>
    /// <returns>The script.</returns>
    public static InMemoryScript HeldAtApproval() => new()
    {
        Steps =
        [
            new EmitStep(new PermissionRequestEvent("call-held", HeldToolName, "{}")),
            new EmitStep(new CompletedEvent("end_turn")),
        ],
    };

    /// <summary>Creates a Development host (development principal on) whose harness replays <paramref name="script"/> and is tracked.</summary>
    /// <param name="script">The script every prompt replays, or null for the default echo harness.</param>
    /// <param name="config">Configuration overrides.</param>
    /// <param name="more">Further service replacements.</param>
    /// <param name="logger">An optional capturing log provider.</param>
    /// <returns>The host factory.</returns>
    public static ApiFactory Dev(InMemoryScript? script = null, Dictionary<string, string?>? config = null, Action<IServiceCollection>? more = null, ILoggerProvider? logger = null) =>
        new(config, services => Configure(services, script, more), logger);

    /// <summary>Creates a host that validates bearer tokens, with the same harness setup as <see cref="Dev"/>.</summary>
    /// <param name="script">The script every prompt replays, or null for the default echo harness.</param>
    /// <param name="config">Extra configuration overrides.</param>
    /// <param name="more">Further service replacements.</param>
    /// <param name="logger">An optional capturing log provider.</param>
    /// <returns>The host factory.</returns>
    public static ApiFactory Jwt(InMemoryScript? script = null, Dictionary<string, string?>? config = null, Action<IServiceCollection>? more = null, ILoggerProvider? logger = null)
    {
        var merged = JwtTestHost.DevelopmentConfig();
        if (config is not null)
        {
            foreach (var (key, value) in config)
            {
                merged[key] = value;
            }
        }

        return new ApiFactory(
            merged,
            services =>
            {
                JwtTestHost.InjectStaticMetadata(services);
                Configure(services, script, more);
            },
            logger);
    }

    /// <summary>Creates a client that sends a bearer token for <paramref name="subject"/>.</summary>
    /// <param name="factory">The host.</param>
    /// <param name="subject">The token subject.</param>
    /// <returns>The client.</returns>
    public static HttpClient AsPrincipal(ApiFactory factory, string subject)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTestHost.IssueToken(subject));
        return client;
    }

    /// <summary>Creates a JSON request body.</summary>
    /// <param name="json">The JSON text.</param>
    /// <returns>The content.</returns>
    public static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");

    /// <summary>Reads a response body as a JSON document.</summary>
    /// <param name="response">The response.</param>
    /// <param name="ct">The test cancellation token.</param>
    /// <returns>The parsed body.</returns>
    public static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(response);
        var text = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        return JsonDocument.Parse(text);
    }

    /// <summary>Sends a request with a relative address.</summary>
    /// <param name="client">The client.</param>
    /// <param name="method">The HTTP method.</param>
    /// <param name="path">The relative path.</param>
    /// <param name="body">The JSON body, or null for none.</param>
    /// <param name="ct">The test cancellation token.</param>
    /// <returns>The response.</returns>
    public static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string path, string? body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(client);
        using var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative));
        if (body is not null)
        {
            request.Content = Json(body);
        }

        return await client.SendAsync(request, ct).ConfigureAwait(false);
    }

    /// <summary>Asserts a problem response: its status and the stable code in its <c>type</c>.</summary>
    /// <param name="response">The response.</param>
    /// <param name="status">The expected status.</param>
    /// <param name="code">The expected code, or null to accept any.</param>
    /// <param name="ct">The test cancellation token.</param>
    /// <returns>A task that completes after the assertions.</returns>
    public static async Task ShouldBeProblemAsync(HttpResponseMessage response, HttpStatusCode status, string? code, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(response);
        response.StatusCode.ShouldBe(status);
        if (code is null)
        {
            return;
        }

        using var document = await ReadJsonAsync(response, ct).ConfigureAwait(false);
        document.RootElement.GetProperty("type").GetString().ShouldBe($"/problems/{code}");
    }

    /// <summary>Creates a session, submits a prompt, and waits until the run is held at its tool approval.</summary>
    /// <param name="client">The client.</param>
    /// <param name="prompt">The prompt text.</param>
    /// <param name="ct">The test cancellation token.</param>
    /// <returns>The session identifier.</returns>
    public static async Task<string> StartHeldRunAsync(HttpClient client, string prompt, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(client);
        var id = await SessionClient.CreateSessionAsync(client, ct).ConfigureAwait(false);
        using var accepted = await SessionClient.PostPromptAsync(client, id, $"{{\"prompt\":\"{prompt}\"}}", ct).ConfigureAwait(false);
        accepted.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        _ = await SessionClient.WaitForStatusAsync(client, id, SessionStatus.AwaitingToolApproval, SettleTimeout, ct).ConfigureAwait(false);
        return id;
    }

    /// <summary>Denies every pending approval of a held run so the run settles.</summary>
    /// <param name="client">The client.</param>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="ct">The test cancellation token.</param>
    /// <returns>A task that completes when the run has settled.</returns>
    public static async Task ReleaseHeldRunAsync(HttpClient client, string sessionId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(client);
        using var pending = await client.GetAsync(new Uri($"/sessions/{sessionId}/tool-approvals", UriKind.Relative), ct).ConfigureAwait(false);
        using var document = await ReadJsonAsync(pending, ct).ConfigureAwait(false);
        foreach (var approval in document.RootElement.GetProperty("approvals").EnumerateArray())
        {
            var requestId = approval.GetProperty("requestId").GetString();
            using var decision = await SendAsync(client, HttpMethod.Post, $"/sessions/{sessionId}/tool-approvals/{requestId}", "{\"decision\":\"deny\"}", ct).ConfigureAwait(false);
            decision.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        _ = await SessionClient.WaitForStatusAsync(client, sessionId, SessionStatus.Completed, SettleTimeout, ct).ConfigureAwait(false);
    }

    private static void Configure(IServiceCollection services, InMemoryScript? script, Action<IServiceCollection>? more)
    {
        if (script is not null)
        {
            services.AddSingleton<IHarnessSessionFactory>(new ScriptedHarnessFactory(script));
            _ = services.AddLiveHarnessTracking();
        }

        more?.Invoke(services);
    }
}
