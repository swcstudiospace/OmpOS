using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Autonogrammer.Api.Automations;
using Autonogrammer.Api.IntegrationTests.Cloud;
using Autonogrammer.Api.Options;
using Autonogrammer.Api.Security;
using Autonogrammer.Api.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shouldly;

namespace Autonogrammer.Api.IntegrationTests.Automations;

/// <summary>
/// A host that is only the automation routes on a test server: the automation services, owner authentication by header, the fallback
/// authorization policy and the rate limiter, with a fake cloud launcher and a clock the test moves. It is built the way <c>Program</c>
/// wires the same pieces.
/// </summary>
internal sealed class AutomationApiHost : IAsyncDisposable
{
    /// <summary>The first test owner.</summary>
    public const string Alice = "alice";

    /// <summary>The second test owner.</summary>
    public const string Bob = "bob";

    private readonly WebApplication _app;

    private AutomationApiHost(WebApplication app, AutomationClock clock, FakeCloudAgentLauncher launcher, CapturingLoggerProvider logs)
    {
        _app = app;
        Clock = clock;
        Launcher = launcher;
        Logs = logs;
    }

    /// <summary>Gets the clock.</summary>
    public AutomationClock Clock { get; }

    /// <summary>Gets the fake cloud launcher.</summary>
    public FakeCloudAgentLauncher Launcher { get; }

    /// <summary>Gets the captured log messages.</summary>
    public CapturingLoggerProvider Logs { get; }

    /// <summary>Gets the services of the host.</summary>
    public IServiceProvider Services => _app.Services;

