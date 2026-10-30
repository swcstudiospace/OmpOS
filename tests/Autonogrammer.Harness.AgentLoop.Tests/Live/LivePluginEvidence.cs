using System.Globalization;
using Autonogrammer.Contracts.Agents;
using Autonogrammer.Harness.AgentLoop.Connection;
using Autonogrammer.Harness.AgentLoop.Launch;
using Autonogrammer.Harness.AgentLoop.Plugins;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.AgentLoop.Tests.Live;

/// <summary>
/// The shared part of the live proofs that the four plugins are connected in a real omp process: it plans the launch of all
/// four plugins in a sandbox, waits for the connection report and the steering heartbeat the omp plugin writes, runs
/// <see cref="PluginConnectionStageSource"/> over the report exactly as the head does, and asserts the result. It never starts
/// omp and never sends a prompt.
/// </summary>
internal static class LivePluginEvidence
{
    /// <summary>The oldest <c>lastSeen</c> the heartbeat may carry and still count as serving (the omp plugin's own contract).</summary>
    public static readonly TimeSpan MaxHeartbeatAge = TimeSpan.FromSeconds(15);

    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan StageObservationWindow = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan HeartbeatTimeout = TimeSpan.FromSeconds(20);

    /// <summary>
    /// Plans the launch of every built-in plugin in the sandbox: isolated home, state below the sandbox, and the substrate
    /// switched on against an address nothing listens on (the status extension then degrades softly, and no real substrate is
    /// reached). The test is skipped when a plugin submodule is not initialised.
    /// </summary>
    /// <param name="sandbox">The sandbox of the run.</param>
    /// <param name="sessionId">The session identifier of the plan.</param>
    /// <returns>The launch plan.</returns>
    public static OmpLaunchPlan PlanLaunch(LiveSandbox sandbox, string sessionId)
    {
        ArgumentNullException.ThrowIfNull(sandbox);
        var settings = new AgentLoopSettings
        {
            PluginsRoot = Path.Combine(LiveSandbox.RepositoryRoot(), "plugins"),
            StateRoot = sandbox.StateRoot,
            UltrathinkEnabled = true,
            SwarmEnabled = true,
            SubstrateEnabled = true,
            SubstrateEndpoint = new Uri("http://127.0.0.1:9", UriKind.Absolute),
            GsdEnabled = true,
            Profile = AgentLoopProfile.Isolated,
            IsolatedHomeDirectory = sandbox.OmpHome,
        };

        var plugins = settings.ResolveBuiltInPlugins();
        foreach (var plugin in plugins.Where(plugin => settings.IsEnabled(plugin.Descriptor.Id)))
        {
            if (plugin.Health == PluginHealth.SubmoduleNotInitialized)
            {
                Assert.Skip($"{plugin.Message} Fix: {plugin.Descriptor.OptionalFixCommand}");
            }

            plugin.Health.ShouldBe(PluginHealth.Ready, $"{plugin.Descriptor.Id}: {plugin.Message}");
        }

        var plan = OmpLaunchPlanner.Plan(settings, plugins, sessionId, sandbox.Workspace, graphId: null);
        plan.LoadedPluginIds.ShouldBe(
            OmpPluginCatalog.BuiltIn.Select(static descriptor => descriptor.Id).ToArray(),
            customMessage: "the plan must load all four plugins in composition order: " + string.Join("; ", plan.PlanningEvents.Select(static e => e.Detail)));
        plan.Environment.ContainsKey(OmpLaunchPlanner.SteerDirectoryVariable).ShouldBeTrue("the plan sets no AUTONOGRAMMER_STEER_DIR.");
        plan.Environment.ContainsKey(OmpLaunchPlanner.ConnectionFileVariable).ShouldBeTrue("the plan sets no AUTONOGRAMMER_CONNECTION_FILE.");
        return plan;
    }

    /// <summary>
    /// Waits for the connection report and the steering heartbeat of a running omp, then runs
    /// <see cref="PluginConnectionStageSource"/> over the session's state directory until every plugin has left the pending state.
    /// </summary>
    /// <param name="plan">The launch plan the omp process runs with.</param>
    /// <param name="sandbox">The sandbox of the run.</param>
    /// <param name="sessionId">The session identifier the plan was made for.</param>
    /// <param name="reportTimeout">How long omp may take to write its first report.</param>
    /// <param name="cancellationToken">Cancels the waits.</param>
    /// <returns>What the run showed.</returns>
    public static async Task<LivePluginObservation> ObserveAsync(
        OmpLaunchPlan plan,
        LiveSandbox sandbox,
        string sessionId,
        TimeSpan reportTimeout,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(sandbox);

        var sessionDirectory = SessionStateLayout.SessionDirectory(sandbox.StateRoot, sessionId);
        var reportPath = plan.Environment[OmpLaunchPlanner.ConnectionFileVariable];
        var steerDirectory = plan.Environment[OmpLaunchPlanner.SteerDirectoryVariable];
        reportPath.ShouldBe(SessionStateLayout.ConnectionFile(sessionDirectory), "the plan's report path is not the one the stage source reads.");
        steerDirectory.ShouldBe(SessionStateLayout.SteerDirectory(sessionDirectory), "the plan's steering directory is not the session's.");

        var report = await WaitForReportAsync(reportPath, reportTimeout, cancellationToken).ConfigureAwait(true);
        var stages = await ObserveStagesAsync(plan, sessionDirectory, sessionId, sandbox.Workspace, cancellationToken).ConfigureAwait(true);
        var (heartbeat, age) = await WaitForHeartbeatAsync(Path.Combine(steerDirectory, LiveHeartbeat.FileName), cancellationToken).ConfigureAwait(true);
        return new LivePluginObservation(report, stages, heartbeat, age);
    }

