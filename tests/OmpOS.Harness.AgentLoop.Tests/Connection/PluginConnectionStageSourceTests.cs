using OmpOS.Contracts.Agents;
using OmpOS.Harness.AgentLoop.Connection;
using OmpOS.Harness.AgentLoop.Launch;
using OmpOS.Harness.AgentLoop.Plugins;
using OmpOS.Harness.AgentLoop.Tests.Launch;
using OmpOS.Harness.AgentLoop.Tests.Plugins;
using OmpOS.Harness.AgentLoop.Tests.Stages;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.AgentLoop.Tests.Connection;

/// <summary>
/// Verifies the plugin connection stages: pending until the first report, the four outcomes of comparing a report with the
/// expected names, the deadline, unreadable and oversize reports, that only launched plugins get a stage, that commands are
/// only checked when omp could list them, that no report text reaches an event, and that cancellation ends the sequence.
/// </summary>
public sealed class PluginConnectionStageSourceTests
{
    private static readonly TimeSpan Interval = StageFixtures.Interval;

    private static PluginConnectionStageSource NewSource(
        StageFakeTimeProvider clock,
        IReadOnlyList<OmpPluginDescriptor>? plugins = null,
        TimeSpan? deadline = null) =>
        new(
            new PluginConnectionStageSourceOptions
            {
                Plugins = plugins ?? OmpPluginCatalog.BuiltIn,
                PollInterval = Interval,
                ReportDeadline = deadline ?? TimeSpan.FromSeconds(2),
            },
            clock);

    private static Task<StageObserverRun> StartAsync(PluginConnectionStageSource source, StageObservationScope scope, StageFakeTimeProvider clock) =>
        StageObserverRun.StartAsync(source, scope, clock, Interval);

    private static StageEvent Latest(StageObserverRun run, string pluginId) =>
        run.Events.Last(e => string.Equals(e.StageId, "connection:" + pluginId, StringComparison.Ordinal));

    private static IEnumerable<string> Without(IEnumerable<string> names, params string[] removed) =>
        names.Where(name => !removed.Contains(name, StringComparer.Ordinal));

    [Fact]
    public async Task WithoutAReportEveryPluginIsPendingWithItsOwnSourceAndTitleAndNothingIsEmittedAgain()
    {
        using var directory = new StageTempDirectory();
        var scope = directory.Scope(acpSessionId: null);
        var clock = new StageFakeTimeProvider();

        var run = await StartAsync(NewSource(clock), scope, clock).ConfigureAwait(true);
        await using var runLifetime = run.ConfigureAwait(true);
        await run.StepAsync(2).ConfigureAwait(true);

        run.Events.Select(e => e.StageId).ShouldBe(["connection:ultrathink", "connection:swarm", "connection:substrate", "connection:gsd"]);
        run.Events.Select(e => e.Source).ShouldBe([StageSource.Ultrathink, StageSource.Swarm, StageSource.Substrate, StageSource.Gsd]);
        run.Events.Select(e => e.Title).ShouldBe(["Ultrathink connection", "Agent Swarm connection", "Agent Substrate connection", "GSD connection"]);
        run.Events.ShouldAllBe(e => e.Status == StageStatus.Pending && e.Detail == "Waiting for omp to report its plugins" && e.SessionId == "s-1");
        Directory.Exists(scope.SessionStateDirectory).ShouldBeFalse();
    }

    [Fact]
    public async Task AReportThatArrivesLateWithEveryExpectedNameSucceedsEveryPlugin()
    {
        using var directory = new StageTempDirectory();
        var scope = directory.Scope();
        var clock = new StageFakeTimeProvider();
        var run = await StartAsync(NewSource(clock), scope, clock).ConfigureAwait(true);
        await using var runLifetime = run.ConfigureAwait(true);

        ConnectionTestSupport.Write(scope, ConnectionTestSupport.FullReport());
        await run.StepAsync().ConfigureAwait(true);

        foreach (var id in new[] { "ultrathink", "swarm", "substrate", "gsd" })
        {
            var latest = Latest(run, id);
            latest.Status.ShouldBe(StageStatus.Succeeded);
            latest.Detail.ShouldBe("Connected to omp");
            latest.Timestamp.ShouldBe(clock.GetUtcNow());
        }

        run.Events.Count.ShouldBe(8);
        await run.StepAsync(3).ConfigureAwait(true);
        run.Events.Count.ShouldBe(8);
    }

