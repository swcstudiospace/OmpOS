using System.Text.Json;
using System.Text.RegularExpressions;

namespace Autonogrammer.Harness.AgentLoop.Stages;

/// <summary>
/// Reads an Ultrathink session record tolerantly. Pure: it takes bytes and returns a summary, so tests feed it fixtures. A
/// record that is empty, is not JSON or has the wrong shape yields null; it never throws.
/// </summary>
internal static partial class UltrathinkPlanParser
{
    /// <summary>The largest session record the observer reads. Real records are a few hundred kilobytes.</summary>
    public const int MaxFileBytes = 2 * 1024 * 1024;

    /// <summary>The most nodes considered when counting waves. The count of nodes itself is exact.</summary>
    private const int MaxWaveNodes = 512;

    private static readonly string[] KnownTrackingStatuses = ["complete", "partial", "failed"];

    /// <summary>Reads the summary of a session record.</summary>
    /// <param name="utf8">The file content.</param>
    /// <returns>The summary, or null when the content is not a readable session record.</returns>
    public static UltrathinkPlanSummary? Parse(ReadOnlySpan<byte> utf8)
    {
        UltrathinkSessionDocument? document;
        try
        {
            document = JsonSerializer.Deserialize(utf8, StagesJsonContext.Default.UltrathinkSessionDocument);
        }
        catch (JsonException)
        {
            return null;
        }

        if (document is null)
        {
            return null;
        }

        var nodes = document.Graph?.Nodes ?? document.Result?.Graph?.Nodes;
        var nodeCount = nodes?.Count(static node => node is not null) ?? 0;
        var graphId = document.Plan?.GraphId;
        var validGraphId = graphId is not null && GraphIdPattern().IsMatch(graphId) ? graphId : null;
        var isFallback =
            string.Equals(document.Result?.Source, "fallback", StringComparison.OrdinalIgnoreCase)
            || document.Plan is null
            || nodeCount == 0;

        return new UltrathinkPlanSummary(
            isFallback,
            validGraphId,
            nodeCount,
            CountWaves(nodes),
            document.Clarifications?.Count ?? 0,
            NormalizeTrackingStatus(document.Tracking?.Status));
    }

    /// <summary>
    /// Counts the dependency levels of a graph: a node with no known dependency is on level 0 and any other node is one level
    /// below its deepest dependency. A dependency on an unknown node or on the node itself is ignored, and a cycle is
    /// cut off at one level per node.
    /// </summary>
    /// <param name="nodes">The thought nodes.</param>
    /// <returns>The number of levels, zero for no nodes.</returns>
    internal static int CountWaves(IReadOnlyList<UltrathinkSessionDocument.NodePart?>? nodes)
    {
        if (nodes is null)
        {
            return 0;
        }

        var ids = new Dictionary<string, int>(StringComparer.Ordinal);
        var considered = new List<UltrathinkSessionDocument.NodePart>();
        foreach (var node in nodes)
        {
            if (node is null || considered.Count >= MaxWaveNodes)
            {
                continue;
            }

            considered.Add(node);
            if (!string.IsNullOrEmpty(node.Id))
            {
                ids.TryAdd(node.Id, considered.Count - 1);
            }
        }

        if (considered.Count == 0)
        {
            return 0;
        }

        return ComputeLevels(considered, ids).Max() + 1;
    }

    private static int[] ComputeLevels(List<UltrathinkSessionDocument.NodePart> considered, Dictionary<string, int> ids)
    {
        var levels = new int[considered.Count];
        for (var pass = 0; pass < considered.Count; pass++)
        {
            var changed = false;
            for (var index = 0; index < considered.Count; index++)
            {
                var level = levels[index];
                foreach (var dependency in considered[index].DependsOn ?? [])
                {
                    if (dependency is not null
                        && ids.TryGetValue(dependency, out var dependencyIndex)
                        && dependencyIndex != index
                        && levels[dependencyIndex] + 1 > level)
                    {
                        level = levels[dependencyIndex] + 1;
                    }
                }

                // An acyclic graph has at most Count levels, so a cycle is cut off there instead of growing with every pass.
                level = Math.Min(level, considered.Count - 1);
                if (level != levels[index])
                {
                    levels[index] = level;
                    changed = true;
                }
            }

            if (!changed)
            {
                break;
            }
        }

        return levels;
    }

    private static string? NormalizeTrackingStatus(string? status)
    {
        var trimmed = status?.Trim();
        foreach (var known in KnownTrackingStatuses)
        {
            if (string.Equals(trimmed, known, StringComparison.OrdinalIgnoreCase))
            {
                return known;
            }
        }

        return null;
    }

    [GeneratedRegex(@"\Aut-[a-z0-9]+-[0-9a-f]{8}\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 250)]
    private static partial Regex GraphIdPattern();
}