    /// <summary>
    /// Asserts that every plugin is connected and that the report and the heartbeat come from one omp process in the expected mode.
    /// </summary>
    /// <param name="observation">What the run showed.</param>
    /// <param name="expectedMode">The run mode omp reports: <c>rpc</c> for ACP, <c>tui</c> for the terminal.</param>
    public static void AssertConnected(LivePluginObservation observation, string expectedMode)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedMode);

        foreach (var descriptor in OmpPluginCatalog.BuiltIn)
        {
            var stageId = PluginConnectionStageSource.StageIdPrefix + descriptor.Id;
            var stage = observation.Stages.SingleOrDefault(candidate => string.Equals(candidate.StageId, stageId, StringComparison.Ordinal));
            stage.ShouldNotBeNull($"no connection stage was reported for {descriptor.DisplayName}.");
            stage.Source.ShouldBe(descriptor.StageSource);
            stage.Status.ShouldBe(StageStatus.Succeeded, $"{descriptor.DisplayName} is not connected: {stage.Detail}");
        }

        observation.Report.CommandsAvailable.ShouldBeTrue("omp could not list its commands, so the commands were not checked.");
        observation.Report.Mode.ShouldBe(expectedMode, "the connection report names another run mode.");
        observation.Heartbeat.Mode.ShouldBe(expectedMode, "the heartbeat names another run mode.");
        observation.Report.Pid.ShouldNotBeNull("the connection report names no process.");
        observation.Heartbeat.Pid.ShouldBe(observation.Report.Pid, "the report and the heartbeat come from different processes.");
        observation.HeartbeatAge.ShouldBeLessThanOrEqualTo(MaxHeartbeatAge, "the heartbeat is stale.");
    }

    private static async Task<PluginConnectionReport> WaitForReportAsync(string path, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var deadline = TimeProvider.System.GetUtcNow() + timeout;
        while (true)
        {
            var bytes = TryRead(path);
            if (bytes is not null && PluginConnectionReport.TryParse(bytes) is { } report)
            {
                return report;
            }

            if (TimeProvider.System.GetUtcNow() >= deadline)
            {
                var state = bytes is null ? "the file does not exist" : "the file exists but is not a readable report";
                Assert.Fail(string.Create(CultureInfo.InvariantCulture, $"omp wrote no connection report within {timeout.TotalSeconds:F0} s: {state}."));
            }

            await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(true);
        }
    }

    private static async Task<(LiveHeartbeat Heartbeat, TimeSpan Age)> WaitForHeartbeatAsync(string path, CancellationToken cancellationToken)
    {
        var deadline = TimeProvider.System.GetUtcNow() + HeartbeatTimeout;
        var state = "the file does not exist";
        while (true)
        {
            var bytes = TryRead(path);
            if (bytes is not null)
            {
                var heartbeat = LiveHeartbeat.TryParse(bytes);
                if (heartbeat is null)
                {
                    state = "the file is not a readable heartbeat";
                }
                else
                {
                    var age = TimeProvider.System.GetUtcNow() - heartbeat.LastSeen;
                    if (age <= MaxHeartbeatAge)
                    {
                        return (heartbeat, age);
                    }

                    state = string.Create(CultureInfo.InvariantCulture, $"the latest lastSeen is {age.TotalSeconds:F1} s old");
                }
            }

            if (TimeProvider.System.GetUtcNow() >= deadline)
            {
                Assert.Fail(string.Create(CultureInfo.InvariantCulture, $"omp's steering heartbeat was not fresh within {HeartbeatTimeout.TotalSeconds:F0} s: {state}."));
            }

            await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(true);
        }
    }

    private static async Task<IReadOnlyList<StageEvent>> ObserveStagesAsync(
        OmpLaunchPlan plan,
        string sessionDirectory,
        string sessionId,
        string workspace,
        CancellationToken cancellationToken)
    {
        var options = PluginConnectionStageSourceOptions.ForPlan(plan);
        var source = new PluginConnectionStageSource(options);
        var scope = new StageObservationScope
        {
            SessionId = sessionId,
            WorkspaceRoot = workspace,
            SessionStateDirectory = sessionDirectory,
        };

        var latest = new Dictionary<string, StageEvent>(StringComparer.Ordinal);
        using var window = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        window.CancelAfter(StageObservationWindow);
        await foreach (var stageEvent in source.ObserveAsync(scope, window.Token).ConfigureAwait(true))
        {
            latest[stageEvent.StageId] = stageEvent;
            if (options.Plugins.All(plugin =>
                latest.TryGetValue(PluginConnectionStageSource.StageIdPrefix + plugin.Id, out var stage) && stage.Status != StageStatus.Pending))
            {
                break;
            }
        }

        return
        [
            .. options.Plugins
                .Select(plugin => latest.GetValueOrDefault(PluginConnectionStageSource.StageIdPrefix + plugin.Id))
                .OfType<StageEvent>(),
        ];
    }

    private static byte[]? TryRead(string path)
    {
        try
        {
            return File.ReadAllBytes(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The file does not exist yet or is being replaced: the caller polls again.
            return null;
        }
    }
}
