using Autonogrammer.Contracts.Catalog;
using Autonogrammer.Web.Demo;
using Autonogrammer.Web.Services;
using Shouldly;
using Xunit;

namespace Autonogrammer.Web.Tests;

/// <summary>Every route of the typed client is answered by the Demo handler (no 404) and the client reads the documented shapes.</summary>
public sealed class OmpApiClientRouteTests : IDisposable
{
    private static readonly string[] ExpectedRoutes =
    [
        "GET /sessions",
        "GET /workspace",
        "GET /source",
        "GET /health/ready",
        "GET /account/board",
        "GET /account",
        "GET /account/sessions",
        "GET /sessions/{}",
        "GET /sessions/{}/stages",
        "GET /sessions/{}/context",
        "GET /sessions/{}/config-options",
        "GET /sessions/{}/commands",
        "GET /sessions/{}/changes",
        "GET /sessions/{}/card-details",
        "GET /sessions/{}/tool-approvals",
        "GET /sessions/{}/events",
        "POST /sessions",
        "PUT /sessions/{}/config-options/{}",
        "POST /sessions/{}/prompts",
        "POST /sessions/{}/steer",
        "POST /sessions/{}/tool-approvals/{}",
        "POST /sessions/{}/changes/{}/accept",
        "POST /sessions/{}/changes/{}/reject",
        "POST /sessions/{}/close",
        "POST /sessions/{}/reopen",
        "POST /sessions/{}/resume",
        "GET /events",
    ];

    private readonly DemoTestHost _host = new();

    /// <summary>Every client method reaches a Demo route; the recorded routes are exactly the documented set.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task EveryClientRouteIsAnsweredByTheDemo()
    {
        var routes = await ClientRouteSurface.ExerciseAsync(_host, TestContext.Current.CancellationToken).ConfigureAwait(true);

        routes.OrderBy(static route => route, StringComparer.Ordinal).ShouldBe(ExpectedRoutes.OrderBy(static route => route, StringComparer.Ordinal));
    }

    /// <summary>The catalogue lists the six Demo sessions with the states the rail shows, the terminal one with kind terminal.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task CatalogueHoldsSixSessionsInEveryRailState()
    {
        var entries = await _host.Api.ListSessionsAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        entries.Count.ShouldBe(6);
        entries.ToDictionary(static entry => entry.Id, static entry => entry.RowStatus, StringComparer.Ordinal).ShouldBe(new Dictionary<string, CatalogRowStatus>(StringComparer.Ordinal)
        {
            [DemoFixtures.RunningId] = CatalogRowStatus.Running,
            [DemoFixtures.ApprovalId] = CatalogRowStatus.NeedsApproval,
            [DemoFixtures.TerminalId] = CatalogRowStatus.Running,
            [DemoFixtures.CompletedId] = CatalogRowStatus.Completed,
            [DemoFixtures.FailedId] = CatalogRowStatus.Failed,
            [DemoFixtures.ClosedId] = CatalogRowStatus.Closed,
        });
        entries.Single(static entry => entry.Kind == CatalogSessionKind.Terminal).Id.ShouldBe(DemoFixtures.TerminalId);
        entries[0].Id.ShouldBe(DemoFixtures.RunningId, "the most recently active session comes first");
    }

    /// <summary>The Demo labels itself: source revision is demo, licence AGPL, the workspace is acme-shop with four connected plugins.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task SourceAndWorkspaceAreHonestlyLabelled()
    {
        var ct = TestContext.Current.CancellationToken;
        var source = await _host.Api.GetSourceAsync(ct).ConfigureAwait(true);
        var workspace = await _host.Api.GetWorkspaceAsync(ct).ConfigureAwait(true);
        var stages = await _host.Api.GetStagesAsync(DemoFixtures.RunningId, ct).ConfigureAwait(true);

        source.Revision.ShouldBe("demo");
        source.Licence.ShouldBe("AGPL-3.0-or-later");
        workspace.Name.ShouldBe("acme-shop");
        workspace.PluginCheck.Status.ShouldBe(PluginCheckStatus.Ok);
        stages.Stages.Count(static stage => stage.Source != Autonogrammer.Contracts.Agents.StageSource.Omp && stage.Status == Autonogrammer.Contracts.Agents.StageStatus.Succeeded && stage.Title.EndsWith("connected", StringComparison.Ordinal))
            .ShouldBe(4);
    }

