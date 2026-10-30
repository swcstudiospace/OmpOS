using System.Text.Json;

namespace Autonogrammer.Harness.AgentLoop.Gsd;

/// <summary>
/// One phase of the tool's <c>roadmap.analyze</c> output. Only the fields the host needs are bound and every one is
/// optional, because the tool's output shape belongs to a third party and may gain, lose or retype fields.
/// </summary>
internal sealed class GsdRoadmapPhaseDocument
{
    public JsonElement? Number { get; set; }

    public string? Name { get; set; }

    public string? DiskStatus { get; set; }

    public bool? RoadmapComplete { get; set; }

    public int? PlanCount { get; set; }

    public int? SummaryCount { get; set; }
}
