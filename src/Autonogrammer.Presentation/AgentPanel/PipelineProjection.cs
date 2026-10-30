using System.Globalization;
using Autonogrammer.Contracts.Agents;

namespace Autonogrammer.Presentation.AgentPanel;

/// <summary>
/// Derives the Pipeline tab blocks, the pipeline strip pills and the run-summary clauses from the folded stage entries. The projection is
/// pure: the same entries give the same snapshot, which keeps <see cref="StageTimelineModel"/> a thin fold.
/// </summary>
internal static class PipelineProjection
{
    /// <summary>The top-level statuses in the order the roll-up prefers them: the worst first.</summary>
    private static readonly StageStatus[] RollUpOrder =
    [
        StageStatus.Failed,
        StageStatus.Unavailable,
        StageStatus.Degraded,
        StageStatus.Running,
        StageStatus.Pending,
        StageStatus.Succeeded,
        StageStatus.Skipped,
    ];

    /// <summary>Gets the sources in pipeline order, as the Pipeline tab shows them: the engine, then the four plugin sources.</summary>
    internal static IReadOnlyList<StageSource> Order { get; } =
        [StageSource.Omp, StageSource.Ultrathink, StageSource.Swarm, StageSource.Substrate, StageSource.Gsd];

    /// <summary>Gets the plugin sources in pipeline order, which the strip shows as pills.</summary>
    internal static IReadOnlyList<StageSource> PluginSources { get; } =
        [StageSource.Ultrathink, StageSource.Swarm, StageSource.Substrate, StageSource.Gsd];

    /// <summary>Returns whether the source can be re-checked by the Retry stage control.</summary>
    /// <param name="source">The source.</param>
    /// <returns>True for the four plugin sources.</returns>
    internal static bool IsRecheckable(StageSource source) => source is StageSource.Ultrathink or StageSource.Swarm or StageSource.Substrate or StageSource.Gsd;

    /// <summary>Returns the block header of a source.</summary>
    /// <param name="source">The source. An undefined value is <c>Other</c>.</param>
    /// <returns>The display name.</returns>
    internal static string NameOf(StageSource source) => source switch
    {
        StageSource.Omp => "Engine",
        StageSource.Ultrathink => "Ultrathink",
        StageSource.Swarm => "Swarm",
        StageSource.Substrate => "Substrate",
        StageSource.Gsd => "GSD",
        _ => "Other",
    };

    /// <summary>Projects the entries.</summary>
    /// <param name="entries">The folded entries, at most one per key.</param>
    /// <param name="canRetry">Whether a re-check handler exists.</param>
    /// <param name="phase">The workflow phase the Gsd pill shows, or null when unknown.</param>
    /// <returns>The snapshot.</returns>
    internal static PipelineSnapshot Project(IEnumerable<StageEntry> entries, bool canRetry, GsdPhaseInfo? phase)
    {
        var all = entries.ToList();
        var blocks = new List<StageSourceBlock>();
        foreach (var source in Order)
        {
            blocks.Add(BuildBlock(source, all.Where(entry => entry.Latest.Source == source), canRetry));
        }

        var unknown = all.Where(entry => !Enum.IsDefined(entry.Latest.Source)).ToList();
        if (unknown.Count > 0)
        {
            blocks.Add(BuildBlock(StageTimelineModel.OtherSource, unknown, canRetry));
        }

        var pills = new List<PipelinePill>();
        var clauses = new List<KeyValuePair<StageSource, string>>();
        var degraded = 0;
        var allUnavailable = true;
        foreach (var source in PluginSources)
        {
            var block = blocks.First(candidate => candidate.Source == source);
            var swarm = SwarmCounts(block);
            pills.Add(Pill(source, block.RollUpStatus, swarm, phase));
            if (block.RollUpStatus is { } status)
            {
                clauses.Add(new KeyValuePair<StageSource, string>(source, Clause(source, status, swarm, phase)));
            }

            allUnavailable &= block.RollUpStatus == StageStatus.Unavailable;
            degraded += block.Rows.Count(row => row.Depth == 0 && row.IsStatusKnown && IsDegradedKind(row.Status));
        }

        return new PipelineSnapshot(blocks, pills, clauses, degraded, allUnavailable);
    }

