namespace Autonogrammer.Harness.OmpConfig;

/// <summary>
/// The line range of one Autonogrammer block, both marker lines included. The range is the same for the new marker family and the legacy
/// one; the indices do not say which family the markers were written in.
/// </summary>
/// <param name="Id">The provider id named by the markers.</param>
/// <param name="Begin">The index of the begin marker line.</param>
/// <param name="End">The index of the end marker line.</param>
internal readonly record struct OmpManagedBlockSpan(string Id, int Begin, int End);
