using System.Net;
using Autonogrammer.Contracts.Agents;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Harness.Acp;
using Autonogrammer.Harness.AgentLoop.Launch;
using Autonogrammer.Harness.AgentLoop.Plugins;
using Autonogrammer.Native.Agents;
using Autonogrammer.Native.Views.Sessions;
using Shouldly;
using Xunit;
using static Autonogrammer.Native.Tests.AgentLoop.AgentLoopFixtures;

namespace Autonogrammer.Native.Tests.AgentLoop;

/// <summary>
/// The head's engine set over scripted omp peers and temporary directories: N sessions with their own state, plan and engine, the
/// degradations of missing plugins and of an unhealthy Substrate, the registry kept in step with the runtimes, restart and resume,
/// and the shutdown of an engine. No test starts omp, a model or a network connection.
/// </summary>
public sealed class SessionEngineSetTests
{
    private const string Secret = "tok-7c1e-never-print-me";

    private const UnixFileMode OwnerOnly = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

    private static Func<string, string?> Substrate(string address) => name => name switch
    {
        AgentLoopEnvironment.SubstrateUrlVariable => address,
        AgentLoopEnvironment.DefaultSubstrateTokenVariable => Secret,
        _ => null,
    };

    /// <summary>Two sessions get two state directories that only their owner may enter, two plans whose plugin state lives in their own directories, and two engines that talk to two peers.</summary>
    [Fact]
    public async Task TwoSessionsGetDistinctStateDirectoriesPlansAndEngines()
    {
        var ct = TestContext.Current.CancellationToken;
        using var tree = new TempTree();
        tree.WriteReadyPlugins();
        var omp = new ScriptedOmp();
        var head = AgentHead.Create(Options(tree, omp));
        await using var headLifetime = head.ConfigureAwait(true);
        await head.InitializeAsync(ct).ConfigureAwait(true);

        var (idA, slotA) = await NewSlotAsync(head, ct).ConfigureAwait(true);
        var (idB, slotB) = await NewSlotAsync(head, ct).ConfigureAwait(true);
        var recordA = head.Host.Registry!.TryGet(idA, out var a) ? a : throw new InvalidOperationException("no record A");
        var recordB = head.Host.Registry!.TryGet(idB, out var b) ? b : throw new InvalidOperationException("no record B");
        var planA = await slotA.PlanAsync(ct).ConfigureAwait(true);
        var planB = await slotB.PlanAsync(ct).ConfigureAwait(true);
        await SendAsync(slotA, "one", ct).ConfigureAwait(true);
        await SendAsync(slotB, "two", ct).ConfigureAwait(true);

        string.Equals(idA, idB, StringComparison.Ordinal).ShouldBeFalse();
        string.Equals(recordA.StateDirectory, recordB.StateDirectory, StringComparison.Ordinal).ShouldBeFalse();
        Directory.Exists(recordA.StateDirectory).ShouldBeTrue();
        Directory.Exists(recordB.StateDirectory).ShouldBeTrue();
        if (!OperatingSystem.IsWindows())
        {
            File.GetUnixFileMode(recordA.StateDirectory).ShouldBe(OwnerOnly);
            File.GetUnixFileMode(recordB.StateDirectory).ShouldBe(OwnerOnly);
        }

        string[] expectedArguments =
        [
            "--no-extensions",
            "-e", tree.PluginPath("claude-ultrathink"),
            "-e", tree.PluginPath("agent-swarm/omp"),
            "-e", tree.PluginPath("gsd-core-omp"),
        ];
        planA.Launch.ExtraArguments.ShouldBe(expectedArguments);
        planB.Launch.ExtraArguments.ShouldBe(expectedArguments);
        planA.Launch.Environment["ULTRATHINK_STATE_DIR"].ShouldBe(SessionStateLayout.UltrathinkDirectory(recordA.StateDirectory));
        planB.Launch.Environment["ULTRATHINK_STATE_DIR"].ShouldBe(SessionStateLayout.UltrathinkDirectory(recordB.StateDirectory));
        string.Equals(planA.Launch.Environment["SWARM_DIR"], planB.Launch.Environment["SWARM_DIR"], StringComparison.Ordinal).ShouldBeFalse();
        (await slotA.EngineAsync(ct).ConfigureAwait(true)).ShouldNotBeSameAs(await slotB.EngineAsync(ct).ConfigureAwait(true));

        omp.Launches.Count.ShouldBe(2);
        omp.Launches[0].WorkingDirectory.ShouldBe(tree.Workspace);
        omp.Launches[0].ExtraArguments.ShouldBe(expectedArguments);
        omp.Launches[0].EnvironmentVariables["ULTRATHINK_STATE_DIR"].ShouldBe(planA.Launch.Environment["ULTRATHINK_STATE_DIR"]);
        omp.Launches[1].EnvironmentVariables["ULTRATHINK_STATE_DIR"].ShouldBe(planB.Launch.Environment["ULTRATHINK_STATE_DIR"]);
        omp.Requests("session/prompt").Count.ShouldBe(2);
        head.Host.Registry!.TryGet(idA, out var afterA).ShouldBeTrue();
        head.Host.Registry!.TryGet(idB, out var afterB).ShouldBeTrue();
        afterA!.AcpSessionId.ShouldBe("native-1");
        afterB!.AcpSessionId.ShouldBe("native-2");
    }