    private static bool IsDegradedKind(StageStatus status) =>
        status is StageStatus.Skipped or StageStatus.Degraded or StageStatus.Failed or StageStatus.Unavailable;

    private static StageSourceBlock BuildBlock(StageSource source, IEnumerable<StageEntry> entries, bool canRetry)
    {
        var ordered = entries.OrderBy(entry => entry.FirstSeen).ThenBy(entry => entry.Sequence).ToList();
        var rows = ordered.Count == 0 ? new List<StageRow>() : BuildRows(ordered, canRetry);
        var rollUp = RollUp(rows);
        var hasRows = rows.Count > 0;
        return new StageSourceBlock
        {
            Source = source,
            Name = NameOf(source),
            Rows = rows,
            RollUpStatus = rollUp,
            RollUpText = hasRows ? (rollUp is { } known ? AgentPanelCopy.StageStatusText(known) : "Unknown") : null,
            RollUpIconKey = hasRows ? (rollUp is { } status ? AgentPanelCopy.StageStatusIcon(status) : StatusIcon.Info) : null,
            EmptyText = hasRows ? null : AgentPanelCopy.WaitingForEvents,
        };
    }

    private static List<StageRow> BuildRows(List<StageEntry> ordered, bool canRetry)
    {
        var keys = new HashSet<(StageSource, string)>(ordered.Select(Key));
        var children = new Dictionary<(StageSource, string), List<StageEntry>>();
        var roots = new List<StageEntry>();
        foreach (var entry in ordered)
        {
            if (!HasParent(entry, keys))
            {
                roots.Add(entry);
                continue;
            }

            var parentKey = (entry.Latest.Source, entry.Latest.ParentStageId!);
            if (!children.TryGetValue(parentKey, out var siblings))
            {
                siblings = [];
                children[parentKey] = siblings;
            }

            siblings.Add(entry);
        }

        var rows = new List<StageRow>(ordered.Count);
        var visited = new HashSet<(StageSource, string)>();

        void Visit(StageEntry entry, int depth)
        {
            var key = Key(entry);
            if (!visited.Add(key))
            {
                return;
            }

            rows.Add(ToRow(entry, depth, canRetry));
            if (!children.TryGetValue(key, out var below))
            {
                return;
            }

            foreach (var child in below)
            {
                Visit(child, Math.Min(depth + 1, StageTimelineModel.MaxDepth));
            }
        }

        foreach (var root in roots)
        {
            Visit(root, 0);
        }

        // Entries that name each other as parents have no root above them; they are shown as top-level rows rather than dropped.
        foreach (var entry in ordered)
        {
            Visit(entry, 0);
        }

        return rows;
    }

    private static (StageSource Source, string StageId) Key(StageEntry entry) => (entry.Latest.Source, entry.Latest.StageId);

    private static bool HasParent(StageEntry entry, HashSet<(StageSource, string)> keys)
    {
        var parent = entry.Latest.ParentStageId;
        return !string.IsNullOrEmpty(parent)
            && !string.Equals(parent, entry.Latest.StageId, StringComparison.Ordinal)
            && keys.Contains((entry.Latest.Source, parent));
    }

    private static StageRow ToRow(StageEntry entry, int depth, bool canRetry)
    {
        var stage = entry.Latest;
        var known = Enum.IsDefined(stage.Status);
        var title = PanelText.Plain(stage.Title, StageTimelineModel.MaxTitleChars);
        if (title.Length == 0)
        {
            title = PanelText.Plain(stage.StageId, StageTimelineModel.MaxTitleChars);
        }

        if (title.Length == 0)
        {
            title = "Stage";
        }

        var detail = PanelText.Plain(stage.Detail, StageTimelineModel.MaxDetailChars);
        var statusText = AgentPanelCopy.StageStatusText(stage.Status);
        var retryable = canRetry && depth == 0 && known && IsRecheckable(stage.Source)
            && stage.Status is StageStatus.Failed or StageStatus.Degraded or StageStatus.Unavailable;
        return new StageRow
        {
            Source = stage.Source,
            StageId = stage.StageId,
            ParentStageId = stage.ParentStageId,
            Depth = depth,
            Title = title,
            Status = stage.Status,
            IsStatusKnown = known,
            StatusText = statusText,
            IconKey = AgentPanelCopy.StageStatusIcon(stage.Status),
            Detail = detail.Length == 0 ? null : detail,
            EffectText = known ? AgentPanelCopy.StageEffect(stage.Source, stage.Status) : null,
            GraphId = string.IsNullOrWhiteSpace(stage.GraphId) ? null : PanelText.Plain(stage.GraphId, 64),
            Timestamp = stage.Timestamp,
            CanRetry = retryable,
            RetryName = $"Retry {title} stage",
            AccessibleName = detail.Length == 0 ? $"{title}, {statusText}" : $"{title}, {statusText}, {detail}",
        };
    }

