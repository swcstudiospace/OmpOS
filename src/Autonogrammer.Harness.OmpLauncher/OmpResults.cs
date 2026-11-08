namespace Autonogrammer.Harness.OmpLauncher;

/// <summary>Builds the shared update results so each failure mode has one construction.</summary>
internal static class OmpResults
{
    /// <summary>A failure with its kind, version and message.</summary>
    /// <param name="kind">Why it failed.</param>
    /// <param name="version">The version involved, or null.</param>
    /// <param name="message">The user-readable text.</param>
    /// <returns>The failure.</returns>
    public static OmpUpdateFailed Failed(OmpFailureKind kind, string? version, string message) => new(kind, version, message);

    /// <summary>A certificate or TLS trust failure.</summary>
    /// <param name="version">The version involved, or null.</param>
    /// <returns>The failure.</returns>
    public static OmpUpdateFailed TrustFailure(string? version) =>
        new(OmpFailureKind.TlsTrust, version, OmpManagedMessages.TlsTrust);

    /// <summary>The stale notice that keeps the active version and shows when it was last checked.</summary>
    /// <param name="state">The state read before the attempt.</param>
    /// <returns>The offline result.</returns>
    public static OmpOffline Offline(OmpManagedState state) =>
        new(state.ActiveVersion, state.LastCheckedAt, OmpManagedMessages.Offline(state.ActiveVersion, state.LastCheckedAt));

    /// <summary>Maps a download status to the pipeline result, or null when the download verified.</summary>
    /// <param name="status">How the download ended.</param>
    /// <param name="artifact">The name shown in the message.</param>
    /// <param name="version">The omp version involved, or null for the Bun archive.</param>
    /// <param name="state">The state read before the attempt.</param>
    /// <returns>The terminal result, or null.</returns>
    public static OmpUpdateResult? ForDownload(OmpDownloadStatus status, string artifact, string? version, OmpManagedState state)
    {
        switch (status)
        {
            case OmpDownloadStatus.Verified:
                return null;
            case OmpDownloadStatus.Offline:
                return Offline(state);
            case OmpDownloadStatus.TrustFailure:
                return TrustFailure(version);
            case OmpDownloadStatus.Truncated:
                return Failed(OmpFailureKind.PartialDownload, version, OmpManagedMessages.PartialDownload(artifact));
            case OmpDownloadStatus.IntegrityMismatch:
                return Failed(OmpFailureKind.IntegrityFailed, version, OmpManagedMessages.IntegrityFailed(artifact));
            default:
                return Failed(OmpFailureKind.Generic, version, OmpManagedMessages.HttpStatus(artifact));
        }
    }
}