    /// <summary>With no plugins root at all, the head still opens, every plugin stage is Unavailable with the fix, and the session runs as plain omp.</summary>
    [Fact]
    public async Task AMissingPluginsRootYieldsUnavailableStagesAndAWorkingPlainSession()
    {
        var ct = TestContext.Current.CancellationToken;
        using var tree = new TempTree();
        Directory.Exists(tree.Plugins).ShouldBeFalse();
        var omp = new ScriptedOmp();
        var head = AgentHead.Create(Options(tree, omp));
        await using var headLifetime = head.ConfigureAwait(true);
        await head.InitializeAsync(ct).ConfigureAwait(true);
        var (_, slot) = await NewSlotAsync(head, ct).ConfigureAwait(true);

        var seen = await ObserveUntilAsync(
            slot,
            events => events.Any(e => e is { Source: StageSource.Ultrathink, StageId: "plugin", Status: StageStatus.Unavailable })
                && events.Any(e => e is { Source: StageSource.Swarm, StageId: "plugin", Status: StageStatus.Unavailable })
                && events.Any(e => e is { Source: StageSource.Gsd, StageId: "plugin", Status: StageStatus.Degraded }),
            ct).ConfigureAwait(true);
        var turn = await SendAsync(slot, "hello", ct).ConfigureAwait(true);

        seen.Where(e => e is { Source: StageSource.Ultrathink, StageId: "plugin", Status: StageStatus.Unavailable })
            .ShouldContain(e => e.Detail!.Contains("git submodule update --init --recursive plugins/claude-ultrathink", StringComparison.Ordinal));
        seen.Where(e => e is { Source: StageSource.Swarm, StageId: "plugin", Status: StageStatus.Unavailable })
            .ShouldContain(e => e.Detail!.Contains("git submodule update --init --recursive plugins/agent-swarm", StringComparison.Ordinal));
        turn.OfType<TextDeltaEvent>().Single().Text.ShouldBe("Hi from the scripted omp");
        turn.Last().ShouldBeOfType<CompletedEvent>();
        omp.Launches.ShouldHaveSingleItem().ExtraArguments.ShouldBe(["--no-extensions"]);
    }