    [Fact]
    public async Task ACommandReportedWithALeadingSlashCountsAsRegistered()
    {
        using var directory = new StageTempDirectory();
        var scope = directory.Scope();
        ConnectionTestSupport.Write(scope, ConnectionTestSupport.Report(ConnectionTestSupport.AllTools, ConnectionTestSupport.AllCommands.Select(static name => "/" + name)));
        var clock = new StageFakeTimeProvider();

        var run = await StartAsync(NewSource(clock), scope, clock).ConfigureAwait(true);
        await using var runLifetime = run.ConfigureAwait(true);

        run.Events.ShouldAllBe(e => e.Status == StageStatus.Succeeded);
    }

    [Fact]
    public async Task SomeMissingNamesDegradeOnlyThatPluginAndNameThemInTheCatalogOrder()
    {
        using var directory = new StageTempDirectory();
        var scope = directory.Scope();
        var tools = Without(ConnectionTestSupport.AllTools, "swarm_gate", "swarm_catalog");
        ConnectionTestSupport.Write(scope, ConnectionTestSupport.Report(tools, ConnectionTestSupport.AllCommands));
        var clock = new StageFakeTimeProvider();

        var run = await StartAsync(NewSource(clock), scope, clock).ConfigureAwait(true);
        await using var runLifetime = run.ConfigureAwait(true);

        var swarm = Latest(run, "swarm");
        swarm.Status.ShouldBe(StageStatus.Degraded);
        swarm.Detail.ShouldBe("Registered 7 of 9 expected names; missing: swarm_gate, swarm_catalog");
        Latest(run, "ultrathink").Status.ShouldBe(StageStatus.Succeeded);
        Latest(run, "substrate").Status.ShouldBe(StageStatus.Succeeded);
        Latest(run, "gsd").Status.ShouldBe(StageStatus.Succeeded);
    }

    [Fact]
    public async Task NoExpectedNameAtAllFailsThePluginAndMissingCommandsAreShownWithASlash()
    {
        using var directory = new StageTempDirectory();
        var scope = directory.Scope();
        ConnectionTestSupport.Write(scope, ConnectionTestSupport.Report(ConnectionTestSupport.AllTools, Without(ConnectionTestSupport.AllCommands, "substrate")));
        var clock = new StageFakeTimeProvider();

        var run = await StartAsync(NewSource(clock), scope, clock).ConfigureAwait(true);
        await using var runLifetime = run.ConfigureAwait(true);

        var substrate = Latest(run, "substrate");
        substrate.Status.ShouldBe(StageStatus.Failed);
        substrate.Detail.ShouldBe("Registered 0 of 1 expected names; missing: /substrate");
    }

    [Fact]
    public async Task AReportWithNothingInItFailsAPluginThatHasToolsAndListsAtMostEightNames()
    {
        using var directory = new StageTempDirectory();
        var scope = directory.Scope();
        ConnectionTestSupport.Write(scope, ConnectionTestSupport.Report([], []));
        var clock = new StageFakeTimeProvider();

        var run = await StartAsync(NewSource(clock, ConnectionTestSupport.Plugins("swarm")), scope, clock).ConfigureAwait(true);
        await using var runLifetime = run.ConfigureAwait(true);

        var swarm = run.Events.ShouldHaveSingleItem();
        swarm.Status.ShouldBe(StageStatus.Failed);
        swarm.Detail.ShouldBe(
            "Registered 0 of 9 expected names; missing: swarm_status, swarm_assignment, swarm_plan, swarm_ingest, swarm_transition, swarm_gate, swarm_catalog, swarm_tool_call, and 1 more");
    }

    [Fact]
    public async Task WhenOmpCouldNotListItsCommandsOnlyToolsAreCheckedAndACommandOnlyPluginCannotBeChecked()
    {
        using var directory = new StageTempDirectory();
        var scope = directory.Scope();
        ConnectionTestSupport.Write(scope, ConnectionTestSupport.Report(Without(ConnectionTestSupport.AllTools, "swarm_plan"), [], commandsAvailable: false));
        var clock = new StageFakeTimeProvider();

        var run = await StartAsync(NewSource(clock), scope, clock).ConfigureAwait(true);
        await using var runLifetime = run.ConfigureAwait(true);

        var swarm = Latest(run, "swarm");
        swarm.Status.ShouldBe(StageStatus.Degraded);
        swarm.Detail.ShouldBe("Registered 7 of 8 expected names; missing: swarm_plan");
        foreach (var (id, name) in new[] { ("ultrathink", "Ultrathink"), ("substrate", "Agent Substrate"), ("gsd", "GSD") })
        {
            var latest = Latest(run, id);
            latest.Status.ShouldBe(StageStatus.Degraded);
            latest.Detail.ShouldBe("omp could not list its commands, so " + name + " could not be checked");
        }
    }

