namespace Autonogrammer.Presentation.Remote;

/// <summary>One item of a remote machine that differs from what Omp needs, with the reason and, when an install cannot fix it, why.</summary>
/// <param name="Subject">The path or plugin directory the item is about, as it appears on the remote machine.</param>
/// <param name="Reason">Why the item differs, for example the version it has and the version it needs.</param>
/// <param name="BlockedBecause">Why an install cannot fix the item, or <see langword="null"/> when an install does.</param>
public sealed record RemoteOmpDriftLine(string Subject, string Reason, string? BlockedBecause);
