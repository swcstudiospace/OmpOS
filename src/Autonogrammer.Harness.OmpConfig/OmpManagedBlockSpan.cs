namespace Autonogrammer.Harness.OmpConfig;

/// <summary>The line range of one Autonogrammer block, both marker lines included.</summary>
/// <param name="Id">The provider id named by the markers.</param>
/// <param name="Begin">The index of the <c># autonogrammer:begin</c> line.</param>
/// <param name="End">The index of the <c># autonogrammer:end</c> line.</param>
internal readonly record struct OmpManagedBlockSpan(string Id, int Begin, int End);
