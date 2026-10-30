using System.Net;
using Autonogrammer.Contracts.Agents;
using Autonogrammer.Harness.AgentLoop.Substrate;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.AgentLoop.Tests.Substrate;

/// <summary>Stage mapping, panel-safety, polling, backoff and recovery of <see cref="SubstrateStageObserver"/>.</summary>
public sealed class SubstrateStageObserverTests
{
    private static StageEvent Stage(IEnumerable<StageEvent> batch, string stageId) => batch.Single(stage => string.Equals(stage.StageId, stageId, StringComparison.Ordinal));

    [Fact]
    public async Task TheFirstCycleEmitsHealthBriefLeaseAndLedgerStagesWithTheEventsOldestFirst()
    {
        var rig = new ObserverRig(SubstrateTestSupport.Scope());
        await using var rigLifetime = rig.ConfigureAwait(true);

        var batch = await rig.NextCycleAsync().ConfigureAwait(true);

        batch.Select(static stage => stage.StageId).ShouldBe(["health", "brief", "lease", "ledger", "event:01J0E1", "event:01J0E2", "event:01J0E3"]);
        batch.ShouldAllBe(static stage => stage.Source == StageSource.Substrate && stage.SessionId == "session-1" && stage.GraphId == SubstrateTestSupport.GraphId);

        var health = Stage(batch, "health");
        health.Status.ShouldBe(StageStatus.Succeeded);
        health.Detail.ShouldBe("Substrate is reachable; index up, ledger up, events writable.");

        var brief = Stage(batch, "brief");
        brief.Status.ShouldBe(StageStatus.Succeeded);
        brief.Detail.ShouldBe("4 sections, 5 items.");

        var lease = Stage(batch, "lease");
        lease.Status.ShouldBe(StageStatus.Running);
        lease.Detail.ShouldBe("Held by omp until 2026-10-26T09:00:00Z.");

        var ledger = Stage(batch, "ledger");
        ledger.Status.ShouldBe(StageStatus.Succeeded);
        ledger.Detail.ShouldBe("3 recent events.");
        ledger.ParentStageId.ShouldBeNull();

        var tool = Stage(batch, "event:01J0E1");
        tool.Title.ShouldBe("tool.call");
        tool.Status.ShouldBe(StageStatus.Succeeded);
        tool.Detail.ShouldBe("2026-10-26T08:08:00Z, actor agent");
        tool.ParentStageId.ShouldBe("ledger");
        tool.Timestamp.ShouldBe(new DateTimeOffset(2026, 10, 26, 8, 8, 0, TimeSpan.Zero));

        var warning = Stage(batch, "event:01J0E2");
        warning.Title.ShouldBe("warning lease.expired");
        warning.Status.ShouldBe(StageStatus.Degraded);
        warning.Detail.ShouldBe("2026-10-26T08:09:00Z, actor agent");

        var steer = Stage(batch, "event:01J0E3");
        steer.Title.ShouldBe("note steer");
        steer.Detail.ShouldBe("2026-10-26T08:10:03Z, actor human, item 0123456789ab, outcome queued");
    }

    [Fact]
    public async Task TheBriefIsReadForTheWorkspaceRepositoryAndTheGraph()
    {
        var rig = new ObserverRig(SubstrateTestSupport.Scope());
        await using var rigLifetime = rig.ConfigureAwait(true);

        await rig.NextCycleAsync().ConfigureAwait(true);

        var brief = rig.Server.Requests.Single(static request => string.Equals(request.Uri.AbsolutePath, "/brief", StringComparison.Ordinal));
        brief.Body.ShouldBe("""{"repo":"swcstudiospace/OmpOS","graph_id":"ut-lq1a2b3c-0f1e2d3c","surface":"omp"}""");
    }

    [Fact]
    public async Task NoLedgerSummaryOrPayloadTextEverReachesAnyStageField()
    {
        using var server = new FakeSubstrateServer { GraphFixture = "graph-get-leaky.json" };
        var rig = new ObserverRig(SubstrateTestSupport.Scope(), server);
        await using var rigLifetime = rig.ConfigureAwait(true);

        var batch = await rig.NextCycleAsync().ConfigureAwait(true);

        batch.Count.ShouldBeGreaterThan(5);
        foreach (var stage in batch)
        {
            var everything = stage.ToString();
            everything.ShouldNotContain("LEAK", Case.Sensitive);
            everything.ShouldNotContain("operator steering", Case.Sensitive);
            everything.ShouldNotContain("acp-session-1", Case.Sensitive);
        }
    }

