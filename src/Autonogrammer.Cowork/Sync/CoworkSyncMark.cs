namespace Autonogrammer.Cowork.Sync;

/// <summary>A quiet sync failure shown on focus. It does not change the local task.</summary>
/// <param name="SessionId">The session the mark belongs to.</param>
/// <param name="Label">Always <see cref="CoworkSessionSync.NotSyncedLabel"/>.</param>
/// <param name="Reason">Why the last push did not land.</param>
public sealed record CoworkSyncMark(string SessionId, string Label, string Reason);