    /// <summary>A submodule directory that exists but is empty reports the exact message of the loader, with the one command that fixes it, and only that plugin is left out.</summary>
    [Fact]
    public async Task AnUninitialisedSubmoduleShowsTheExactFixText()
    {
        var ct = TestContext.Current.CancellationToken;
        using var tree = new TempTree();
        tree.WriteReadyPlugins();
        Directory.Delete(tree.PluginPath("claude-ultrathink"), recursive: true);
        Directory.CreateDirectory(tree.PluginPath("claude-ultrathink"));
        var head = AgentHead.Create(Options(tree, new ScriptedOmp()));
        await using var headLifetime = head.ConfigureAwait(true);
        await head.InitializeAsync(ct).ConfigureAwait(true);
        var (_, slot) = await NewSlotAsync(head, ct).ConfigureAwait(true);
        var expected = SubmoduleNotInitializedException.FormatMessage("ultrathink", "claude-ultrathink", tree.PluginPath("claude-ultrathink"));

        var seen = await ObserveUntilAsync(
            slot,
            events => events.Any(e => e is { Source: StageSource.Ultrathink, StageId: "plugin", Status: StageStatus.Unavailable } && string.Equals(e.Detail, expected, StringComparison.Ordinal)),
            ct).ConfigureAwait(true);
        var plan = await slot.PlanAsync(ct).ConfigureAwait(true);

        expected.ShouldEndWith("git submodule update --init --recursive plugins/claude-ultrathink");
        seen.ShouldContain(e => e.Source == StageSource.Ultrathink && e.StageId == "plugin" && e.Status == StageStatus.Unavailable && e.Detail == expected);
        seen.ShouldNotContain(e => e.Source == StageSource.Swarm && e.StageId == "plugin" && e.Status == StageStatus.Unavailable);
        plan.Launch.ExtraArguments.ShouldNotContain(tree.PluginPath("claude-ultrathink"), StringComparer.Ordinal);
        plan.Launch.ExtraArguments.ShouldContain(tree.PluginPath("agent-swarm/omp"), StringComparer.Ordinal);
    }

    /// <summary>A Substrate that does not answer its health check is left out of the plan, the MCP server list and the child's environment, and the planner's Degraded event is kept.</summary>
    [Fact]
    public async Task AnUnhealthySubstrateIsOmittedFromThePlanAndTheMcpList()
    {
        var ct = TestContext.Current.CancellationToken;
        using var tree = new TempTree();
        tree.WriteReadyPlugins();
        using var substrate = new StubSubstrateHandler { HealthStatus = HttpStatusCode.ServiceUnavailable };
        var omp = new ScriptedOmp();
        var head = AgentHead.Create(Options(tree, omp, Substrate("http://127.0.0.1:7410"), substrate));
        await using var headLifetime = head.ConfigureAwait(true);
        await head.InitializeAsync(ct).ConfigureAwait(true);
        var (_, slot) = await NewSlotAsync(head, ct).ConfigureAwait(true);

        var plan = await slot.PlanAsync(ct).ConfigureAwait(true);
        await SendAsync(slot, "hello", ct).ConfigureAwait(true);

        plan.SubstrateActive.ShouldBeFalse();
        plan.Steering.ShouldBeNull();
        plan.Launch.McpServers.ShouldBeEmpty();
        plan.Launch.ExtraArguments.Any(static argument => argument.Contains("substrate-status.ts", StringComparison.Ordinal)).ShouldBeFalse();
        plan.Launch.Environment.ContainsKey("SUBSTRATE_URL").ShouldBeFalse();
        var degraded = plan.Launch.PlanningEvents.Single(static e => e.Source == StageSource.Substrate);
        degraded.Status.ShouldBe(StageStatus.Degraded);
        degraded.Detail.ShouldNotBeNull().ShouldContain("did not answer its health check");
        omp.Launches.ShouldHaveSingleItem().McpServers.ShouldBeEmpty();
        omp.Requests("session/new").Single()["params"]!["mcpServers"]!.AsArray().ShouldBeEmpty();
        substrate.Requests.All(static request => string.Equals(request.Path, "/healthz", StringComparison.Ordinal)).ShouldBeTrue();
    }

