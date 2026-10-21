using OmpOS.Contracts.Sessions;

namespace OmpOS.SessionRuns;

/// <summary>Decides how a harness turn settles from the completion the harness reports.</summary>
internal static class HarnessTurnOutcome
{
    /// <summary>The stop reason the harness reports when a turn fails.</summary>
    internal const string ErrorStopReason = "error";

    /// <summary>Maps a turn's harness outcome to the status it settles with.</summary>
    /// <param name="stopReason">The reason on the harness completion, or <see langword="null"/> when the stream ended without one.</param>
    /// <param name="sawError">Whether the harness streamed an error event during the turn.</param>
    /// <returns>
    /// <see cref="SessionStatus.Failed"/> when the completion reports the error reason, or when the stream ended
    /// after an error without a completion; otherwise <see cref="SessionStatus.Completed"/>.
    /// </returns>
    public static SessionStatus SettledStatus(string? stopReason, bool sawError) => stopReason switch
    {
        ErrorStopReason => SessionStatus.Failed,
        null when sawError => SessionStatus.Failed,
        _ => SessionStatus.Completed,
    };
}
