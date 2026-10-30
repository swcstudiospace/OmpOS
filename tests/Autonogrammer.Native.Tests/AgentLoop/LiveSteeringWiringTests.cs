using System.Runtime.Versioning;
using Autonogrammer.Harness.AgentLoop.Connection;
using Autonogrammer.Harness.AgentLoop.Launch;
using Autonogrammer.Harness.AgentLoop.Plugins;
using Autonogrammer.Harness.AgentLoop.Steering;
using Autonogrammer.Native.Agents;
using Autonogrammer.Native.Tests.RunControls;
using Autonogrammer.Native.Views.Sessions;
using Autonogrammer.Presentation.AgentPanel;
using Shouldly;
using Xunit;
using static Autonogrammer.Native.Tests.AgentLoop.AgentLoopFixtures;

namespace Autonogrammer.Native.Tests.AgentLoop;

/// <summary>
/// How the head wires every omp process to live steering and to the plugin connection report: a chat session gets the steering channel
/// of its own state directory and the connection stage source, the directories exist before omp launches, and the composer's send-now of
/// a running chat or terminal session reaches that session's inbox and is reported as omp acknowledged it.
/// </summary>
public sealed class LiveSteeringWiringTests
{
    private const string Text = "steer toward the failing test";

    /// <summary>A chat session's plan creates the steering, connection and Swarm directories itself, even for a record that never had them, and its stages carry one connection stage for every plugin the plan loads.</summary>
    [Fact]
    public async Task AChatPlanCreatesTheDirectoriesAndObservesThePluginConnection()
    {
        var ct = TestContext.Current.CancellationToken;
        using var tree = new TempTree();
        tree.WriteReadyPlugins();
        var head = AgentHead.Create(Options(tree, new ScriptedOmp()));
        await using var headLifetime = head.ConfigureAwait(true);
        await head.InitializeAsync(ct).ConfigureAwait(true);
        var record = head.Host.Registry!.Create(title: null, tree.Workspace);
        Directory.Exists(SessionStateLayout.SteerDirectory(record.StateDirectory)).ShouldBeFalse();
        var slot = head.Host.CreateSlot(record.Id, null, null);

        var plan = await slot.PlanAsync(ct).ConfigureAwait(true);
        var seen = await ObserveUntilAsync(
            slot,
            events => PluginIds.All(id => events.Any(e => string.Equals(e.StageId, PluginConnectionStageSource.StageIdPrefix + id, StringComparison.Ordinal))),
            ct).ConfigureAwait(true);

        Directory.Exists(Path.Combine(SessionStateLayout.SteerDirectory(record.StateDirectory), "inbox")).ShouldBeTrue();
        Directory.Exists(Path.Combine(SessionStateLayout.SteerDirectory(record.StateDirectory), "acks")).ShouldBeTrue();
        Directory.Exists(Path.Combine(SessionStateLayout.SteerDirectory(record.StateDirectory), "done")).ShouldBeTrue();
        Directory.Exists(Path.GetDirectoryName(SessionStateLayout.ConnectionFile(record.StateDirectory))!).ShouldBeTrue();
        Directory.Exists(SessionStateLayout.SwarmDirectory(record.StateDirectory)).ShouldBeTrue();
        plan.Launch.Environment[OmpLaunchPlanner.SteerDirectoryVariable].ShouldBe(SessionStateLayout.SteerDirectory(record.StateDirectory));
        plan.Launch.Environment[OmpLaunchPlanner.ConnectionFileVariable].ShouldBe(SessionStateLayout.ConnectionFile(record.StateDirectory));
        foreach (var id in PluginIds)
        {
            var stage = seen.First(e => string.Equals(e.StageId, PluginConnectionStageSource.StageIdPrefix + id, StringComparison.Ordinal));
            stage.Detail.ShouldBe(PluginConnectionStageSource.WaitingDetail);
        }

        slot.Steering.ShouldBeOfType<FileSteeringChannel>();
    }

