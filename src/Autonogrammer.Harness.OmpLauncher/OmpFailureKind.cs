namespace Autonogrammer.Harness.OmpLauncher;

/// <summary>
/// Why a managed omp install or update did not complete. Each kind has its user-readable text in
/// <see cref="OmpManagedMessages"/>.
/// </summary>
public enum OmpFailureKind
{
    /// <summary>A failure with no more specific kind. The message says what failed.</summary>
    Generic,

    /// <summary>The managed state file exists but cannot be read or parsed.</summary>
    StateUnreadable,

    /// <summary>The TLS handshake or certificate chain was rejected, usually by an intercepting proxy.</summary>
    TlsTrust,

    /// <summary>The managed directory cannot be read or written by this user.</summary>
    Permission,

    /// <summary>A file was blocked right after it was written or run. This is the usual sign of antivirus quarantine.</summary>
    AntivirusSuspected,

    /// <summary>A download ended before its declared length.</summary>
    PartialDownload,

    /// <summary>A download does not match its published or pinned digest. Nothing was installed.</summary>
    IntegrityFailed,

    /// <summary>The pinned Bun is older than the Bun range the release requires. Nothing was installed.</summary>
    IncompatibleVersion,

    /// <summary>There is no pinned Bun for this platform.</summary>
    UnsupportedPlatform,

    /// <summary>The Bun runtime could not be downloaded, verified or extracted.</summary>
    BunUnavailable,

    /// <summary>The package archive is corrupt or does not have the expected shape.</summary>
    InvalidArchive,

    /// <summary>The package archive has an entry that is not allowed. Nothing was extracted.</summary>
    UnsafeArchive,

    /// <summary>The registry answered with a document that is not a usable package record.</summary>
    InvalidRegistryResponse,

    /// <summary>The staged or activated executable did not pass its health check.</summary>
    HealthCheckFailed,

    /// <summary>The version failed a health check earlier and is quarantined until a newer release is published.</summary>
    Quarantined,
}