    [Fact]
    public async Task WhenOmpCouldNotListItsCommandsAPluginWithAllItsToolsStillSucceeds()
    {
        using var directory = new StageTempDirectory();
        var scope = directory.Scope();
        ConnectionTestSupport.Write(scope, ConnectionTestSupport.Report(ConnectionTestSupport.AllTools, [], commandsAvailable: false));
        var clock = new StageFakeTimeProvider();

        var run = await StartAsync(NewSource(clock, ConnectionTestSupport.Plugins("swarm")), scope, clock).ConfigureAwait(true);
        await using var runLifetime = run.ConfigureAwait(true);

        var swarm = run.Events.ShouldHaveSingleItem();
        swarm.Status.ShouldBe(StageStatus.Succeeded);
        swarm.Detail.ShouldBe("Connected to omp");
    }

    [Fact]
    public async Task ACommandListThatTheReportDisownsIsIgnoredEvenWhenItNamesEverything()
    {
        using var directory = new StageTempDirectory();
        var scope = directory.Scope();
        ConnectionTestSupport.Write(scope, ConnectionTestSupport.Report(ConnectionTestSupport.AllTools, ConnectionTestSupport.AllCommands, commandsAvailable: false));
        var clock = new StageFakeTimeProvider();

        var run = await StartAsync(NewSource(clock, ConnectionTestSupport.Plugins("substrate")), scope, clock).ConfigureAwait(true);
        await using var runLifetime = run.ConfigureAwait(true);

        run.Events.ShouldHaveSingleItem().Status.ShouldBe(StageStatus.Degraded);
    }

    [Fact]
    public async Task ReportOnlyTheLaunchedPluginsGetAStage()
    {
        using var directory = new StageTempDirectory();
        var scope = directory.Scope();
        ConnectionTestSupport.Write(scope, ConnectionTestSupport.FullReport());
        var clock = new StageFakeTimeProvider();

        var run = await StartAsync(NewSource(clock, ConnectionTestSupport.Plugins("ultrathink", "gsd")), scope, clock).ConfigureAwait(true);
        await using var runLifetime = run.ConfigureAwait(true);

        run.Events.Select(e => e.StageId).ShouldBe(["connection:ultrathink", "connection:gsd"]);
    }

    [Fact]
    public async Task TheOptionsForAPlanNameExactlyThePluginsThePlannerLoaded()
    {
        var settings = new AgentLoopSettings
        {
            PluginsRoot = PlannerFixtures.PluginsRoot,
            StateRoot = PlannerFixtures.StateRoot,
            SwarmEnabled = false,
        };
        var plugins = PlannerFixtures.AllReady().Where(static plugin => !string.Equals(plugin.Descriptor.Id, "gsd", StringComparison.Ordinal)).ToList();
        var plan = OmpLaunchPlanner.Plan(settings, plugins, "s1", "/work", null, new FixedTimeProvider(new DateTimeOffset(2026, 10, 26, 12, 0, 0, TimeSpan.Zero)));

        var options = PluginConnectionStageSourceOptions.ForPlan(plan);

        options.Plugins.Select(plugin => plugin.Id).ShouldBe(["ultrathink"]);
        using var directory = new StageTempDirectory();
        var clock = new StageFakeTimeProvider();
        var run = await StartAsync(new PluginConnectionStageSource(options with { PollInterval = Interval }, clock), directory.Scope(), clock).ConfigureAwait(true);
        await using var runLifetime = run.ConfigureAwait(true);
        run.Events.ShouldHaveSingleItem().StageId.ShouldBe("connection:ultrathink");
    }

    [Fact]
    public async Task AnEmptyPluginListEndsTheSequenceAtOnceWithoutReadingAnything()
    {
        using var directory = new StageTempDirectory();
        var clock = new StageFakeTimeProvider();

        var run = await StartAsync(NewSource(clock, []), directory.Scope(), clock).ConfigureAwait(true);
        await using var runLifetime = run.ConfigureAwait(true);
        await run.Completion.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken).ConfigureAwait(true);