    /// <summary>The composer of a running chat session sends now through the steering channel of that session: the directive reaches its inbox and the history says delivered because omp acknowledged it.</summary>
    [Fact]
    public async Task ASendNowFromARunningChatReachesItsInboxAndIsReportedDelivered()
    {
        var ct = TestContext.Current.CancellationToken;
        using var tree = new TempTree();
        var omp = new ScriptedOmp { HoldAfterFirstChunk = true };
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var head = AgentHead.Create(Options(tree, omp));
            try
            {
                await head.InitializeAsync(ct).ConfigureAwait(true);
                await head.Panel.NewSessionAsync(ct).ConfigureAwait(true);
                var selected = head.Panel.Selected.ShouldNotBeNull();
                var runtime = AgentSessionRuntime.For(selected).ShouldNotBeNull();
                head.Host.Registry!.TryGet(selected.Id, out var record).ShouldBeTrue();
                FakeSteerPlugin.WriteAlive(record!.StateDirectory, DateTimeOffset.UtcNow);
                selected.Steering.IsLive.ShouldBeTrue("the head passes the session's steering channel to its runtime");
                runtime.Composer.Draft = "long work";
                await runtime.Composer.SendAsync().ConfigureAwait(true);
                await RunControlsHarness.UntilAsync(() => selected.Steering.IsRunActive, "the turn to be running", ct).ConfigureAwait(true);

                var acknowledging = FakeSteerPlugin.AckNextAsync(record.StateDirectory, "delivered", null, ct);
                await selected.Steering.SubmitAsync(Text, SteeringMode.Steer, ct).ConfigureAwait(true);
                var directive = await acknowledging.ConfigureAwait(true);

                directive["mode"]!.GetValue<string>().ShouldBe("steer");
                directive["text"]!.GetValue<string>().ShouldBe(Text);
                selected.Steering.History.Single().DeliveryStatus.ShouldBe(SteeringDeliveryStatus.Delivered);
            }
            finally
            {
                await head.DisposeAsync().ConfigureAwait(true);
            }
        }, ct).ConfigureAwait(true);
    }

    /// <summary>A session whose omp is not serving keeps the text out of the history's delivered state: the send fails, the inbox stays empty and the session is unchanged.</summary>
    [Fact]
    public async Task ASendNowToAChatWhoseOmpIsNotServingQueuesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        using var tree = new TempTree();
        var omp = new ScriptedOmp { HoldAfterFirstChunk = true };
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var head = AgentHead.Create(Options(tree, omp));
            try
            {
                await head.InitializeAsync(ct).ConfigureAwait(true);
                await head.Panel.NewSessionAsync(ct).ConfigureAwait(true);
                var selected = head.Panel.Selected.ShouldNotBeNull();
                var runtime = AgentSessionRuntime.For(selected).ShouldNotBeNull();
                head.Host.Registry!.TryGet(selected.Id, out var record).ShouldBeTrue();
                runtime.Composer.Draft = "long work";
                await runtime.Composer.SendAsync().ConfigureAwait(true);
                await RunControlsHarness.UntilAsync(() => selected.Steering.IsRunActive, "the turn to be running", ct).ConfigureAwait(true);

                await selected.Steering.SubmitAsync(Text, SteeringMode.Steer, ct).ConfigureAwait(true);

                FakeSteerPlugin.Directives(record!.StateDirectory).ShouldBeEmpty();
                selected.Steering.History.Where(static entry => entry.DeliveryStatus == SteeringDeliveryStatus.Delivered).ShouldBeEmpty();
            }
            finally
            {
                await head.DisposeAsync().ConfigureAwait(true);
            }
        }, ct).ConfigureAwait(true);
    }

    /// <summary>The composer of a running omp terminal steers through the same channel: the terminal's omp is the run, and a send now reaches the inbox of the terminal's own session.</summary>
    [Fact]
    [SupportedOSPlatform("linux")]
    public async Task ASendNowFromARunningTerminalReachesItsInboxAndIsReportedDelivered()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("The fake omp is a POSIX shell script.");
        }

        var ct = TestContext.Current.CancellationToken;
        using var tree = new TempTree();
        var fake = FakeTerminalOmp.Waiting(tree);
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var head = AgentHead.Create(Options(tree, omp: null, ompPath: fake.Path));
            try
            {
                await head.InitializeAsync(ct).ConfigureAwait(true);
                await head.Panel.NewTerminalAsync(ct).ConfigureAwait(true);
                var selected = head.Panel.Selected.ShouldNotBeNull();
                var terminal = AgentSessionRuntime.For(selected).ShouldNotBeNull().Terminal.ShouldNotBeNull();
                head.Host.Registry!.TryGet(selected.Id, out var record).ShouldBeTrue();
                FakeSteerPlugin.WriteAlive(record!.StateDirectory, DateTimeOffset.UtcNow);
                await terminal.StartAsync().ConfigureAwait(true);
                await RunControlsHarness.UntilAsync(() => selected.Steering.IsRunActive, "omp to be running in the terminal", ct).ConfigureAwait(true);

                var acknowledging = FakeSteerPlugin.AckNextAsync(record.StateDirectory, "delivered", null, ct);
                await selected.Steering.SubmitAsync(Text, SteeringMode.Steer, ct).ConfigureAwait(true);
                var directive = await acknowledging.ConfigureAwait(true);

                directive["text"]!.GetValue<string>().ShouldBe(Text);
                selected.Steering.History.Single().DeliveryStatus.ShouldBe(SteeringDeliveryStatus.Delivered);
            }
            finally
            {
                await head.DisposeAsync().ConfigureAwait(true);
            }
        }, ct).ConfigureAwait(true);
    }

    private static IEnumerable<string> PluginIds =>
        [OmpPluginCatalog.UltrathinkId, OmpPluginCatalog.SwarmId, OmpPluginCatalog.GsdId];
}
