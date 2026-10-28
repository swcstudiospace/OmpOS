using Autonogrammer.Contracts.Agents;

namespace Autonogrammer.Harness.AgentLoop.Stages;

/// <summary>A stage source with the name and the pipeline component that <see cref="CompositeStageSource"/> reports it under.</summary>
/// <param name="Name">The display name. When the source faults, the Degraded event's title is this name.</param>
/// <param name="Source">The pipeline component the source belongs to. A Degraded event for the source carries it.</param>
/// <param name="Inner">The source that is merged.</param>
/// <param name="MayComplete">
/// True when the source finishes by design after emitting its events, as a one-shot availability report does. A source that
/// is expected to observe until cancelled leaves it false, and its early completion is reported as a Degraded stage.
/// </param>
public sealed record NamedStageSource(string Name, StageSource Source, IStageSource Inner, bool MayComplete = false);