    /// <summary>A healthy Substrate is loaded into the plan: its two extensions, its MCP entry with the bearer header, and the steering recorder; the token appears in no description.</summary>
    [Fact]
    public async Task AHealthySubstrateIsPlannedAndItsTokenAppearsInNoDescription()
    {
        var ct = TestContext.Current.CancellationToken;
        using var tree = new TempTree();
        tree.WriteReadyPlugins();
        using var substrate = new StubSubstrateHandler();
        var omp = new ScriptedOmp();
        var head = AgentHead.Create(Options(tree, omp, Substrate("http://127.0.0.1:7410"), substrate));
        await using var headLifetime = head.ConfigureAwait(true);
        await head.InitializeAsync(ct).ConfigureAwait(true);
        var (_, slot) = await NewSlotAsync(head, ct).ConfigureAwait(true);

        var plan = await slot.PlanAsync(ct).ConfigureAwait(true);
        await SendAsync(slot, "hello", ct).ConfigureAwait(true);

        plan.SubstrateActive.ShouldBeTrue();
        plan.Steering.ShouldNotBeNull();
        plan.Launch.ExtraArguments.ShouldContain(tree.PluginPath("agent-substrate/packages/projector/omp/substrate-status.ts"), StringComparer.Ordinal);
        plan.Launch.ExtraArguments.ShouldContain(tree.PluginPath("agent-substrate/packages/projector/omp/substrate-skills.ts"), StringComparer.Ordinal);
        var server = omp.Launches.ShouldHaveSingleItem().McpServers.ShouldHaveSingleItem().ShouldBeOfType<AcpHttpMcpServer>();
        server.Name.ShouldBe("substrate");
        server.Url.ToString().ShouldBe("http://127.0.0.1:7410/mcp");
        server.Headers["Authorization"].ShouldBe("Bearer " + Secret);
        plan.Launch.ToString().ShouldNotContain(Secret);
        server.ToString().ShouldNotContain(Secret);
        plan.Launch.McpServers.Single().ToString().ShouldNotContain(Secret);
        head.Host.Loop.ToString().ShouldNotContain(Secret);
    }

