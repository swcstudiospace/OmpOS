namespace Autonogrammer.Harness.OmpLauncher;

/// <summary>How a verified download ended.</summary>
internal enum OmpDownloadStatus
{
    /// <summary>The body arrived in full and matched its digest.</summary>
    Verified,

    /// <summary>The network failed before the body started, or the wait timed out.</summary>
    Offline,

    /// <summary>A certificate or TLS trust failure stopped the connection.</summary>
    TrustFailure,

    /// <summary>The body ended before its declared length, or the connection broke during it.</summary>
    Truncated,

    /// <summary>The body arrived in full but its digest differs from the expected one.</summary>
    IntegrityMismatch,

    /// <summary>The server answered with an error status.</summary>
    HttpStatus,
}
