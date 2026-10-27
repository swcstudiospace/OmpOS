using System.Net;
using Microsoft.Extensions.DependencyInjection;
using OmpOS.Api.Services;
using OmpOS.Contracts.Controls;
using OmpOS.Harness.Abstractions;
using OmpOS.Harness.InMemory;
using OmpOS.Sessions;
using Shouldly;
using Xunit;

namespace OmpOS.Api.IntegrationTests;

/// <summary>
/// The configuration routes: <c>GET</c> and <c>PUT /sessions/{id}/config-options</c> and <c>GET /sessions/{id}/commands</c>. The engine owns the
/// lists; a change needs a live engine session, an engine mid-turn refuses it, and a successful change is appended to the session log as a
/// <c>config_option_update</c> event. Between runs the last reported list is returned with <c>live: false</c>.
/// </summary>
public sealed class SessionConfigEndpointTests
{
    /// <summary>Before any run the engine has reported nothing: the answer is unavailable with a reason.</summary>
    [Fact]
    public async Task Options_BeforeAnyRun_AreUnavailable()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = WebAppTestHost.Dev();
        using var client = factory.CreateClient();
        var id = await SessionClient.CreateSessionAsync(client, ct).ConfigureAwait(true);

        using var response = await client.GetAsync(new Uri($"/sessions/{id}/config-options", UriKind.Relative), ct).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await WebAppTestHost.ReadJsonAsync(response, ct).ConfigureAwait(true);
        body.RootElement.GetProperty("available").GetBoolean().ShouldBeFalse();
        body.RootElement.GetProperty("live").GetBoolean().ShouldBeFalse();
        body.RootElement.GetProperty("reason").GetString().ShouldNotBeNullOrWhiteSpace();
        body.RootElement.GetProperty("options").GetArrayLength().ShouldBe(0);
    }

    /// <summary>After a run the last list the engine reported stays readable, marked not live, and a change is refused.</summary>
    [Fact]
    public async Task Options_AfterARun_AreTheLastKnownList_AndAChangeIsRefused()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = WebAppTestHost.Dev();
        using var client = factory.CreateClient();
        var id = await SessionClient.CreateSessionAsync(client, ct).ConfigureAwait(true);
        using var accepted = await SessionClient.PostPromptAsync(client, id, "{\"prompt\":\"hi\"}", ct).ConfigureAwait(true);
        _ = await SessionClient.WaitForStatusAsync(client, id, OmpOS.Contracts.Sessions.SessionStatus.Completed, WebAppTestHost.SettleTimeout, ct).ConfigureAwait(true);

        using (var response = await client.GetAsync(new Uri($"/sessions/{id}/config-options", UriKind.Relative), ct).ConfigureAwait(true))
        {
            using var body = await WebAppTestHost.ReadJsonAsync(response, ct).ConfigureAwait(true);
            body.RootElement.GetProperty("available").GetBoolean().ShouldBeTrue();
            body.RootElement.GetProperty("live").GetBoolean().ShouldBeFalse();
            body.RootElement.GetProperty("options").EnumerateArray().Select(static option => option.GetProperty("id").GetString()).ShouldBe(["mode", "thought_level", "model"]);
        }

        using var change = await WebAppTestHost.SendAsync(client, HttpMethod.Put, $"/sessions/{id}/config-options/mode", "{\"value\":\"plan\"}", ct).ConfigureAwait(true);
        await WebAppTestHost.ShouldBeProblemAsync(change, HttpStatusCode.Conflict, "config_options_unavailable", ct).ConfigureAwait(true);
    }

    /// <summary>A live engine reports its options with current values and the model as one more option.</summary>
    [Fact]
    public async Task Options_OfALiveEngine_ListModeThinkingAndModel()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = WebAppTestHost.Dev();
        using var client = factory.CreateClient();
        var id = await SessionClient.CreateSessionAsync(client, ct).ConfigureAwait(true);
        _ = await TrackIdleEngineAsync(factory, id, ct).ConfigureAwait(true);

        using var response = await client.GetAsync(new Uri($"/sessions/{id}/config-options", UriKind.Relative), ct).ConfigureAwait(true);

        using var body = await WebAppTestHost.ReadJsonAsync(response, ct).ConfigureAwait(true);
        body.RootElement.GetProperty("live").GetBoolean().ShouldBeTrue();
        var options = body.RootElement.GetProperty("options").EnumerateArray().ToArray();
        options.Select(static option => option.GetProperty("id").GetString()).ShouldBe(["mode", "thought_level", "model"]);
        options.Select(static option => option.GetProperty("currentValue").GetString()).ShouldBe(["agent", "medium", "inmemory/fast"]);
        options[0].GetProperty("values").EnumerateArray().Select(static value => value.GetProperty("value").GetString()).ShouldBe(["agent", "ask", "plan"]);
    }

    /// <summary>A change reaches the engine, is returned, and is appended to the session log and its stream as a config_option_update event.</summary>
    [Fact]
    public async Task Put_ChangesTheEngine_AndLogsAnUpdateEvent()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = WebAppTestHost.Dev();
        using var client = factory.CreateClient();
        var id = await SessionClient.CreateSessionAsync(client, ct).ConfigureAwait(true);
        var engine = await TrackIdleEngineAsync(factory, id, ct).ConfigureAwait(true);

        using var response = await WebAppTestHost.SendAsync(client, HttpMethod.Put, $"/sessions/{id}/config-options/mode", "{\"value\":\"plan\"}", ct).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await WebAppTestHost.ReadJsonAsync(response, ct).ConfigureAwait(true);
        body.RootElement.GetProperty("options").EnumerateArray().Single(static option => string.Equals(option.GetProperty("id").GetString(), "mode", StringComparison.Ordinal))
            .GetProperty("currentValue").GetString().ShouldBe("plan", Case.Sensitive);
        engine.Options.Single(static option => string.Equals(option.Id, "mode", StringComparison.Ordinal)).CurrentValue.ShouldBe("plan", Case.Sensitive);

        var session = await factory.Services.GetRequiredService<ISessionRepository>().GetAsync(id, ct).ConfigureAwait(true);
        var logged = session.ShouldNotBeNull().Events.ShouldHaveSingleItem();
        StreamEventPayloads.TryReadConfigOptionUpdate(logged.Payload, out var update).ShouldBeTrue();
        update.ShouldNotBeNull().OptionId.ShouldBe("mode", Case.Sensitive);
        update.CurrentValue.ShouldBe("plan", Case.Sensitive);

        var items = await SessionClient.ReadStreamAsync(client, $"/sessions/{id}/events", 1, ct, lastEventId: "0").ConfigureAwait(true);
        items.ShouldHaveSingleItem().EventType.ShouldBe("config_option_update", Case.Sensitive);
    }

    /// <summary>The model is changed through the option named model.</summary>
    [Fact]
    public async Task Put_Model_SwitchesTheEnginesModel()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = WebAppTestHost.Dev();
        using var client = factory.CreateClient();
        var id = await SessionClient.CreateSessionAsync(client, ct).ConfigureAwait(true);
        var engine = await TrackIdleEngineAsync(factory, id, ct).ConfigureAwait(true);

        using var response = await WebAppTestHost.SendAsync(client, HttpMethod.Put, $"/sessions/{id}/config-options/model", "{\"value\":\"inmemory/deep\"}", ct).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        engine.CurrentModelId.ShouldBe("inmemory/deep", Case.Sensitive);
    }

    /// <summary>An unknown option is 404, an unknown value is 400, and neither changes the engine.</summary>
    [Fact]
    public async Task Put_UnknownOptionOrValue_IsRefused()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = WebAppTestHost.Dev();
        using var client = factory.CreateClient();
        var id = await SessionClient.CreateSessionAsync(client, ct).ConfigureAwait(true);
        var engine = await TrackIdleEngineAsync(factory, id, ct).ConfigureAwait(true);

        using (var option = await WebAppTestHost.SendAsync(client, HttpMethod.Put, $"/sessions/{id}/config-options/nope", "{\"value\":\"x\"}", ct).ConfigureAwait(true))
        {
            await WebAppTestHost.ShouldBeProblemAsync(option, HttpStatusCode.NotFound, "config_option_not_found", ct).ConfigureAwait(true);
        }

        using (var value = await WebAppTestHost.SendAsync(client, HttpMethod.Put, $"/sessions/{id}/config-options/mode", "{\"value\":\"nope\"}", ct).ConfigureAwait(true))
        {
            await WebAppTestHost.ShouldBeProblemAsync(value, HttpStatusCode.BadRequest, "invalid_config_value", ct).ConfigureAwait(true);
        }

        using (var model = await WebAppTestHost.SendAsync(client, HttpMethod.Put, $"/sessions/{id}/config-options/model", "{\"value\":\"nope/nope\"}", ct).ConfigureAwait(true))
        {
            await WebAppTestHost.ShouldBeProblemAsync(model, HttpStatusCode.BadRequest, "invalid_config_value", ct).ConfigureAwait(true);
        }

        engine.Options.Single(static option => string.Equals(option.Id, "mode", StringComparison.Ordinal)).CurrentValue.ShouldBe("agent", Case.Sensitive);
        engine.CurrentModelId.ShouldBe("inmemory/fast", Case.Sensitive);
    }

    /// <summary>An engine that is running a turn refuses the change with 409 config_option_busy.</summary>
    [Fact]
    public async Task Put_WhileATurnRuns_IsBusy()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = WebAppTestHost.Dev(WebAppTestHost.HeldAtApproval());
        using var client = factory.CreateClient();
        var id = await WebAppTestHost.StartHeldRunAsync(client, "hold", ct).ConfigureAwait(true);

        using (var response = await WebAppTestHost.SendAsync(client, HttpMethod.Put, $"/sessions/{id}/config-options/mode", "{\"value\":\"plan\"}", ct).ConfigureAwait(true))
        {
            await WebAppTestHost.ShouldBeProblemAsync(response, HttpStatusCode.Conflict, "config_option_busy", ct).ConfigureAwait(true);
        }

        using (var live = await client.GetAsync(new Uri($"/sessions/{id}/config-options", UriKind.Relative), ct).ConfigureAwait(true))
        {
            using var body = await WebAppTestHost.ReadJsonAsync(live, ct).ConfigureAwait(true);
            body.RootElement.GetProperty("live").GetBoolean().ShouldBeTrue();
        }

        await WebAppTestHost.ReleaseHeldRunAsync(client, id, ct).ConfigureAwait(true);
    }

    /// <summary>The body rules apply after the session lookup: a missing or blank value and malformed JSON are 400, another media type 415, a large body 413.</summary>
    [Fact]
    public async Task Put_BodyRules()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = WebAppTestHost.Dev(config: new Dictionary<string, string?>(StringComparer.Ordinal) { ["Api:MaxRequestBodyBytes"] = "64" });
        using var client = factory.CreateClient();
        var id = await SessionClient.CreateSessionAsync(client, ct).ConfigureAwait(true);
        _ = await TrackIdleEngineAsync(factory, id, ct).ConfigureAwait(true);

        foreach (var body in new[] { "{}", "{\"value\":\" \"}", "not json", string.Empty })
        {
            using var response = await WebAppTestHost.SendAsync(client, HttpMethod.Put, $"/sessions/{id}/config-options/mode", body, ct).ConfigureAwait(true);
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        using var text = new StringContent("{\"value\":\"plan\"}", System.Text.Encoding.UTF8, "text/plain");
        using (var wrongType = await client.PutAsync(new Uri($"/sessions/{id}/config-options/mode", UriKind.Relative), text, ct).ConfigureAwait(true))
        {
            wrongType.StatusCode.ShouldBe(HttpStatusCode.UnsupportedMediaType);
        }

        using var large = await WebAppTestHost.SendAsync(client, HttpMethod.Put, $"/sessions/{id}/config-options/mode", $"{{\"value\":\"{new string('z', 200)}\"}}", ct).ConfigureAwait(true);
        large.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
    }

    /// <summary>Another owner reads and changes nothing: every route answers 404 and the engine keeps its value.</summary>
    [Fact]
    public async Task ConfigRoutes_AreOwnerScoped()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = WebAppTestHost.Jwt();
        using var alice = WebAppTestHost.AsPrincipal(factory, "alice");
        using var bob = WebAppTestHost.AsPrincipal(factory, "bob");
        var id = await SessionClient.CreateSessionAsync(alice, ct).ConfigureAwait(true);
        var engine = await TrackIdleEngineAsync(factory, id, ct).ConfigureAwait(true);

        using var options = await bob.GetAsync(new Uri($"/sessions/{id}/config-options", UriKind.Relative), ct).ConfigureAwait(true);
        using var commands = await bob.GetAsync(new Uri($"/sessions/{id}/commands", UriKind.Relative), ct).ConfigureAwait(true);
        using var put = await WebAppTestHost.SendAsync(bob, HttpMethod.Put, $"/sessions/{id}/config-options/mode", "{\"value\":\"plan\"}", ct).ConfigureAwait(true);

        options.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        commands.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        put.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        engine.Options.Single(static option => string.Equals(option.Id, "mode", StringComparison.Ordinal)).CurrentValue.ShouldBe("agent", Case.Sensitive);
    }

    /// <summary>Commands are unavailable before a run, live while an engine is open, follow the engine's advertisement, and stay as last known afterwards.</summary>
    [Fact]
    public async Task Commands_FollowTheEngine()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = WebAppTestHost.Dev();
        using var client = factory.CreateClient();
        var id = await SessionClient.CreateSessionAsync(client, ct).ConfigureAwait(true);

        using (var none = await client.GetAsync(new Uri($"/sessions/{id}/commands", UriKind.Relative), ct).ConfigureAwait(true))
        {
            using var body = await WebAppTestHost.ReadJsonAsync(none, ct).ConfigureAwait(true);
            body.RootElement.GetProperty("available").GetBoolean().ShouldBeFalse();
        }

        var engine = await TrackIdleEngineAsync(factory, id, ct).ConfigureAwait(true);
        using (var live = await client.GetAsync(new Uri($"/sessions/{id}/commands", UriKind.Relative), ct).ConfigureAwait(true))
        {
            using var body = await WebAppTestHost.ReadJsonAsync(live, ct).ConfigureAwait(true);
            body.RootElement.GetProperty("live").GetBoolean().ShouldBeTrue();
            var names = body.RootElement.GetProperty("commands").EnumerateArray().Select(static command => command.GetProperty("name").GetString()).ToArray();
            names.ShouldBe(["plan", "model", "compact"]);
            body.RootElement.GetProperty("commands")[1].GetProperty("inputHint").GetString().ShouldBe("[model-id]", Case.Sensitive);
        }

        engine.AdvertiseCommands([new OmpCommand("review", "Review the change.", null)]);
        using (var changed = await client.GetAsync(new Uri($"/sessions/{id}/commands", UriKind.Relative), ct).ConfigureAwait(true))
        {
            using var body = await WebAppTestHost.ReadJsonAsync(changed, ct).ConfigureAwait(true);
            body.RootElement.GetProperty("commands").EnumerateArray().Select(static command => command.GetProperty("name").GetString()).ShouldBe(["review"]);
        }

        await engine.DisposeAsync().ConfigureAwait(true);
        using var last = await client.GetAsync(new Uri($"/sessions/{id}/commands", UriKind.Relative), ct).ConfigureAwait(true);
        using var lastBody = await WebAppTestHost.ReadJsonAsync(last, ct).ConfigureAwait(true);
        lastBody.RootElement.GetProperty("available").GetBoolean().ShouldBeTrue();
        lastBody.RootElement.GetProperty("live").GetBoolean().ShouldBeFalse();
        lastBody.RootElement.GetProperty("commands").EnumerateArray().Select(static command => command.GetProperty("name").GetString()).ShouldBe(["review"]);
    }

    private static async Task<InMemoryOmpSession> TrackIdleEngineAsync(OmpApiFactory factory, string sessionId, CancellationToken ct)
    {
        var engine = (InMemoryOmpSession)new InMemoryOmpAdapter(new InMemoryScript { Steps = [] }).CreateSession();
        await engine.CreateAsync(ct).ConfigureAwait(false);
        factory.Services.GetRequiredService<LiveSessionTracker>().Track(sessionId, engine);
        return engine;
    }
}