    /// <summary>A started turn is Running, a completed one Completed, a stopped one Created, a faulted one Failed; each touches the session's activity.</summary>
    [Fact]
    public async Task TheRegistryFollowsTheTurnEventsOfARuntime()
    {
        var ct = TestContext.Current.CancellationToken;
        using var tree = new TempTree();
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 10, 26, 12, 0, 0, TimeSpan.Zero));
        var head = AgentHead.Create(Options(tree, new ScriptedOmp(), clock: clock));
        await using var headLifetime = head.ConfigureAwait(true);
        await head.InitializeAsync(ct).ConfigureAwait(true);
        var (id, slot) = await NewSlotAsync(head, ct).ConfigureAwait(true);
        var registry = head.Host.Registry!;

        SessionLifecycle After(SessionTurnKind kind, string? reason = null)
        {
            clock.Advance(TimeSpan.FromSeconds(5));
            head.Host.OnTurnChanged(slot, new SessionTurnEventArgs(kind, reason));
            registry.TryGet(id, out var record).ShouldBeTrue();
            record!.LastActivity.ShouldBe(clock.GetUtcNow());
            return record.Lifecycle;
        }

        After(SessionTurnKind.Started).ShouldBe(SessionLifecycle.Running);
        slot.TurnActive.ShouldBeTrue();
        After(SessionTurnKind.Completed).ShouldBe(SessionLifecycle.Completed);
        slot.TurnActive.ShouldBeFalse();
        After(SessionTurnKind.Started).ShouldBe(SessionLifecycle.Running);
        After(SessionTurnKind.Completed, "cancelled").ShouldBe(SessionLifecycle.Created);
        After(SessionTurnKind.Started).ShouldBe(SessionLifecycle.Running);
        After(SessionTurnKind.Faulted, "the engine failed").ShouldBe(SessionLifecycle.Failed);
    }

    /// <summary>The omp session identifier is recorded as soon as the engine reports it, and the planner's Graph identifier as soon as its record is written, found through the scope that follows the session.</summary>
    [Fact]
    public async Task TheNativeIdAndTheGraphIdReachTheRegistryAndTheObservers()
    {
        var ct = TestContext.Current.CancellationToken;
        using var tree = new TempTree();
        tree.WriteReadyPlugins();
        var head = AgentHead.Create(Options(tree, new ScriptedOmp()));
        await using var headLifetime = head.ConfigureAwait(true);
        await head.InitializeAsync(ct).ConfigureAwait(true);
        var (id, slot) = await NewSlotAsync(head, ct).ConfigureAwait(true);
        head.Host.Registry!.TryGet(id, out var created).ShouldBeTrue();
        created!.AcpSessionId.ShouldBeNull();
        using var observing = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var observation = Task.Run(
            async () =>
            {
                await foreach (var unused in slot.Stages.ObserveAsync(slot.Scope.Current, observing.Token).WithCancellation(observing.Token).ConfigureAwait(false))
                {
                    // The events are folded by the tap above the composite; this loop only keeps the observation running.
                }
            },
            CancellationToken.None);

        await SendAsync(slot, "hello", ct).ConfigureAwait(true);
        slot.Scope.Current.AcpSessionId.ShouldBe("native-1");
        head.Host.Registry!.TryGet(id, out var named).ShouldBeTrue();
        named!.AcpSessionId.ShouldBe("native-1");
        named.ResumeReference.ShouldBe("native-1");

        var graphId = "ut-mvp0bh59-2dd6ef4c";
        tree.Write(
            Path.GetRelativePath(tree.Root, SessionStateLayout.UltrathinkSessionFile(named.StateDirectory, "native-1")).Replace(Path.DirectorySeparatorChar, '/'),
            "{\"result\":{\"source\":\"llm\"},\"graph\":{\"nodes\":[{\"id\":\"a\"}]},\"plan\":{\"graphId\":\"" + graphId + "\"}}");
        await EventuallyAsync(
            () => head.Host.Registry!.TryGet(id, out var now) && string.Equals(now!.GraphId, graphId, StringComparison.Ordinal),
            "the Graph identifier in the registry",
            ct).ConfigureAwait(true);

        slot.Scope.Current.GraphId.ShouldBe(graphId);
        await observing.CancelAsync().ConfigureAwait(true);
        await observation.ConfigureAwait(true);
    }

    /// <summary>A turn that was running when the application ended is found lost at the next start, and reopening it resumes the stored omp session with <c>session/load</c> instead of creating a new one.</summary>
    [Fact]
    public async Task RecoverAfterRestartThenReopenResumesWithTheStoredNativeId()
    {
        var ct = TestContext.Current.CancellationToken;
        using var tree = new TempTree();
        string id;
        var first = new ScriptedOmp { HoldAfterFirstChunk = true };
        var firstHead = AgentHead.Create(Options(tree, first));
        await using (firstHead.ConfigureAwait(true))
        {
            await firstHead.InitializeAsync(ct).ConfigureAwait(true);
            SessionSlot slot;
            (id, slot) = await NewSlotAsync(firstHead, ct).ConfigureAwait(true);
            var turn = slot.Prompts.SubmitAsync("long work", ct).GetAsyncEnumerator(ct);
            await using (turn.ConfigureAwait(true))
            {
                (await turn.MoveNextAsync().ConfigureAwait(true)).ShouldBeTrue();
                firstHead.Host.OnTurnChanged(slot, new SessionTurnEventArgs(SessionTurnKind.Started));
                firstHead.Host.Registry!.TryGet(id, out var running).ShouldBeTrue();
                running!.Lifecycle.ShouldBe(SessionLifecycle.Running);
                running.ResumeReference.ShouldBe("native-1");
            }
        }

        var second = new ScriptedOmp();
        var secondHead = AgentHead.Create(Options(tree, second));
        await using var secondLifetime = secondHead.ConfigureAwait(true);
        await secondHead.InitializeAsync(ct).ConfigureAwait(true);
        secondHead.Host.Registry!.TryGet(id, out var lost).ShouldBeTrue();
        lost!.Lifecycle.ShouldBe(SessionLifecycle.Lost);
        var reopened = secondHead.Host.CreateSlot(id, null, null);
        reopened.Scope.Current.AcpSessionId.ShouldBe("native-1");

        await secondHead.Host.ReattachAsync(reopened, ct).ConfigureAwait(true);
        var events = await SendAsync(reopened, "continue", ct).ConfigureAwait(true);

        events.Last().ShouldBeOfType<CompletedEvent>();
        second.Requests("session/new").ShouldBeEmpty();
        var load = second.Requests("session/load").ShouldHaveSingleItem();
        load["params"]!["sessionId"]!.GetValue<string>().ShouldBe("native-1");
        second.Requests("session/prompt").ShouldHaveSingleItem()["params"]!["prompt"]![0]!["text"]!.GetValue<string>().ShouldBe("continue");
        secondHead.Host.Registry!.TryGet(id, out var resumed).ShouldBeTrue();
        resumed!.Lifecycle.ShouldBe(SessionLifecycle.Created);
        resumed.AcpSessionId.ShouldBe("native-1");
    }

    /// <summary>Closing stops the session's engine and records it Paused, a session that finished stays Completed, a prompt after the close is refused, and reopening resumes the same omp session on a new engine.</summary>
    [Fact]
    public async Task ClosingDisposesTheEngineAndReopeningResumesIt()
    {
        var ct = TestContext.Current.CancellationToken;
        using var tree = new TempTree();
        var omp = new ScriptedOmp();
        var head = AgentHead.Create(Options(tree, omp));
        await using var headLifetime = head.ConfigureAwait(true);
        await head.InitializeAsync(ct).ConfigureAwait(true);
        var (id, slot) = await NewSlotAsync(head, ct).ConfigureAwait(true);
        var (finishedId, finished) = await NewSlotAsync(head, ct).ConfigureAwait(true);
        await SendAsync(slot, "hello", ct).ConfigureAwait(true);
        await SendAsync(finished, "done soon", ct).ConfigureAwait(true);
        head.Host.OnTurnChanged(finished, new SessionTurnEventArgs(SessionTurnKind.Completed));
        omp.ClosedTransports.ShouldBe(0);

        await head.Host.CloseSessionAsync(id, ct).ConfigureAwait(true);
        await head.Host.CloseSessionAsync(finishedId, ct).ConfigureAwait(true);

        omp.ClosedTransports.ShouldBe(2);
        head.Host.IsClosed(id).ShouldBeTrue();
        head.Host.Registry!.TryGet(id, out var paused).ShouldBeTrue();
        paused!.Lifecycle.ShouldBe(SessionLifecycle.Paused);
        head.Host.Registry!.TryGet(finishedId, out var completed).ShouldBeTrue();
        completed!.Lifecycle.ShouldBe(SessionLifecycle.Completed);
        var refused = await Should.ThrowAsync<InvalidOperationException>(() => SendAsync(slot, "too late", ct)).ConfigureAwait(true);
        refused.Message.ShouldContain("closed");
        omp.Launches.Count.ShouldBe(2);

        await head.Host.ReattachAsync(slot, ct).ConfigureAwait(true);
        head.Host.IsClosed(id).ShouldBeFalse();
        await SendAsync(slot, "back again", ct).ConfigureAwait(true);

        omp.Launches.Count.ShouldBe(3);
        omp.Requests("session/load").ShouldHaveSingleItem()["params"]!["sessionId"]!.GetValue<string>().ShouldBe("native-1");
    }

    /// <summary>Disposing the head stops every engine and leaves the lifecycles as they were, so a running turn is found lost at the next start.</summary>
    [Fact]
    public async Task DisposingTheHeadStopsEveryEngine()
    {
        var ct = TestContext.Current.CancellationToken;
        using var tree = new TempTree();
        var omp = new ScriptedOmp();
        var head = AgentHead.Create(Options(tree, omp));
        await using var headScope = head.ConfigureAwait(true);
        await head.InitializeAsync(ct).ConfigureAwait(true);
        var (_, one) = await NewSlotAsync(head, ct).ConfigureAwait(true);
        var (_, two) = await NewSlotAsync(head, ct).ConfigureAwait(true);
        await SendAsync(one, "a", ct).ConfigureAwait(true);
        await SendAsync(two, "b", ct).ConfigureAwait(true);

        await head.DisposeAsync().ConfigureAwait(true);

        omp.ClosedTransports.ShouldBe(2);
        Should.Throw<ObjectDisposedException>(() => { _ = head.Host.CreateSlot("s-late", null, null); });
    }

    /// <summary>The maintenance tick keeps a running turn alive with a heartbeat, and marks a session that is Running without any signal as lost.</summary>
    [Fact]
    public async Task TheMaintenanceTickKeepsRunningTurnsAliveAndMarksSilentSessionsLost()
    {
        var ct = TestContext.Current.CancellationToken;
        using var tree = new TempTree();
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 10, 26, 12, 0, 0, TimeSpan.Zero));
        var head = AgentHead.Create(Options(tree, new ScriptedOmp(), clock: clock, maintenance: SessionEngineSetOptions.DefaultMaintenanceInterval));
        await using var headLifetime = head.ConfigureAwait(true);
        await head.InitializeAsync(ct).ConfigureAwait(true);
        var (activeId, active) = await NewSlotAsync(head, ct).ConfigureAwait(true);
        var (silentId, _) = await NewSlotAsync(head, ct).ConfigureAwait(true);
        head.Host.OnTurnChanged(active, new SessionTurnEventArgs(SessionTurnKind.Started));
        head.Host.Registry!.SetLifecycle(silentId, SessionLifecycle.Running);
        var tick = clock.Timers.Single(static timer => timer.Period == SessionEngineSetOptions.DefaultMaintenanceInterval);

        clock.Advance(TimeSpan.FromSeconds(50));
        tick.Fire();

        head.Host.Registry!.TryGet(activeId, out var kept).ShouldBeTrue();
        kept!.Lifecycle.ShouldBe(SessionLifecycle.Running);
        head.Host.Registry!.TryGet(silentId, out var silent).ShouldBeTrue();
        silent!.Lifecycle.ShouldBe(SessionLifecycle.Lost);

        await head.DisposeAsync().ConfigureAwait(true);
        tick.Disposed.ShouldBeTrue();
    }

    /// <summary>Reloading resolves the plugins again and re-reads the registry without losing a session.</summary>
    [Fact]
    public async Task ReloadingSeesNewPluginsAndKeepsTheSessions()
    {
        var ct = TestContext.Current.CancellationToken;
        using var tree = new TempTree();
        var head = AgentHead.Create(Options(tree, new ScriptedOmp()));
        await using var headLifetime = head.ConfigureAwait(true);
        await head.InitializeAsync(ct).ConfigureAwait(true);
        var (id, _) = await NewSlotAsync(head, ct).ConfigureAwait(true);
        head.Host.Plugins.Any(static plugin => plugin.Health == PluginHealth.Ready).ShouldBeFalse();

        tree.WriteReadyPlugins();
        await head.Host.ReloadAsync(ct).ConfigureAwait(true);

        head.Host.Plugins.Count(static plugin => plugin.Health == PluginHealth.Ready).ShouldBe(3, "the three enabled plugins are ready; the substrate is switched off");
        head.Host.Catalog.Snapshot().ShouldHaveSingleItem().Id.ShouldBe(id);
    }
}
