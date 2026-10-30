using Autonogrammer.Contracts.Agents;
using Autonogrammer.Harness.AgentLoop.Launch;
using Autonogrammer.Harness.AgentLoop.Plugins;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.AgentLoop.Tests.Plugins;

/// <summary>
/// Verifies which plugins the availability source reports, with which state and detail, and that it then completes.
/// </summary>
public sealed class PluginAvailabilityStageSourceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static StageObservationScope Scope(string? graphId = null) => new()
    {
        SessionId = "s1",
        WorkspaceRoot = "/work",
        SessionStateDirectory = "/state/sessions/s1",
        GraphId = graphId,
    };

    private static ResolvedPlugin With(string id, PluginHealth health, string message) => new()
    {
        Descriptor = OmpPluginCatalog.BuiltIn.Single(plugin => string.Equals(plugin.Id, id, StringComparison.Ordinal)),
        AbsoluteDirectory = "/p/" + id,
        EntryPaths = [],
        Health = health,
        Message = message,
    };

    private static async Task<List<StageEvent>> CollectAsync(PluginAvailabilityStageSource source, StageObservationScope scope)
    {
        var events = new List<StageEvent>();
        await foreach (var stageEvent in source.ObserveAsync(scope, Token).ConfigureAwait(true))
        {
            events.Add(stageEvent);
        }

        return events;
    }

    [Fact]
    public async Task EmitsNothingForReadyAndForSwitchedOffPlugins()
    {
        var plugins = new[]
        {
            With("ultrathink", PluginHealth.Ready, "ok"),
            With("swarm", PluginHealth.Disabled, "off"),
            With("substrate", PluginHealth.SubmoduleNotInitialized, "not initialized but switched off"),
        };
        var settings = new AgentLoopSettings { PluginsRoot = "/p", StateRoot = "/s", SwarmEnabled = false, SubstrateEnabled = false };

        var events = await CollectAsync(new PluginAvailabilityStageSource(plugins, settings, new FixedTimeProvider(Now)), Scope()).ConfigureAwait(true);

        events.ShouldBeEmpty();
    }

    [Fact]
    public async Task EmitsOneEventPerEnabledPluginThatIsNotReadyWithItsMessageAsDetail()
    {
        var plugins = new[]
        {
            With("ultrathink", PluginHealth.SubmoduleNotInitialized, "run git submodule update --init --recursive plugins/claude-ultrathink"),
            With("swarm", PluginHealth.EntryMissing, "swarm entry missing"),
            With("substrate", PluginHealth.ManifestInvalid, "substrate entry leaves the plugin directory"),
            With("gsd", PluginHealth.Ready, "ok"),
        };
        var settings = new AgentLoopSettings { PluginsRoot = "/p", StateRoot = "/s", SubstrateEnabled = true };

        var events = await CollectAsync(new PluginAvailabilityStageSource(plugins, settings, new FixedTimeProvider(Now)), Scope("ut-1-abc")).ConfigureAwait(true);

        events.Select(e => (e.Source, e.Status, e.Detail)).ShouldBe(
        [
            (StageSource.Ultrathink, StageStatus.Unavailable, "run git submodule update --init --recursive plugins/claude-ultrathink"),
            (StageSource.Swarm, StageStatus.Degraded, "swarm entry missing"),
            (StageSource.Substrate, StageStatus.Degraded, "substrate entry leaves the plugin directory"),
        ]);
        events.ShouldAllBe(e => e.SessionId == "s1" && e.GraphId == "ut-1-abc" && e.Timestamp == Now && e.StageId == "plugin");
        events.Select(e => e.Title).ShouldBe(["Ultrathink plugin", "Agent Swarm plugin", "Agent Substrate plugin"]);
    }

    [Fact]
    public async Task CompletesWithoutEventsWhenCancelledBeforeObserving()
    {
        var plugins = new[] { With("ultrathink", PluginHealth.EntryMissing, "missing") };
        var settings = new AgentLoopSettings { PluginsRoot = "/p", StateRoot = "/s" };
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync().ConfigureAwait(true);
        var source = new PluginAvailabilityStageSource(plugins, settings, new FixedTimeProvider(Now));

        var events = new List<StageEvent>();
        await foreach (var stageEvent in source.ObserveAsync(Scope(), cancellation.Token).ConfigureAwait(true))
        {
            events.Add(stageEvent);
        }

        events.ShouldBeEmpty();
    }
}
