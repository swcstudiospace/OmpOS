using System.Net;
using System.Text.Json;
using OmpOS.Contracts.WorkspaceSettings;
using OmpOS.Web.Services;
using Shouldly;
using Xunit;

namespace OmpOS.Web.Tests.Settings;

/// <summary>The settings client: which addresses it reads, and how a refused or unreadable answer surfaces.</summary>
public sealed class WorkspaceSettingsClientTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>Each read is a GET of its own address with no body.</summary>
    [Fact]
    public async Task EachRead_IsAGetOfItsOwnAddress()
    {
        using var handler = new ScriptedHandler();
        using var http = WorkspaceFixtures.Http(handler);
        var client = WorkspaceFixtures.Client(http);
        using var conventions = WorkspaceFixtures.Json(HttpStatusCode.OK, WorkspaceFixtures.Conventions(), WorkspaceSettingsJsonContext.Default.ConventionsResponse);
        using var runtime = WorkspaceFixtures.Json(HttpStatusCode.OK, Runtime(), WorkspaceSettingsJsonContext.Default.RuntimeSettingsResponse);
        using var plugins = WorkspaceFixtures.Json(
            HttpStatusCode.OK,
            new PluginsSettingsResponse { Check = new OmpOS.Contracts.Catalog.PluginCheckResult { Status = OmpOS.Contracts.Catalog.PluginCheckStatus.Ok, Summary = "ok" }, Plugins = [] },
            WorkspaceSettingsJsonContext.Default.PluginsSettingsResponse);
        using var network = WorkspaceFixtures.Json(HttpStatusCode.OK, new NetworkSettingsResponse { Checked = false, Reason = "none", Endpoints = [] }, WorkspaceSettingsJsonContext.Default.NetworkSettingsResponse);
        using var hooks = WorkspaceFixtures.Json(
            HttpStatusCode.OK,
            new HooksResponse { Available = true, ConsentRecorded = false, Hooks = [], Sources = [], Diagnostics = [] },
            WorkspaceSettingsJsonContext.Default.HooksResponse);
        using var automations = WorkspaceFixtures.Json(
            HttpStatusCode.OK,
            new AutomationsResponse { Available = false, Reason = "none", Automations = [] },
            WorkspaceSettingsJsonContext.Default.AutomationsResponse);
        foreach (var response in new[] { conventions, runtime, plugins, network, hooks, automations })
        {
            handler.Enqueue(response);
        }

        _ = await client.GetConventionsAsync(Token).ConfigureAwait(true);
        _ = await client.GetRuntimeAsync(Token).ConfigureAwait(true);
        _ = await client.GetPluginsAsync(Token).ConfigureAwait(true);
        _ = await client.GetNetworkAsync(Token).ConfigureAwait(true);
        _ = await client.GetHooksAsync(Token).ConfigureAwait(true);
        _ = await client.GetAutomationsAsync(Token).ConfigureAwait(true);

        handler.Requests.Select(static request => request.Method).ShouldAllBe(static method => method == HttpMethod.Get);
        handler.Requests.Select(static request => request.RequestUri!.AbsolutePath).ShouldBe(
            ["/workspace/conventions", "/settings/runtime", "/settings/plugins", "/settings/network", "/hooks", "/automations"]);
        handler.Requests.ShouldAllBe(static request => request.Content == null);
    }

    /// <summary>A refused call carries the status and never reads the body.</summary>
    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task ARefusedCall_CarriesTheStatusOnly(HttpStatusCode status)
    {
        using var handler = new ScriptedHandler();
        using var http = WorkspaceFixtures.Http(handler);
        var client = WorkspaceFixtures.Client(http);
        using var refused = WorkspaceFixtures.Text(status, "{\"detail\":\"/srv/secret/path\"}");
        handler.Enqueue(refused);

        var failure = await Should.ThrowAsync<ApiClientException>(() => client.GetRuntimeAsync(Token)).ConfigureAwait(true);

        failure.StatusCode.ShouldBe(status);
        failure.Body.ShouldBeNull();
        failure.Message.ShouldNotContain("/srv/secret/path", Case.Sensitive);
    }

    /// <summary>A body that is not the expected JSON is a <see cref="JsonException"/>.</summary>
    [Fact]
    public async Task AMalformedBody_IsAJsonException()
    {
        using var handler = new ScriptedHandler();
        using var http = WorkspaceFixtures.Http(handler);
        var client = WorkspaceFixtures.Client(http);
        using var malformed = WorkspaceFixtures.Text(HttpStatusCode.OK, "{ not json");
        handler.Enqueue(malformed);

        _ = await Should.ThrowAsync<JsonException>(() => client.GetHooksAsync(Token)).ConfigureAwait(true);
    }

    /// <summary>A body with the literal <c>null</c> is refused as empty.</summary>
    [Fact]
    public async Task ANullBody_IsAnEmptyAnswer()
    {
        using var handler = new ScriptedHandler();
        using var http = WorkspaceFixtures.Http(handler);
        var client = WorkspaceFixtures.Client(http);
        using var empty = WorkspaceFixtures.Text(HttpStatusCode.OK, "null");
        handler.Enqueue(empty);

        _ = await Should.ThrowAsync<ApiClientException>(() => client.GetNetworkAsync(Token)).ConfigureAwait(true);
    }

    private static RuntimeSettingsResponse Runtime() => new()
    {
        HarnessAdapter = "Acp",
        SessionStore = "Postgres",
        CoordinationStore = "Dragonfly",
        RunLeaseSeconds = 30,
        RunLeaseRenewSeconds = 10,
        MaxPromptLength = 64000,
        MaxRequestBodyBytes = 1_048_576,
        PromptsPerMinute = 60,
        SessionsPerMinute = 60,
        WorkspaceRequestsPerMinute = 120,
        WorkspaceConfigured = true,
        IncludesUserScope = false,
        MaxWorkspaceFileBytes = 1_048_576,
    };
}
