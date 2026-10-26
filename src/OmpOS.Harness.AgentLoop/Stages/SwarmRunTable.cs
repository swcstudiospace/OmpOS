using System.Globalization;
using System.Text.RegularExpressions;
using OmpOS.Contracts.Agents;

namespace OmpOS.Harness.AgentLoop.Stages;

/// <summary>
/// The runs of one swarm as the observer has pieced them together from <c>live.json</c> and <c>events.jsonl</c>, and their
/// projection to stage snapshots. Pure: it does no I/O, so tests feed it parsed documents. Runs only move forward (see
/// <see cref="SwarmRunStates.Rank"/>) and a finished run never changes, so the order in which the two files are read does not
/// matter. Only identifiers, states, counters and short titles are kept; no free text from the files is ever shown.
/// </summary>
internal sealed partial class SwarmRunTable
{
    /// <summary>The stage identifier of the orchestrator row.</summary>
    public const string SwarmStageId = "swarm";

    /// <summary>The prefix of a child stage identifier.</summary>
    public const string AgentStagePrefix = "agent:";

    /// <summary>The most runs tracked. A finished run is dropped to make room; when none is finished a new run is not tracked.</summary>
    public const int MaxRuns = 256;

    private const string SwarmTitle = "Subagents";
    private const int MaxLabelLength = 64;

    private readonly OrderedDictionary<string, SwarmRun> _runs = new(StringComparer.Ordinal);

    /// <summary>Gets the number of runs tracked.</summary>
    public int Count => _runs.Count;

    /// <summary>Applies the runs of a <c>live.json</c> snapshot.</summary>
    /// <param name="document">The parsed document. Its schema was already checked.</param>
    public void ApplyLive(SwarmLiveDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        foreach (var run in document.Runs ?? [])
        {
            if (run is not null)
            {
                Update(run.RunId, run.Agent, run.State, run.Stalled, run.Error);
            }
        }
    }

    /// <summary>Applies one parsed <c>events.jsonl</c> line. Event types the observer does not use are ignored.</summary>
    /// <param name="record">The parsed line.</param>
    public void ApplyEvent(SwarmEventRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var payload = record.Payload;
        if (payload is null || record.Type is null)
        {
            return;
        }

        switch (record.Type)
        {
            case "swarm.run.queued":
                Update(payload.RunId, payload.Agent, "queued", null, null);
                break;
            case "swarm.run.started":
                Update(payload.RunId, payload.Agent, "starting", null, null);
                break;
            case "swarm.run.state":
                Update(payload.RunId, payload.Agent, payload.To, null, null);
                break;
            case "swarm.run.heartbeat":
                Update(payload.RunId, payload.Agent, payload.State, payload.Stalled, null);
                break;
            case "swarm.run.stalled":
                if (payload.RunId is not null && _runs.TryGetValue(payload.RunId, out var stalledRun) && !SwarmRunStates.IsTerminal(stalledRun.Phase))
                {
                    stalledRun.Stalled = true;
                }

                break;
            case "swarm.run.finished":
                Update(payload.RunId, payload.Agent, payload.State, false, payload.ErrorCode);
                break;
            case "subagent.spawn":
                Update(payload.SubagentId, payload.SubagentId, "running", null, null);
                break;
            case "subagent.complete":
                Update(payload.SubagentId, payload.SubagentId, "completed", false, null);
                break;
            case "subagent.status.stream":
                Update(payload.SubagentId, payload.SubagentId, payload.Status ?? payload.State, null, null);
                break;
            default:
                break;
        }
    }

    /// <summary>Projects the runs to the stage snapshots of one poll: the orchestrator row first, then one child row per run.</summary>
    /// <param name="filesSeen">True when at least one of the two files exists.</param>
    /// <param name="liveUnreadable">True when <c>live.json</c> exists but cannot be used.</param>
    /// <param name="graphId">The Graph identifier of the session, or null.</param>
    /// <returns>The snapshots.</returns>
    public List<StageSnapshot> Project(bool filesSeen, bool liveUnreadable, string? graphId)
    {
        var snapshots = new List<StageSnapshot>(_runs.Count + 1);
        if (_runs.Count == 0)
        {
            if (liveUnreadable)
            {
                snapshots.Add(new StageSnapshot(SwarmStageId, SwarmTitle, StageStatus.Degraded, "Swarm status file could not be read", null, graphId));
            }
            else if (filesSeen)
            {
                snapshots.Add(new StageSnapshot(SwarmStageId, SwarmTitle, StageStatus.Pending, "No subagents have started yet", null, graphId));
            }
            else
            {
                snapshots.Add(new StageSnapshot(SwarmStageId, SwarmTitle, StageStatus.Pending, "Orchestrator has not started", null, graphId));
            }

            return snapshots;
        }

        var counters = Tally();
        snapshots.Add(ProjectParent(counters, graphId));
        foreach (var run in _runs.Values)
        {
            snapshots.Add(ProjectChild(run));
        }

        return snapshots;
    }

