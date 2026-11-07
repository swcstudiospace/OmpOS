namespace Autonogrammer.Harness.ComputerUse.Abstractions;

// Plan note: tool-provider and approval-broker types are not inputs of this assembly.
// The Native head implements IComputerUseAccessFacts over ICoworkAccountGate once phase 69 signatures exist.
// Re-read those signatures when phases 67, 69 and 70 land and reconcile names without changing these semantics.
// The web sign-in seam is not an input.

/// <summary>Reads redacted audit rows for the settings page.</summary>
public interface IComputerAuditReader
{
    /// <summary>Reads redacted rows for the last <paramref name="days"/> days.</summary>
    /// <param name="days">How many days to include.</param>
    /// <param name="includeThumbnails">Whether thumbnail bytes may be included. Default audit thumbnails are off.</param>
    /// <param name="cancellationToken">Token that cancels the call.</param>
    /// <returns>Redacted rows. Thumbnail bytes are not on <see cref="AuditRecordView"/>.</returns>
    ValueTask<IReadOnlyList<AuditRecordView>> ReadAsync(int days, bool includeThumbnails, CancellationToken cancellationToken);
}

