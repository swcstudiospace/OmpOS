using Autonogrammer.Harness.AgentLoop.Launch;
using Autonogrammer.Native.Engine;

namespace Autonogrammer.Native.Cowork;

/// <summary>A composed Cowork engine.</summary>
/// <param name="Engine">The engine.</param>
/// <param name="Notices">Composer notices, including a cowork name clash.</param>
/// <param name="Plan">The launch plan.</param>
internal sealed record CoworkEngineComposition(EngineComposition Engine, IReadOnlyList<string> Notices, OmpLaunchPlan Plan);
