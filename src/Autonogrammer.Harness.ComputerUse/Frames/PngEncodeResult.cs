using Autonogrammer.Harness.ComputerUse.Abstractions;

namespace Autonogrammer.Harness.ComputerUse.Frames;

/// <summary>PNG bytes, or <see cref="DenialCode.FrameTooLarge"/> after four 0.85 reductions.</summary>
/// <param name="Bytes">PNG bytes when the cap was met.</param>
/// <param name="Denial">Denial when the cap was not met.</param>
/// <param name="Reductions">How many 0.85 reductions were applied. Four means the cap still failed.</param>
public sealed record PngEncodeResult(ReadOnlyMemory<byte> Bytes, DenialCode? Denial, int Reductions);
