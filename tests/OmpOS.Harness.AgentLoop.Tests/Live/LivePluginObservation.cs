using System.Globalization;
using OmpOS.Contracts.Agents;
using OmpOS.Harness.AgentLoop.Connection;

namespace OmpOS.Harness.AgentLoop.Tests.Live;

/// <summary>
/// What a live run showed about the four plugins: the connection report omp's plugin wrote, the stage each plugin got from
/// <see cref="PluginConnectionStageSource"/> over that report, and the steering heartbeat.
/// </summary>
/// <param name="Report">The connection report the omp process wrote.</param>
/// <param name="Stages">The latest connection stage of every plugin the plan loads, in composition order.</param>
/// <param name="Heartbeat">The steering heartbeat.</param>
/// <param name="HeartbeatAge">How old <c>lastSeen</c> of <paramref name="Heartbeat"/> was when it was read.</param>
internal sealed record LivePluginObservation(
    PluginConnectionReport Report,
    IReadOnlyList<StageEvent> Stages,
    LiveHeartbeat Heartbeat,
    TimeSpan HeartbeatAge)
{
    /// <summary>Describes the observation in one block of lines, with counts only and no report content beyond them.</summary>
    /// <returns>The description.</returns>
    public string Describe() => string.Join(
        Environment.NewLine,
        string.Create(
            CultureInfo.InvariantCulture,
            $"connection report: mode={Report.Mode ?? "null"} pid={Report.Pid?.ToString(CultureInfo.InvariantCulture) ?? "null"} tools={Report.Tools.Count} commands={Report.Commands.Count} commandsAvailable={Report.CommandsAvailable}"),
        "stages: " + string.Join(", ", Stages.Select(stage => stage.StageId + "=" + stage.Status)),
        string.Create(
            CultureInfo.InvariantCulture,
            $"heartbeat: mode={Heartbeat.Mode ?? "null"} pid={Heartbeat.Pid?.ToString(CultureInfo.InvariantCulture) ?? "null"} lastSeen age={HeartbeatAge.TotalSeconds:F1} s"));
}