    [Fact]
    public async Task TheLeaseStageShowsOnlyHolderSurfaceAndExpiryNeverALeaseOrSessionIdentifier()
    {
        using var server = new FakeSubstrateServer { GraphFixture = "graph-get-leaky.json" };
        var rig = new ObserverRig(SubstrateTestSupport.Scope(), server);
        await using var rigLifetime = rig.ConfigureAwait(true);

        var batch = await rig.NextCycleAsync().ConfigureAwait(true);

        var lease = Stage(batch, "lease");
        lease.Status.ShouldBe(StageStatus.Running);
        lease.Detail.ShouldBe("Held by omp until 2026-10-26T09:00:00Z.");
    }

    [Fact]
    public async Task AnExpiredLeaseIsADegradedStageNamingTheHolderAndWhenItLapsed()
    {
        using var expired = new FakeSubstrateServer { GraphFixture = "graph-get-expired.json" };
        var rig = new ObserverRig(SubstrateTestSupport.Scope(), expired);
        await using (rig.ConfigureAwait(true))
        {
            var batch = await rig.NextCycleAsync().ConfigureAwait(true);

            var lease = Stage(batch, "lease");
            lease.Status.ShouldBe(StageStatus.Degraded);
            lease.Detail.ShouldBe("Lease lapsed: omp expired 2026-10-26T07:00:00Z.");
        }
    }

    [Fact]
    public async Task AGraphMissingFromTheIndexIsADegradedLeaseStage()
    {
        using var rig = GatewayRig.Create(request =>
        {
            if (string.Equals(request.Uri.AbsolutePath, "/mcp", StringComparison.Ordinal))
            {
                return ScriptedHandler.Text(HttpStatusCode.OK, SubstrateTestSupport.RpcResult("No graph ut-lq1a2b3c-0f1e2d3c in the index."));
            }

            return ScriptedHandler.Text(HttpStatusCode.OK, "{}");
        });
        var observer = new SubstrateStageObserver(rig.Gateway, TimeProvider.System, _ => null);
        var enumerator = observer.ObserveAsync(SubstrateTestSupport.Scope(acpSessionId: null), TestContext.Current.CancellationToken).GetAsyncEnumerator(TestContext.Current.CancellationToken);
        await using var enumeratorLifetime = enumerator.ConfigureAwait(true);

        var stages = new List<StageEvent>();
        while (stages.Count < 3 && await enumerator.MoveNextAsync().ConfigureAwait(true))
        {
            stages.Add(enumerator.Current);
        }

        var lease = Stage(stages, "lease");
        lease.Status.ShouldBe(StageStatus.Degraded);
        lease.Detail.ShouldBe("The graph is not registered in the Substrate index.");
    }

    [Fact]
    public async Task UnchangedStagesAreNotEmittedAgainButANewLedgerEventIsEmittedOnce()
    {
        var rig = new ObserverRig(SubstrateTestSupport.Scope());
        await using var rigLifetime = rig.ConfigureAwait(true);
        await rig.NextCycleAsync().ConfigureAwait(true);
        rig.Tick().ShouldBe(TimeSpan.FromSeconds(2));

        var quiet = await rig.NextCycleAsync().ConfigureAwait(true);

        quiet.ShouldBeEmpty();

        rig.Server.EventsFixture = "events-query-newer.json";
        rig.Tick();
        var changed = await rig.NextCycleAsync().ConfigureAwait(true);

        changed.Select(static stage => stage.StageId).ShouldBe(["ledger", "event:01J0E4"]);
        Stage(changed, "ledger").Detail.ShouldBe("4 recent events.");
        Stage(changed, "event:01J0E4").Detail.ShouldBe("2026-10-26T08:12:00Z, actor agent, outcome claimed");
        Stage(changed, "event:01J0E4").Title.ShouldBe("claim granted");
    }

    [Fact]
    public async Task TheBriefIsRefreshedEveryThirtySecondsAndNotOnEveryPoll()
    {
        var rig = new ObserverRig(SubstrateTestSupport.Scope());
        await using var rigLifetime = rig.ConfigureAwait(true);

        for (var cycle = 0; cycle < 15; cycle++)
        {
            await rig.NextCycleAsync().ConfigureAwait(true);
            if (cycle < 14)
            {
                rig.Tick();
            }
        }

        rig.Server.Count(HttpMethod.Post, "/brief").ShouldBe(1);
        rig.Server.Count(HttpMethod.Get, "/healthz").ShouldBe(15);

        rig.Tick();
        await rig.NextCycleAsync().ConfigureAwait(true);

        rig.Server.Count(HttpMethod.Post, "/brief").ShouldBe(2);
    }