        run.Events.ShouldBeEmpty();
        clock.TimersCreated.ShouldBe(0);
    }

    [Fact]
    public async Task NoReportWithinTheDefaultThirtySecondsDegradesEveryStageAndALateReportRecoversThem()
    {
        using var directory = new StageTempDirectory();
        var scope = directory.Scope();
        var clock = new StageFakeTimeProvider();
        var source = new PluginConnectionStageSource(
            new PluginConnectionStageSourceOptions { Plugins = OmpPluginCatalog.BuiltIn, PollInterval = Interval },
            clock);

        var run = await StartAsync(source, scope, clock).ConfigureAwait(true);
        await using var runLifetime = run.ConfigureAwait(true);
        await run.StepAsync(59).ConfigureAwait(true);
        run.Events.ShouldAllBe(e => e.Status == StageStatus.Pending);

        await run.StepAsync().ConfigureAwait(true);

        run.Events.Count.ShouldBe(8);
        run.Events.Skip(4).ShouldAllBe(e => e.Status == StageStatus.Degraded && e.Detail == "omp has not reported its plugins after 30 s");

        ConnectionTestSupport.Write(scope, ConnectionTestSupport.FullReport());
        await run.StepAsync().ConfigureAwait(true);

        run.Events.Count.ShouldBe(12);
        run.Events.Skip(8).ShouldAllBe(e => e.Status == StageStatus.Succeeded);
    }

    [Fact]
    public async Task AReportThatGrowsFromPartialToCompleteMovesTheStageFromDegradedToSucceeded()
    {
        using var directory = new StageTempDirectory();
        var scope = directory.Scope();
        ConnectionTestSupport.Write(scope, ConnectionTestSupport.Report(Without(ConnectionTestSupport.AllTools, "swarm_tool_call"), ConnectionTestSupport.AllCommands));
        var clock = new StageFakeTimeProvider();

        var run = await StartAsync(NewSource(clock, ConnectionTestSupport.Plugins("swarm")), scope, clock).ConfigureAwait(true);
        await using var runLifetime = run.ConfigureAwait(true);
        run.Events.ShouldHaveSingleItem().Status.ShouldBe(StageStatus.Degraded);

        ConnectionTestSupport.Write(scope, ConnectionTestSupport.FullReport());
        await run.StepAsync().ConfigureAwait(true);

        run.Events.Select(e => e.Status).ShouldBe([StageStatus.Degraded, StageStatus.Succeeded]);
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("")]
    [InlineData("[1,2,3]")]
    [InlineData("{\"v\":2,\"tools\":[]}")]
    public async Task AnUnreadableReportIsDegradedPollingContinuesAndTheNextGoodReportSucceeds(string content)
    {
        using var directory = new StageTempDirectory();
        var scope = directory.Scope();
        ConnectionTestSupport.Write(scope, content);
        var clock = new StageFakeTimeProvider();

        var run = await StartAsync(NewSource(clock, ConnectionTestSupport.Plugins("gsd")), scope, clock).ConfigureAwait(true);
        await using var runLifetime = run.ConfigureAwait(true);
        await run.StepAsync(2).ConfigureAwait(true);

        var degraded = run.Events.ShouldHaveSingleItem();
        degraded.Status.ShouldBe(StageStatus.Degraded);
        degraded.Detail.ShouldBe("omp wrote a connection report that could not be read");

        ConnectionTestSupport.Write(scope, ConnectionTestSupport.FullReport());
        await run.StepAsync().ConfigureAwait(true);

        run.Events.Count.ShouldBe(2);
        run.Events[1].Status.ShouldBe(StageStatus.Succeeded);
        run.Completion.IsCompleted.ShouldBeFalse();
    }

    [Fact]
    public async Task AnOversizeReportIsDegradedWithoutBeingParsed()
    {
        using var directory = new StageTempDirectory();
        var scope = directory.Scope();
        ConnectionTestSupport.Write(scope, new byte[PluginConnectionReport.MaxFileBytes + 1]);
        var clock = new StageFakeTimeProvider();

        var run = await StartAsync(NewSource(clock, ConnectionTestSupport.Plugins("gsd")), scope, clock).ConfigureAwait(true);
        await using var runLifetime = run.ConfigureAwait(true);

        run.Events.ShouldHaveSingleItem().Detail.ShouldBe("omp wrote a connection report that could not be read");
    }

    [Fact]
    public async Task AReportThatBecomesUnreadableAfterAGoodOneKeepsTheLastKnownState()
    {
        using var directory = new StageTempDirectory();
        var scope = directory.Scope();
        ConnectionTestSupport.Write(scope, ConnectionTestSupport.FullReport());
        var clock = new StageFakeTimeProvider();

        var run = await StartAsync(NewSource(clock, ConnectionTestSupport.Plugins("gsd")), scope, clock).ConfigureAwait(true);
        await using var runLifetime = run.ConfigureAwait(true);
        ConnectionTestSupport.Write(scope, "{ half a report");
        await run.StepAsync(2).ConfigureAwait(true);

        run.Events.ShouldHaveSingleItem().Status.ShouldBe(StageStatus.Succeeded);
    }

    [Fact]
    public async Task AReportThatDisappearsAfterAGoodOneKeepsTheLastKnownState()
    {
        using var directory = new StageTempDirectory();
        var scope = directory.Scope();
        ConnectionTestSupport.Write(scope, ConnectionTestSupport.FullReport());
        var clock = new StageFakeTimeProvider();

        var run = await StartAsync(NewSource(clock, ConnectionTestSupport.Plugins("gsd")), scope, clock).ConfigureAwait(true);
        await using var runLifetime = run.ConfigureAwait(true);
        File.Delete(SessionStateLayout.ConnectionFile(scope.SessionStateDirectory));
        await run.StepAsync(3).ConfigureAwait(true);

        run.Events.ShouldHaveSingleItem().Status.ShouldBe(StageStatus.Succeeded);
    }

    [Fact]
    public async Task NamesFromTheReportNeverReachAnEventText()
    {
        using var directory = new StageTempDirectory();
        var scope = directory.Scope();
        ConnectionTestSupport.Write(
            scope,
            "{\"v\":1,\"tools\":[\"swarm_status\\nINJECTED-TEXT\",\"SECRET-TOOL\"],\"commands\":[\"/INJECTED-COMMAND\"],\"commandsAvailable\":true,\"mode\":\"INJECTED-MODE\"}");
        var clock = new StageFakeTimeProvider();

        var run = await StartAsync(NewSource(clock), scope, clock).ConfigureAwait(true);
        await using var runLifetime = run.ConfigureAwait(true);

        run.Events.Count.ShouldBe(4);
        foreach (var stageEvent in run.Events)
        {
            stageEvent.Status.ShouldBe(StageStatus.Failed);
            var detail = stageEvent.Detail.ShouldNotBeNull();
            detail.ShouldNotContain("INJECTED", Case.Sensitive);
            detail.ShouldNotContain("SECRET", Case.Sensitive);
            detail.Contains('\n', StringComparison.Ordinal).ShouldBeFalse();
            detail.Length.ShouldBeLessThanOrEqualTo(200);
        }
    }

    [Fact]
    public async Task AScopeWithAnUnusableStateDirectoryIsDegradedNotAnException()
    {
        var scope = new StageObservationScope { SessionId = "s-1", WorkspaceRoot = "/work", SessionStateDirectory = " " };
        var clock = new StageFakeTimeProvider();

        var run = await StartAsync(NewSource(clock, ConnectionTestSupport.Plugins("gsd")), scope, clock).ConfigureAwait(true);
        await using var runLifetime = run.ConfigureAwait(true);

        run.Events.ShouldHaveSingleItem().Status.ShouldBe(StageStatus.Degraded);
        run.Completion.IsCompleted.ShouldBeFalse();
    }

    [Fact]
    public async Task ASessionNeverReadsAnotherSessionsReport()
    {
        using var directory = new StageTempDirectory();
        ConnectionTestSupport.Write(directory.Scope("session-b"), ConnectionTestSupport.FullReport());
        var mine = directory.Scope("session-a");
        var clock = new StageFakeTimeProvider();

        var run = await StartAsync(NewSource(clock), mine, clock).ConfigureAwait(true);
        await using var runLifetime = run.ConfigureAwait(true);
        await run.StepAsync(2).ConfigureAwait(true);

        run.Events.ShouldAllBe(e => e.Status == StageStatus.Pending && e.SessionId == "session-a");
    }

    [Fact]
    public async Task CancellationEndsTheSequenceWithoutAnException()
    {
        using var directory = new StageTempDirectory();
        var clock = new StageFakeTimeProvider();

        var run = await StartAsync(NewSource(clock), directory.Scope(), clock).ConfigureAwait(true);
        await using var runLifetime = run.ConfigureAwait(true);
        run.Completion.IsCompleted.ShouldBeFalse();

        await run.StopAsync().ConfigureAwait(true);

        run.Completion.IsCompletedSuccessfully.ShouldBeTrue();
    }

    [Fact]
    public void TheSourceRejectsNonPositiveIntervalsAndMissingOptions()
    {
        Should.Throw<ArgumentNullException>(() => new PluginConnectionStageSource(null!));
        Should.Throw<ArgumentOutOfRangeException>(() => new PluginConnectionStageSource(
            new PluginConnectionStageSourceOptions { Plugins = [], PollInterval = TimeSpan.Zero }));
        Should.Throw<ArgumentOutOfRangeException>(() => new PluginConnectionStageSource(
            new PluginConnectionStageSourceOptions { Plugins = [], ReportDeadline = TimeSpan.Zero }));
    }
}
