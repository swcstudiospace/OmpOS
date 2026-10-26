using OmpOS.Harness.AgentLoop.Launch;
using OmpOS.Harness.AgentLoop.Plugins;

namespace OmpOS.Harness.AgentLoop.Connection;

/// <summary>Options for <see cref="PluginConnectionStageSource"/>.</summary>
public sealed record PluginConnectionStageSourceOptions
{
    /// <summary>The default time between two looks at the connection report.</summary>
    public static readonly TimeSpan DefaultPollInterval = TimeSpan.FromMilliseconds(500);

    /// <summary>The default time omp is given to write its first report.</summary>
    public static readonly TimeSpan DefaultReportDeadline = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets the plugins that were launched with the session and so are expected to connect, in the order their stages are
    /// reported. A plugin the planner left out or the user switched off must not be listed.
    /// </summary>
    public required IReadOnlyList<OmpPluginDescriptor> Plugins { get; init; }

    /// <summary>Gets the time between two looks at the report. It must be positive.</summary>
    public TimeSpan PollInterval { get; init; } = DefaultPollInterval;

    /// <summary>Gets how long omp may take to write its first report before the stages turn degraded. It must be positive.</summary>
    public TimeSpan ReportDeadline { get; init; } = DefaultReportDeadline;

    /// <summary>
    /// Builds the options for a launch: the built-in plugins the plan loads, in composition order.
    /// </summary>
    /// <param name="plan">The launch plan of the session.</param>
    /// <returns>The options with the defaults for the intervals.</returns>
    public static PluginConnectionStageSourceOptions ForPlan(OmpLaunchPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return new PluginConnectionStageSourceOptions
        {
            Plugins =
            [
                .. OmpPluginCatalog.BuiltIn.Where(descriptor =>
                    plan.LoadedPluginIds.Contains(descriptor.Id, StringComparer.Ordinal)),
            ],
        };
    }
}