    [Fact]
    public async Task AnUnhealthyServerEmitsUnavailableOnceBacksOffToThirtySecondsThenRecoversByItself()
    {
        using var server = new FakeSubstrateServer { HealthStatus = HttpStatusCode.ServiceUnavailable };
        var rig = new ObserverRig(SubstrateTestSupport.Scope(), server);
        await using var rigLifetime = rig.ConfigureAwait(true);

        var delays = new List<TimeSpan>();
        var outage = new List<StageEvent>();
        for (var cycle = 0; cycle < 5; cycle++)
        {
            outage.AddRange(await rig.NextCycleAsync().ConfigureAwait(true));
            delays.Add(rig.Clock.NextDueIn!.Value);
            if (cycle < 4)
            {
                rig.Tick();
            }
        }

        delays.ShouldBe([TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(16), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30)]);
        var unavailable = outage.ShouldHaveSingleItem();
        unavailable.StageId.ShouldBe("health");
        unavailable.Status.ShouldBe(StageStatus.Unavailable);
        unavailable.Detail.ShouldNotBeNull().ShouldContain("HttpStatus");
        unavailable.Detail.ShouldContain("503");
        server.Requests.ShouldAllBe(static request => request.Uri.AbsolutePath == "/healthz");

        server.HealthStatus = HttpStatusCode.OK;
        rig.Tick();
        var recovered = await rig.NextCycleAsync().ConfigureAwait(true);