    /// <summary>The board has the ten columns in desktop order and every session sits in the column of its status.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task BoardHasTenColumnsAndPlacesEverySession()
    {
        var board = await _host.Api.GetBoardAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        board.Columns.Select(static column => column.Key).ShouldBe(["backlog", "queued", "running", "awaiting-input", "needs-attention", "paused", "completed", "failed", "unreachable", "archived"]);
        board.Columns.SelectMany(static column => column.Cards).Count().ShouldBe(6);
        board.Columns.Single(static column => string.Equals(column.Key, "needs-attention", StringComparison.Ordinal)).Cards.Single().Id.ShouldBe(DemoFixtures.ApprovalId);
        board.Columns.Single(static column => string.Equals(column.Key, "archived", StringComparison.Ordinal)).Cards.Single().Id.ShouldBe(DemoFixtures.ClosedId);
    }

    /// <summary>GET account/sessions goes through the same bearer handler as the board.</summary>
    /// <returns>A task that completes when the header was checked.</returns>
    [Fact]
    public async Task GetAccountSessionsKeepsTheBearerHandler()
    {
        using var inner = new HeaderHandler();
        using var bearer = new BearerTokenHandler(new StaticToken("session-token"), inner);
        using var http = new HttpClient(bearer) { BaseAddress = new Uri("http://api.invalid/") };

        var list = await new OmpApiClient(http).GetAccountSessionsAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        list.Items.ShouldBeEmpty();
        inner.Authorization.ShouldBe("Bearer session-token");
        inner.Path.ShouldBe("/account/sessions");
    }

    /// <summary>The board request carries the bearer token and reads the API board response shape.</summary>
    [Fact]
    public async Task GetBoardUsesBearerAndParsesApiResponse()
    {
        using var inner = new HeaderHandler("{\"state\":\"ready\",\"columns\":[{\"key\":\"backlog\",\"title\":\"Backlog\",\"acceptsDrop\":true,\"cards\":[{\"id\":\"session-1\",\"title\":\"Session 1\",\"rowStatus\":\"ready\",\"columnKey\":\"backlog\",\"lastActivityAt\":\"2026-11-10T00:00:00Z\",\"isStale\":false,\"closed\":false}]}]}");
        using var bearer = new BearerTokenHandler(new StaticToken("board-token"), inner);
        using var http = new HttpClient(bearer) { BaseAddress = new Uri("http://api.invalid/") };

        var board = await new OmpApiClient(http).GetBoardAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        inner.Authorization.ShouldBe("Bearer board-token");
        inner.Path.ShouldBe("/account/board");
        board.State.ShouldBe(BoardStateKind.Ready);
        board.Columns.Single().Cards.Single().Id.ShouldBe("session-1");
    }

    /// <summary>The Demo offers 20 engine commands and a model list led by the default model.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task CommandsAndOptionsAreBelievable()
    {
        var ct = TestContext.Current.CancellationToken;
        var commands = await _host.Api.GetCommandsAsync(DemoFixtures.RunningId, ct).ConfigureAwait(true);
        var options = await _host.Api.GetConfigOptionsAsync(DemoFixtures.RunningId, ct).ConfigureAwait(true);

        commands.Commands.Count.ShouldBe(20);
        options.Options.Select(static option => option.Id).ShouldBe(["model", "mode", "thinking"]);
        options.Options[0].CurrentValue.ShouldBe(DemoCatalogue.DefaultModel);
        options.Options[1].Values.Select(static value => value.Value).ShouldBe(["default", "plan"]);
    }

    /// <inheritdoc />
    public void Dispose() => _host.Dispose();

    private sealed class StaticToken(string token) : IApiTokenProvider
    {
        public ValueTask<string?> GetTokenAsync(CancellationToken cancellationToken) => ValueTask.FromResult<string?>(token);
    }

    private sealed class HeaderHandler(string json = "{\"items\":[]}") : HttpMessageHandler
    {
        public string? Authorization { get; private set; }

        public string? Path { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Authorization = request.Headers.Authorization?.ToString();
            Path = request.RequestUri?.AbsolutePath;
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json"),
            });
        }
    }
}
