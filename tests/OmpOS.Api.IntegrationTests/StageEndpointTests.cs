using System.Net;
using Microsoft.Extensions.DependencyInjection;
using OmpOS.Api.Services;
using OmpOS.Contracts.Agents;
using OmpOS.Contracts.Controls;
using OmpOS.Sessions;
using Shouldly;
using Xunit;

namespace OmpOS.Api.IntegrationTests;

/// <summary>
/// <c>GET /sessions/{id}/stages</c> and <c>GET /sessions/{id}/context</c>: the engine turn is derived from the stored session, a plugin
/// source that publishes appears with its own stage, a source that is not connected is explained as unavailable, and another owner's session is 404.
/// </summary>
public sealed class StageEndpointTests
{
    /// <summary>With no plugin source connected, the engine stage is listed and each plugin source is an explained unavailable stage.</summary>
    [Fact]
    public async Task Stages_WithoutAPluginSource_ListTheTurnAndExplainTheRest()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = WebAppTestHost.Dev();
        using var client = factory.CreateClient();
        var id = await SessionClient.CreateSessionAsync(client, ct).ConfigureAwait(true);

        using var response = await client.GetAsync(new Uri($"/sessions/{id}/stages", UriKind.Relative), ct).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var document = await WebAppTestHost.ReadJsonAsync(response, ct).ConfigureAwait(true);
        document.RootElement.GetProperty("available").GetBoolean().ShouldBeFalse();
        document.RootElement.GetProperty("reason").GetString().ShouldNotBeNullOrWhiteSpace();
        var stages = document.RootElement.GetProperty("stages").EnumerateArray().ToArray();
        stages.Select(static stage => stage.GetProperty("source").GetString()).ShouldBe(["Omp", "Ultrathink", "Swarm", "Substrate", "Gsd"]);
        stages[0].GetProperty("status").GetString().ShouldBe("Pending", Case.Sensitive);
        foreach (var plugin in stages.Skip(1))
        {
            plugin.GetProperty("status").GetString().ShouldBe("Unavailable", Case.Sensitive);
        }
    }

    /// <summary>A finished run turns the engine stage to Succeeded.</summary>
    [Fact]
    public async Task Stages_TrackTheSessionStatus()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = WebAppTestHost.Dev();
        using var client = factory.CreateClient();
        var id = await SessionClient.CreateSessionAsync(client, ct).ConfigureAwait(true);
        using var accepted = await SessionClient.PostPromptAsync(client, id, "{\"prompt\":\"go\"}", ct).ConfigureAwait(true);
        _ = await SessionClient.WaitForStatusAsync(client, id, OmpOS.Contracts.Sessions.SessionStatus.Completed, WebAppTestHost.SettleTimeout, ct).ConfigureAwait(true);

        using var response = await client.GetAsync(new Uri($"/sessions/{id}/stages", UriKind.Relative), ct).ConfigureAwait(true);

        using var document = await WebAppTestHost.ReadJsonAsync(response, ct).ConfigureAwait(true);
        document.RootElement.GetProperty("stages")[0].GetProperty("status").GetString().ShouldBe("Succeeded", Case.Sensitive);
    }

    /// <summary>A published plugin stage is listed in place of that source's unavailable stage, is appended to the session log as a stage event, and names the Graph in the catalog.</summary>
    [Fact]
    public async Task PublishedStage_IsListed_LoggedAndNamesTheGraph()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = WebAppTestHost.Dev();
        using var client = factory.CreateClient();
        var id = await SessionClient.CreateSessionAsync(client, ct).ConfigureAwait(true);
        var hub = factory.Services.GetRequiredService<StageHub>();
        (await hub.PublishAsync(StageFixtures.Planner(id, "graph-9"), ct).ConfigureAwait(true)).ShouldBeTrue();

        using var stages = await client.GetAsync(new Uri($"/sessions/{id}/stages", UriKind.Relative), ct).ConfigureAwait(true);
        using var document = await WebAppTestHost.ReadJsonAsync(stages, ct).ConfigureAwait(true);
        document.RootElement.GetProperty("available").GetBoolean().ShouldBeTrue();
        var planner = document.RootElement.GetProperty("stages").EnumerateArray().Where(static stage => string.Equals(stage.GetProperty("source").GetString(), "Ultrathink", StringComparison.Ordinal)).ShouldHaveSingleItem();
        planner.GetProperty("stageId").GetString().ShouldBe("plan", Case.Sensitive);
        planner.GetProperty("status").GetString().ShouldBe("Running", Case.Sensitive);

        var session = await factory.Services.GetRequiredService<ISessionRepository>().GetAsync(id, ct).ConfigureAwait(true);
        var logged = session.ShouldNotBeNull().Events.ShouldHaveSingleItem();
        StreamEventPayloads.TryReadStage(logged.Payload, out var stage).ShouldBeTrue();
        stage.ShouldNotBeNull().GraphId.ShouldBe("graph-9", Case.Sensitive);

        using var list = await client.GetAsync(new Uri("/sessions", UriKind.Relative), ct).ConfigureAwait(true);
        using var rows = await WebAppTestHost.ReadJsonAsync(list, ct).ConfigureAwait(true);
        rows.RootElement[0].GetProperty("graphId").GetString().ShouldBe("graph-9", Case.Sensitive);
    }

    /// <summary>A stage for a session that does not exist is refused by the hub and appends nothing.</summary>
    [Fact]
    public async Task PublishedStage_ForAnAbsentSession_IsRefused()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = WebAppTestHost.Dev();
        using var client = factory.CreateClient();

        (await factory.Services.GetRequiredService<StageHub>().PublishAsync(StageFixtures.Planner("absent", "g"), ct).ConfigureAwait(true)).ShouldBeFalse();
    }

    /// <summary>Another owner's session and an absent session both answer 404 on both routes.</summary>
    [Fact]
    public async Task StagesAndContext_AreOwnerScoped()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = WebAppTestHost.Jwt();
        using var alice = WebAppTestHost.AsPrincipal(factory, "alice");
        using var bob = WebAppTestHost.AsPrincipal(factory, "bob");
        var id = await SessionClient.CreateSessionAsync(alice, ct).ConfigureAwait(true);

        foreach (var suffix in new[] { "stages", "context" })
        {
            using var foreign = await bob.GetAsync(new Uri($"/sessions/{id}/{suffix}", UriKind.Relative), ct).ConfigureAwait(true);
            using var absent = await bob.GetAsync(new Uri($"/sessions/absent/{suffix}", UriKind.Relative), ct).ConfigureAwait(true);
            foreign.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            absent.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }
    }

    /// <summary>Without a source the context is unavailable with a reason; with one it carries the context and the scope names the session and its Graph.</summary>
    [Fact]
    public async Task Context_ReportsUnavailable_OrTheSourcesContext()
    {
        var ct = TestContext.Current.CancellationToken;
        using (var bare = WebAppTestHost.Dev())
        using (var client = bare.CreateClient())
        {
            var id = await SessionClient.CreateSessionAsync(client, ct).ConfigureAwait(true);
            using var response = await client.GetAsync(new Uri($"/sessions/{id}/context", UriKind.Relative), ct).ConfigureAwait(true);
            using var document = await WebAppTestHost.ReadJsonAsync(response, ct).ConfigureAwait(true);
            document.RootElement.GetProperty("available").GetBoolean().ShouldBeFalse();
            document.RootElement.TryGetProperty("context", out _).ShouldBeFalse();
        }

        var root = Directory.CreateTempSubdirectory("ompos-ctx-").FullName;
        try
        {
            var source = new FakeTaskContextSource();
            using var factory = WebAppTestHost.Dev(
                config: new Dictionary<string, string?>(StringComparer.Ordinal) { ["Api:Workspace:Root"] = root },
                more: services => services.AddSingleton<ITaskContextSource>(source));
            using var client = factory.CreateClient();
            var id = await SessionClient.CreateSessionAsync(client, ct).ConfigureAwait(true);
            (await factory.Services.GetRequiredService<StageHub>().PublishAsync(StageFixtures.Planner(id, "graph-3"), ct).ConfigureAwait(true)).ShouldBeTrue();

            using var response = await client.GetAsync(new Uri($"/sessions/{id}/context", UriKind.Relative), ct).ConfigureAwait(true);

            using var document = await WebAppTestHost.ReadJsonAsync(response, ct).ConfigureAwait(true);
            document.RootElement.GetProperty("available").GetBoolean().ShouldBeTrue();
            document.RootElement.GetProperty("context").GetProperty("title").GetString().ShouldBe("Ship the web head", Case.Sensitive);
            source.LastScope.ShouldNotBeNull().GraphId.ShouldBe("graph-3", Case.Sensitive);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
