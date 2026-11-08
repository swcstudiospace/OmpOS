namespace Autonogrammer.Presentation.Remote;

/// <summary>Says an Omp report in words: what the check found, what the install did, and what the user must do next.</summary>
public static class RemoteOmpDescriber
{
    /// <summary>Returns the result sentence of a report, which the page shows and announces.</summary>
    /// <param name="report">The report.</param>
    /// <returns>The sentence.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="report"/> is null.</exception>
    public static string ResultText(RemoteOmpReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var reason = string.IsNullOrWhiteSpace(report.Reason) ? RemoteCopy.OmpNoReason : report.Reason;
        return report.Outcome switch
        {
            RemoteOmpOutcome.UpToDate => RemoteCopy.OmpUpToDate,
            RemoteOmpOutcome.Planned => RemoteCopy.OmpPlanned,
            RemoteOmpOutcome.NeedsCheck => RemoteCopy.OmpNeedsCheck,
            RemoteOmpOutcome.Installed => RemoteCopy.OmpInstalled,
            RemoteOmpOutcome.Failed => RemoteCopy.OmpFailedPrefix + reason + " " + (report.Changed ? RemoteCopy.OmpFailedChanged : RemoteCopy.OmpFailedUnchanged) + RemoteCopy.OmpFailedEnding,
            RemoteOmpOutcome.Refused => RemoteCopy.OmpRefusedPrefix + reason,
            RemoteOmpOutcome.Unavailable => RemoteCopy.OmpUnavailablePrefix + reason,
            _ => throw new ArgumentOutOfRangeException(nameof(report), report.Outcome, "The outcome has no known sentence."),
        };
    }

    /// <summary>Says one drift item in one line: what differs, and why an install cannot fix it when it cannot.</summary>
    /// <param name="line">The item.</param>
    /// <returns>The line.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="line"/> is null.</exception>
    public static string DriftText(RemoteOmpDriftLine line)
    {
        ArgumentNullException.ThrowIfNull(line);
        var text = line.Subject + ": " + line.Reason;
        return line.BlockedBecause is null ? text : text + " " + RemoteCopy.OmpDriftBlockedPrefix + line.BlockedBecause;
    }
}
