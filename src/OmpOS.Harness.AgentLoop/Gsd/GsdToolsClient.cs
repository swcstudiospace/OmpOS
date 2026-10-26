using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text.Json;
using OmpOS.Contracts.Agents;

namespace OmpOS.Harness.AgentLoop.Gsd;

/// <summary>
/// Reads the workflow phases of a workspace by running the workflow engine's <c>roadmap.analyze</c> query. A result is cached
/// per workspace and stays valid while the modification time and length of <c>.planning/ROADMAP.md</c> and
/// <c>.planning/STATE.md</c> are unchanged and the entry is younger than the maximum age, so repeated polls spawn no process.
/// The age bound exists because plan and summary files inside the phase directories can change without touching either file.
/// Failures are typed <see cref="GsdToolsException"/>s and are never cached.
/// </summary>
public sealed class GsdToolsClient
{
    /// <summary>The default longest time a cached result is reused.</summary>
    public static readonly TimeSpan DefaultMaxCacheAge = TimeSpan.FromSeconds(60);

    private const string PlanningDirectoryName = ".planning";
    private const string QueryName = "roadmap.analyze";

    private readonly IGsdProcessRunner _runner;
    private readonly GsdToolsLocator _locator;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _maxCacheAge;
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new(StringComparer.Ordinal);

    /// <summary>Initializes a new instance of the <see cref="GsdToolsClient"/> class.</summary>
    /// <param name="runner">Runs the query.</param>
    /// <param name="locator">Finds the tool script.</param>
    /// <param name="timeProvider">Supplies the clock for the cache age.</param>
    /// <param name="maxCacheAge">The longest time a cached result is reused, or null for <see cref="DefaultMaxCacheAge"/>.</param>
    public GsdToolsClient(IGsdProcessRunner runner, GsdToolsLocator locator, TimeProvider timeProvider, TimeSpan? maxCacheAge = null)
    {
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(locator);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _runner = runner;
        _locator = locator;
        _timeProvider = timeProvider;
        _maxCacheAge = maxCacheAge ?? DefaultMaxCacheAge;
    }

    /// <summary>Reads the workflow phases of the workspace.</summary>
    /// <param name="workspaceRoot">The absolute path of the workspace that holds <c>.planning</c>.</param>
    /// <param name="cancellationToken">Cancels the query and kills its process.</param>
    /// <returns>The phases and the phase the work is at.</returns>
    /// <exception cref="GsdToolsException">The workspace has no workflow state, the tool is missing, or the query failed.</exception>
    public async Task<GsdWorkflowSnapshot> GetWorkflowAsync(string workspaceRoot, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        cancellationToken.ThrowIfCancellationRequested();

        var root = Path.GetFullPath(workspaceRoot);
        var planning = Path.Combine(root, PlanningDirectoryName);
        if (!Directory.Exists(planning))
        {
            throw new GsdToolsException(GsdToolsFailure.WorkspaceNotInitialized, "No workflow state in this workspace");
        }

        var stamp = ReadStamp(planning);
        var now = _timeProvider.GetUtcNow();
        if (_cache.TryGetValue(root, out var cached) && cached.Stamp == stamp && now - cached.At < _maxCacheAge)
        {
            return cached.Snapshot;
        }

        var script = _locator.Locate()
            ?? throw new GsdToolsException(GsdToolsFailure.ToolsNotFound, "The GSD tools were not found");

        var output = await _runner.QueryAsync(script, QueryName, [], root, cancellationToken).ConfigureAwait(false);
        var snapshot = Parse(output);
        _cache[root] = new CacheEntry(stamp, now, snapshot);
        return snapshot;
    }

    private static PlanningStamp ReadStamp(string planningDirectory)
    {
        try
        {
            return new PlanningStamp(
                StampOf(Path.Combine(planningDirectory, "ROADMAP.md")),
                StampOf(Path.Combine(planningDirectory, "STATE.md")));
        }
        catch (IOException exception)
        {
            throw new GsdToolsException(GsdToolsFailure.Unreadable, "The planning files could not be inspected.", innerException: exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new GsdToolsException(GsdToolsFailure.Unreadable, "The planning files could not be inspected.", innerException: exception);
        }
    }

    private static FileStamp StampOf(string path)
    {
        var file = new FileInfo(path);
        return file.Exists ? new FileStamp(file.LastWriteTimeUtc.Ticks, file.Length) : new FileStamp(-1, -1);
    }

    private static GsdWorkflowSnapshot Parse(string output)
    {
        GsdRoadmapDocument? document;
        try
        {
            document = JsonSerializer.Deserialize(output, GsdJsonContext.Default.GsdRoadmapDocument);
        }
        catch (JsonException exception)
        {
            throw new GsdToolsException(GsdToolsFailure.InvalidOutput, "The workflow tool did not return a roadmap analysis.", innerException: exception);
        }

        if (document?.Phases is null)
        {
            throw new GsdToolsException(GsdToolsFailure.InvalidOutput, "The workflow tool did not return a roadmap analysis.");
        }

        var phases = new List<GsdPhaseInfo>(document.Phases.Count);
        foreach (var phase in document.Phases)
        {
            if (phase is null)
            {
                continue;
            }

            var number = ReadText(phase.Number);
            if (number is null)
            {
                continue;
            }

            phases.Add(new GsdPhaseInfo
            {
                Number = number,
                Name = string.IsNullOrWhiteSpace(phase.Name) ? "(unnamed)" : phase.Name.Trim(),
                Status = StatusOf(phase),
                PlansComplete = Math.Max(0, phase.SummaryCount ?? 0),
                PlansTotal = Math.Max(0, phase.PlanCount ?? 0),
            });
        }

        var currentNumber = ReadText(document.CurrentPhase);
        var nextNumber = ReadText(document.NextPhase);
        var current = Find(phases, currentNumber)
            ?? Find(phases, nextNumber)
            ?? phases.Find(static phase => !IsComplete(phase))
            ?? (phases.Count > 0 ? phases[^1] : null);
        return new GsdWorkflowSnapshot { Phases = phases, CurrentPhase = current };
    }

    private static GsdPhaseInfo? Find(List<GsdPhaseInfo> phases, string? number) =>
        number is null ? null : phases.Find(phase => string.Equals(phase.Number, number, StringComparison.Ordinal));

    private static bool IsComplete(GsdPhaseInfo phase) =>
        string.Equals(phase.Status, GsdWorkflowSnapshot.CompleteStatus, StringComparison.Ordinal);

    private static string StatusOf(GsdRoadmapPhaseDocument phase)
    {
        var disk = string.IsNullOrWhiteSpace(phase.DiskStatus)
            ? null
            : phase.DiskStatus.Trim().Replace('_', ' ');
        if (phase.RoadmapComplete == true || string.Equals(disk, GsdWorkflowSnapshot.CompleteStatus, StringComparison.OrdinalIgnoreCase))
        {
            return GsdWorkflowSnapshot.CompleteStatus;
        }

        return disk ?? "unknown";
    }

    private static string? ReadText(JsonElement? element)
    {
        if (element is not { } value)
        {
            return null;
        }

        var text = value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null,
        };
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly record struct FileStamp(long Ticks, long Length);

    [StructLayout(LayoutKind.Auto)]
    private readonly record struct PlanningStamp(FileStamp Roadmap, FileStamp State);

    private sealed record CacheEntry(PlanningStamp Stamp, DateTimeOffset At, GsdWorkflowSnapshot Snapshot);
}