    private static StageStatus? RollUp(List<StageRow> rows)
    {
        var present = rows.Where(row => row.Depth == 0 && row.IsStatusKnown).Select(row => row.Status).ToHashSet();
        foreach (var status in RollUpOrder)
        {
            if (present.Contains(status))
            {
                return status;
            }
        }

        return null;
    }

    private static (int Total, int Done) SwarmCounts(StageSourceBlock block)
    {
        if (block.Source != StageSource.Swarm)
        {
            return (0, 0);
        }

        var below = block.Rows.Where(row => row.Depth > 0).ToList();
        return (below.Count, below.Count(row => row.IsStatusKnown && row.Status == StageStatus.Succeeded));
    }

    private static PipelinePill Pill(StageSource source, StageStatus? status, (int Total, int Done) swarm, GsdPhaseInfo? phase)
    {
        var word = status is { } known ? PillWord(source, known, swarm, phase) : "waiting";
        return new PipelinePill
        {
            Source = source,
            Text = $"{NameOf(source)} {word}",
            Status = status,
            IconKey = status is { } shown ? AgentPanelCopy.StageStatusIcon(shown) : StatusIcon.Clock,
        };
    }

    private static string PillWord(StageSource source, StageStatus status, (int Total, int Done) swarm, GsdPhaseInfo? phase) => status switch
    {
        StageStatus.Running => source switch
        {
            StageSource.Ultrathink => "planning\u2026",
            StageSource.Swarm => swarm.Total > 0 ? Count($"{swarm.Done} of {swarm.Total}") : "running",
            _ => "checking\u2026",
        },
        StageStatus.Succeeded => source switch
        {
            StageSource.Ultrathink => "planned",
            StageSource.Swarm => swarm.Total > 0 ? Count($"{swarm.Total} done") : "done",
            StageSource.Substrate => "ok",
            _ => phase is null ? "unknown" : PhaseWord(phase),
        },
        StageStatus.Degraded => source == StageSource.Ultrathink ? "fallback" : "degraded",
        StageStatus.Failed => "failed",
        StageStatus.Skipped => "off",
        StageStatus.Unavailable => "unavailable",
        _ => "waiting",
    };

    private static string Clause(StageSource source, StageStatus status, (int Total, int Done) swarm, GsdPhaseInfo? phase) => status switch
    {
        StageStatus.Running => source switch
        {
            StageSource.Ultrathink => "planning",
            StageSource.Swarm => swarm.Total > 0 ? Count($"{swarm.Done} of {swarm.Total}") : "running",
            _ => "checking",
        },
        StageStatus.Succeeded => source switch
        {
            StageSource.Ultrathink => "planned",
            StageSource.Swarm => swarm.Total > 0 ? Count($"{swarm.Done} of {swarm.Total} done") : "done",
            StageSource.Substrate => "ok",
            _ => phase is null ? "unknown" : Count($"{PanelText.Plain(phase.Number, 20)}, plan {phase.PlansComplete} of {phase.PlansTotal}"),
        },
        StageStatus.Degraded => source == StageSource.Ultrathink ? "fallback" : "degraded",
        StageStatus.Failed => "failed",
        StageStatus.Skipped => "off",
        StageStatus.Unavailable => "unavailable",
        _ => "waiting",
    };

    private static string PhaseWord(GsdPhaseInfo phase) =>
        Count($"{PanelText.Plain(phase.Number, 20)} \u00B7 {phase.PlansComplete} of {phase.PlansTotal}");

    private static string Count(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