    private static StageSnapshot ProjectChild(SwarmRun run)
    {
        var detail = SwarmRunStates.Label(run.Phase) ?? (string.IsNullOrEmpty(run.RawState) ? "Running" : run.RawState);
        if (run.Stalled && !SwarmRunStates.IsTerminal(run.Phase))
        {
            detail += " (stalled)";
        }

        if (run.ErrorCode is not null && run.Phase is SwarmRunPhase.Failed or SwarmRunPhase.TimedOut)
        {
            detail += " [" + run.ErrorCode + "]";
        }

        return new StageSnapshot(
            AgentStagePrefix + run.Id,
            run.Label,
            SwarmRunStates.ToStatus(run.Phase),
            StageText.Clean(detail),
            SwarmStageId);
    }

    private static string Describe(RunCounters counters)
    {
        var text = string.Create(
            CultureInfo.InvariantCulture,
            $"{counters.Running} running, {counters.Queued} queued, {counters.Completed} completed, {counters.Failed} failed");
        if (counters.Waiting > 0)
        {
            text += string.Create(CultureInfo.InvariantCulture, $", {counters.Waiting} waiting");
        }

        if (counters.Cancelled > 0)
        {
            text += string.Create(CultureInfo.InvariantCulture, $", {counters.Cancelled} cancelled");
        }

        if (counters.Stalled > 0)
        {
            text += string.Create(CultureInfo.InvariantCulture, $", {counters.Stalled} stalled");
        }

        return text;
    }

    private static StageSnapshot ProjectParent(RunCounters counters, string? graphId)
    {
        if (counters.Active > 0)
        {
            return new StageSnapshot(SwarmStageId, SwarmTitle, StageStatus.Running, Describe(counters), null, graphId);
        }

        if (counters.Failed > 0)
        {
            var failed = string.Create(CultureInfo.InvariantCulture, $"Subagents failed: {counters.Failed} of {counters.Total}");
            return new StageSnapshot(SwarmStageId, SwarmTitle, StageStatus.Degraded, failed, null, graphId);
        }

        var finished = string.Create(CultureInfo.InvariantCulture, $"Subagents finished: {counters.Total}");
        if (counters.Cancelled > 0)
        {
            finished += string.Create(CultureInfo.InvariantCulture, $", {counters.Cancelled} cancelled");
        }

        return new StageSnapshot(SwarmStageId, SwarmTitle, StageStatus.Succeeded, finished, null, graphId);
    }

    private RunCounters Tally()
    {
        var counters = new RunCounters();
        foreach (var run in _runs.Values)
        {
            counters.Total++;
            switch (run.Phase)
            {
                case SwarmRunPhase.Waiting:
                    counters.Waiting++;
                    counters.Active++;
                    break;
                case SwarmRunPhase.Queued:
                    counters.Queued++;
                    counters.Active++;
                    break;
                case SwarmRunPhase.Completed:
                    counters.Completed++;
                    break;
                case SwarmRunPhase.Failed:
                case SwarmRunPhase.TimedOut:
                    counters.Failed++;
                    break;
                case SwarmRunPhase.Cancelled:
                    counters.Cancelled++;
                    break;
                default:
                    counters.Running++;
                    counters.Active++;
                    if (run.Stalled)
                    {
                        counters.Stalled++;
                    }

                    break;
            }
        }

        return counters;
    }

    private void Update(string? id, string? agent, string? rawState, bool? stalled, string? error)
    {
        if (string.IsNullOrEmpty(id) || string.IsNullOrWhiteSpace(rawState) || !IdPattern().IsMatch(id))
        {
            return;
        }

        var phase = SwarmRunStates.Parse(rawState);
        if (!_runs.TryGetValue(id, out var run))
        {
            if (_runs.Count >= MaxRuns && !EvictFinished())
            {
                return;
            }

            run = new SwarmRun(id);
            _runs.Add(id, run);
        }
        else if (SwarmRunStates.IsTerminal(run.Phase))
        {
            return;
        }

        var label = StageText.Clean(agent, MaxLabelLength);
        if (label.Length > 0)
        {
            run.Label = label;
        }

        if (SwarmRunStates.Rank(phase) < SwarmRunStates.Rank(run.Phase))
        {
            return;
        }

        run.Phase = phase;
        run.RawState = phase == SwarmRunPhase.Unknown ? StageText.Clean(rawState, MaxLabelLength) : null;
        if (stalled is not null)
        {
            run.Stalled = stalled.Value && !SwarmRunStates.IsTerminal(phase);
        }
        else if (SwarmRunStates.IsTerminal(phase))
        {
            run.Stalled = false;
        }

        if (error is not null && ErrorCodePattern().IsMatch(error))
        {
            run.ErrorCode = error;
        }
    }

    private bool EvictFinished()
    {
        foreach (var (key, run) in _runs)
        {
            if (SwarmRunStates.IsTerminal(run.Phase))
            {
                _runs.Remove(key);
                return true;
            }
        }

        return false;
    }

    [GeneratedRegex(@"\A[A-Za-z0-9][A-Za-z0-9._-]{0,127}\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 250)]
    private static partial Regex IdPattern();

    [GeneratedRegex(@"\AE-[A-Z]{2,24}\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 250)]
    private static partial Regex ErrorCodePattern();

    private sealed class RunCounters
    {
        public int Total { get; set; }

        public int Active { get; set; }

        public int Running { get; set; }

        public int Queued { get; set; }

        public int Waiting { get; set; }

        public int Completed { get; set; }

        public int Failed { get; set; }

        public int Cancelled { get; set; }

        public int Stalled { get; set; }
    }
}