        recovered.Select(static stage => stage.StageId).ShouldBe(["health", "brief", "lease", "ledger", "event:01J0E1", "event:01J0E2", "event:01J0E3"]);
        Stage(recovered, "health").Status.ShouldBe(StageStatus.Succeeded);
        rig.Clock.NextDueIn.ShouldBe(TimeSpan.FromSeconds(2));
        outage.ShouldAllBe(static stage => !stage.ToString().Contains(SubstrateTestSupport.Token, StringComparison.Ordinal));
        server.Logger.Lines.ShouldAllBe(static line => !line.Contains(SubstrateTestSupport.Token, StringComparison.Ordinal));
    }

    [Fact]
    public async Task AFailureAfterRecoveryStartsTheBackoffAgainFromTheShortestDelay()
    {
        using var server = new FakeSubstrateServer();
        var rig = new ObserverRig(SubstrateTestSupport.Scope(), server);
        await using var rigLifetime = rig.ConfigureAwait(true);
        await rig.NextCycleAsync().ConfigureAwait(true);

        server.HealthStatus = HttpStatusCode.ServiceUnavailable;
        rig.Tick();
        var down = await rig.NextCycleAsync().ConfigureAwait(true);
        down.ShouldHaveSingleItem().Status.ShouldBe(StageStatus.Unavailable);
        rig.Clock.NextDueIn.ShouldBe(TimeSpan.FromSeconds(4));

        server.HealthStatus = HttpStatusCode.OK;
        rig.Tick();
        var up = await rig.NextCycleAsync().ConfigureAwait(true);
        Stage(up, "health").Status.ShouldBe(StageStatus.Succeeded);
        rig.Clock.NextDueIn.ShouldBe(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task APartThatFailsBecomesADegradedStageWhileTheOthersKeepWorking()
    {
        using var server = new FakeSubstrateServer { McpStatus = HttpStatusCode.ServiceUnavailable, EventsStatus = HttpStatusCode.ServiceUnavailable };
        var rig = new ObserverRig(SubstrateTestSupport.Scope(), server);
        await using var rigLifetime = rig.ConfigureAwait(true);

        var batch = await rig.NextCycleAsync().ConfigureAwait(true);

        Stage(batch, "health").Status.ShouldBe(StageStatus.Succeeded);
        Stage(batch, "brief").Status.ShouldBe(StageStatus.Succeeded);
        var lease = Stage(batch, "lease");
        lease.Status.ShouldBe(StageStatus.Degraded);
        lease.Detail.ShouldBe("The task lease could not be read (HttpStatus, HTTP 503).");
        var ledger = Stage(batch, "ledger");
        ledger.Status.ShouldBe(StageStatus.Degraded);
        ledger.Detail.ShouldBe("The event ledger could not be read (HttpStatus, HTTP 503).");
        batch.ShouldNotContain(static stage => stage.StageId.StartsWith("event:", StringComparison.Ordinal));

        server.McpStatus = HttpStatusCode.OK;
        server.EventsStatus = HttpStatusCode.OK;
        rig.Tick();
        var healed = await rig.NextCycleAsync().ConfigureAwait(true);

        Stage(healed, "lease").Status.ShouldBe(StageStatus.Running);
        Stage(healed, "ledger").Status.ShouldBe(StageStatus.Succeeded);
        healed.Count(static stage => stage.StageId.StartsWith("event:", StringComparison.Ordinal)).ShouldBe(3);
    }

    [Fact]
    public async Task TheLedgerIsOnlyReadForTheSessionsGraphOrElseForTheSessionAndNeverUnscoped()
    {
        var withGraph = new ObserverRig(SubstrateTestSupport.Scope());
        await using (withGraph.ConfigureAwait(true))
        {
            await withGraph.NextCycleAsync().ConfigureAwait(true);

            withGraph.Server.Requests.Single(static r => string.Equals(r.Uri.AbsolutePath, "/events/query", StringComparison.Ordinal)).Body.ShouldBe("""{"graph_id":"ut-lq1a2b3c-0f1e2d3c","limit":10,"order":"desc","include_payload":true}""");
        }

        var sessionOnly = new ObserverRig(SubstrateTestSupport.Scope(graphId: null));
        await using (sessionOnly.ConfigureAwait(true))
        {
            var batch = await sessionOnly.NextCycleAsync().ConfigureAwait(true);

            sessionOnly.Server.Requests.Single(static r => string.Equals(r.Uri.AbsolutePath, "/events/query", StringComparison.Ordinal)).Body.ShouldBe("""{"session_id":"acp-session-1","limit":10,"order":"desc","include_payload":true}""");
            sessionOnly.Server.Count(HttpMethod.Post, "/mcp").ShouldBe(0);
            batch.ShouldNotContain(static stage => stage.StageId == "lease");
        }

        var neither = new ObserverRig(SubstrateTestSupport.Scope(graphId: null, acpSessionId: null));
        await using var neitherLifetime = neither.ConfigureAwait(true);
        var none = await neither.NextCycleAsync().ConfigureAwait(true);

        neither.Server.Count(HttpMethod.Post, "/events/query").ShouldBe(0);
        none.Select(static stage => stage.StageId).ShouldBe(["health", "brief"]);
    }

    [Fact]
    public async Task CancellationEndsTheSequenceWithoutAnExceptionAndWithoutAnotherPoll()
    {
        var rig = new ObserverRig(SubstrateTestSupport.Scope());
        await using var rigLifetime = rig.ConfigureAwait(true);
        await rig.NextCycleAsync().ConfigureAwait(true);
        var requests = rig.Server.Requests.Count;

        await rig.StopAsync().ConfigureAwait(true);

        rig.Completion.IsCompletedSuccessfully.ShouldBeTrue();
        rig.Server.Requests.Count.ShouldBe(requests);
    }

    [Theory]
    [InlineData(-3, 2)]
    [InlineData(0, 2)]
    [InlineData(1, 4)]
    [InlineData(2, 8)]
    [InlineData(3, 16)]
    [InlineData(4, 30)]
    [InlineData(5, 30)]
    [InlineData(1000, 30)]
    public void TheDelayDoublesWithEachConsecutiveFailureUpToThirtySeconds(int failures, int seconds)
    {
        SubstrateStageObserver.DelayAfter(failures).ShouldBe(TimeSpan.FromSeconds(seconds));
        SubstrateStageObserver.PollInterval.ShouldBe(TimeSpan.FromSeconds(2));
        SubstrateStageObserver.MaxBackoff.ShouldBe(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task AnEventWhoseIdentifierHasNothingSafeInItProducesNoStage()
    {
        using var rig = GatewayRig.Create(request => string.Equals(request.Uri.AbsolutePath, "/events/query", StringComparison.Ordinal)
            ? ScriptedHandler.Text(HttpStatusCode.OK, """{"events":[{"id":"\u202E\u0000","ts":"2026-10-26T08:00:00Z","kind":"note"},{"id":"ok-1\u202E","kind":"note"}],"count":2}""")
            : ScriptedHandler.Text(HttpStatusCode.OK, SubstrateTestSupport.RpcResult("No graph")));
        var observer = new SubstrateStageObserver(rig.Gateway, TimeProvider.System, _ => null);
        var enumerator = observer.ObserveAsync(SubstrateTestSupport.Scope(graphId: null), TestContext.Current.CancellationToken).GetAsyncEnumerator(TestContext.Current.CancellationToken);
        await using var enumeratorLifetime = enumerator.ConfigureAwait(true);

        var stages = new List<StageEvent>();
        while (stages.Count < 4 && await enumerator.MoveNextAsync().ConfigureAwait(true))
        {
            stages.Add(enumerator.Current);
        }

        stages.Select(static stage => stage.StageId).ShouldBe(["health", "brief", "ledger", "event:ok-1"]);
    }
}
