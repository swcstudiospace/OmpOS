namespace OmpOS.Harness.OmpConfig;

/// <summary>The line range of one OmpOS block, both marker lines included.</summary>
/// <param name="Id">The provider id named by the markers.</param>
/// <param name="Begin">The index of the <c># ompos:begin</c> line.</param>
/// <param name="End">The index of the <c># ompos:end</c> line.</param>
internal readonly record struct OmpManagedBlockSpan(string Id, int Begin, int End);
