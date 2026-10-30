using System.Text.Json;

namespace Autonogrammer.Harness.AgentLoop.Gsd;

/// <summary>
/// The tool's <c>roadmap.analyze</c> output, reduced to the fields the host needs. A null <see cref="Phases"/> means the
/// document is not a roadmap analysis at all.
/// </summary>
internal sealed class GsdRoadmapDocument
{
    public List<GsdRoadmapPhaseDocument>? Phases { get; set; }

    public JsonElement? CurrentPhase { get; set; }

    public JsonElement? NextPhase { get; set; }
}
