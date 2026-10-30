using Autonogrammer.Harness.ComputerUse.Abstractions;

namespace Autonogrammer.Harness.ComputerUse.Frames;

/// <summary>A frame, or a denial. A success carries one image.</summary>
/// <param name="Frame">The frame when allowed.</param>
/// <param name="Denial">The denial when refused.</param>
public sealed record VisionCapture(FrameRecord? Frame, DenialCode? Denial);