    /// <summary>Starts a host.</summary>
    /// <param name="configuration">Configuration values over the defaults (<c>Automations:Enabled</c> true, scheduler off).</param>
    /// <param name="cancellationToken">Cancels the start.</param>
    /// <returns>The running host.</returns>
    public static async Task<AutomationApiHost> StartAsync(Dictionary<string, string?>? configuration = null, CancellationToken cancellationToken = default)
    {
        var clock = new AutomationClock(new DateTimeOffset(2026, 10, 28, 8, 0, 0, TimeSpan.Zero));
        var launcher = new FakeCloudAgentLauncher();
        var logs = new CapturingLoggerProvider();
        var values = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Automations:Enabled"] = "true",
            ["Automations:SchedulerEnabled"] = "false",
            ["Automations:RequestsPerMinute"] = "100000",
        };
        foreach (var (key, value) in configuration ?? [])
        {
            values[key] = value;
        }

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        _ = builder.WebHost.UseTestServer();
        _ = builder.Configuration.AddInMemoryCollection(values);
        _ = builder.Logging.ClearProviders().AddProvider(logs);
        builder.Services.AddSingleton<TimeProvider>(clock);
        builder.Services.AddOptions<ApiOptions>().BindConfiguration(ApiOptions.SectionName);
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<IUserContext>(static provider => new AuthenticatedUserContext(provider.GetRequiredService<IHttpContextAccessor>()));
        builder.Services.AddAuthentication(TestOwnerAuthHandler.SchemeName).AddScheme<AuthenticationSchemeOptions, TestOwnerAuthHandler>(TestOwnerAuthHandler.SchemeName, null);
        builder.Services.AddAuthorizationBuilder().SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
        builder.Services.AddRateLimiter(static _ => { });
        builder.Services.AddSingleton<ICloudAgentLauncher>(launcher);
        builder.Services.ConfigureHttpJsonOptions(static options => options.SerializerOptions.TypeInfoResolverChain.Insert(0, ApiJsonContext.Default));
        builder.Services.AddAutomationServices();
        var app = builder.Build();
        app.UseAuthentication();
        app.UseRateLimiter();
        app.UseAuthorization();
        app.UseMiddleware<AutomationDisabledMiddleware>();
        _ = app.MapAutomationEndpoints();
        await app.StartAsync(cancellationToken).ConfigureAwait(false);
        return new AutomationApiHost(app, clock, launcher, logs);
    }

    /// <summary>Creates a client that acts as <paramref name="owner"/>, or an unauthenticated one when it is null.</summary>
    /// <param name="owner">The owner's name, or null.</param>
    /// <returns>The client. The caller disposes it.</returns>
    public HttpClient Client(string? owner = Alice)
    {
        var client = _app.GetTestClient();
        if (owner is not null)
        {
            client.DefaultRequestHeaders.Add(TestOwnerAuthHandler.Header, owner);
        }

        return client;
    }

    /// <summary>Sends a JSON request.</summary>
    /// <param name="client">The client.</param>
    /// <param name="method">The method.</param>
    /// <param name="path">The relative path.</param>
    /// <param name="json">The body, or null for none.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The response.</returns>
    public static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string path, string? json, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        using var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative));
        if (json is not null)
        {
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        return await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Creates an automation as an owner and returns the response body.</summary>
    /// <param name="json">The request body, or null for <see cref="CreateBody"/>.</param>
    /// <param name="owner">The caller.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The body of the 201 answer.</returns>
    public async Task<JsonElement> CreateAsync(string? json = null, string owner = Alice, CancellationToken cancellationToken = default)
    {
        using var client = Client(owner);
        using var response = await SendAsync(client, HttpMethod.Post, "/automations", json ?? CreateBody(), cancellationToken).ConfigureAwait(false);
        var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, text);
        return Parse(text);
    }

    /// <summary>Builds a valid create body.</summary>
    /// <param name="triggers">The triggers JSON array, or null for a cron trigger and a generic webhook.</param>
    /// <param name="enabled">Whether the automation is enabled.</param>
    /// <returns>The JSON text.</returns>
    public static string CreateBody(string? triggers = null, bool enabled = true) =>
        $$$"""
        {"name":"Nightly review","prompt":"Review the changes of the last day.","enabled":{{{(enabled ? "true" : "false")}}},"triggers":{{{triggers ?? """[{"kind":"cron","cron":"0 9 * * *"},{"kind":"webhook"}]"""}}},"target":{"repositories":[{"address":"https://github.com/acme/widgets"}],"secretRefs":["NPM_TOKEN"]}}
        """;

    /// <summary>Parses JSON text.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The root element.</returns>
    public static JsonElement Parse(string text)
    {
        using var document = JsonDocument.Parse(text);
        return document.RootElement.Clone();
    }

    /// <summary>Reads a response body as JSON.</summary>
    /// <param name="response">The response.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The root element.</returns>
    public static async Task<JsonElement> ReadAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(response);
        return Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Reads the stable code of a problem response from its <c>type</c> member.</summary>
    /// <param name="response">The response.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The code, for example <c>automation_not_found</c>.</returns>
    public static async Task<string> ProblemCodeAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var problem = await ReadAsync(response, cancellationToken).ConfigureAwait(false);
        return problem.GetProperty("type").GetString()!.Replace("/problems/", string.Empty, StringComparison.Ordinal);
    }

    /// <summary>Builds a generic webhook delivery signed with the secret.</summary>
    /// <param name="id">The automation identifier.</param>
    /// <param name="secret">The signing secret.</param>
    /// <param name="body">The body.</param>
    /// <param name="sentAt">The timestamp, or null for the host clock.</param>
    /// <returns>The request.</returns>
    public HttpRequestMessage GenericDelivery(string id, string secret, string body = "{\"deploy\":\"v1\"}", DateTimeOffset? sentAt = null)
    {
        var timestamp = (sentAt ?? Clock.GetUtcNow()).ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
        var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/hooks/automations/" + id, UriKind.Relative)) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        request.Headers.Add("X-Autonogrammer-Timestamp", timestamp);
        request.Headers.Add("X-Autonogrammer-Signature", Sign(secret, Encoding.UTF8.GetBytes(timestamp + "." + body)));
        return request;
    }

    /// <summary>Builds a GitHub delivery signed with the secret.</summary>
    /// <param name="id">The automation identifier.</param>
    /// <param name="secret">The signing secret.</param>
    /// <param name="eventName">The event name.</param>
    /// <param name="body">The body.</param>
    /// <param name="delivery">The delivery identifier.</param>
    /// <returns>The request.</returns>
    public static HttpRequestMessage GitHubDelivery(string id, string secret, string eventName, string body, string delivery = "delivery-1")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/hooks/automations/" + id, UriKind.Relative)) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        request.Headers.Add("X-Hub-Signature-256", Sign(secret, Encoding.UTF8.GetBytes(body)));
        request.Headers.Add("X-GitHub-Event", eventName);
        request.Headers.Add("X-GitHub-Delivery", delivery);
        return request;
    }

    /// <summary>Waits until an automation has no run in progress, then returns its history.</summary>
    /// <param name="id">The automation identifier.</param>
    /// <param name="owner">The caller.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>The <c>runs</c> array.</returns>
    public async Task<JsonElement> SettledRunsAsync(string id, string owner = Alice, CancellationToken cancellationToken = default)
    {
        using var client = Client(owner);
        while (true)
        {
            using var response = await client.GetAsync(new Uri($"/automations/{id}/runs", UriKind.Relative), cancellationToken).ConfigureAwait(false);
            var runs = (await ReadAsync(response, cancellationToken).ConfigureAwait(false)).GetProperty("runs");
            if (runs.EnumerateArray().All(static run => !string.Equals(run.GetProperty("status").GetString(), "running", StringComparison.Ordinal)))
            {
                return runs;
            }

            await Task.Yield();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync().ConfigureAwait(false);
        await _app.DisposeAsync().ConfigureAwait(false);
    }

    private static string Sign(string secret, byte[] data) =>
        "sha256=" + Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), data));
}
