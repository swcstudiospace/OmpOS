namespace OmpOS.Contracts.Environments;

/// <summary>What is wrong with a remote environment, as found by the connection test (the preflight) or by a failed connection.</summary>
public enum RemoteDiagnosisKind
{
    /// <summary>The cause is not one of the known ones. The detail holds the first lines the tool printed.</summary>
    Unknown,

    /// <summary>The <c>ssh</c> client is not installed or cannot be started.</summary>
    SshMissing,

    /// <summary>The host cannot be reached: name not resolved, connection refused, no route or a timeout.</summary>
    HostUnreachable,

    /// <summary>The host key is not in <c>known_hosts</c> and ssh may not ask (non-interactive mode).</summary>
    HostKeyUnknown,

    /// <summary>The host key differs from the one in <c>known_hosts</c>.</summary>
    HostKeyChanged,

    /// <summary>The host refused the credentials ssh could offer without a prompt.</summary>
    AuthFailed,

    /// <summary>The login shell of the remote user does not understand POSIX shell syntax.</summary>
    ShellNotPosix,

    /// <summary>The remote workspace directory does not exist.</summary>
    DirectoryMissing,

    /// <summary>The remote workspace directory exists and cannot be written by the remote user.</summary>
    DirectoryNotWritable,

    /// <summary>omp was not found on the remote machine.</summary>
    OmpMissing,

    /// <summary>omp is older than the oldest version OmpOS supports.</summary>
    OmpTooOld,

    /// <summary>omp needs Bun and the remote machine has none.</summary>
    BunMissing,

    /// <summary>The remote operating system or CPU is not one OmpOS has verified.</summary>
    UnsupportedPlatform,

    /// <summary>A standard tool OmpOS needs on the remote machine (for example <c>mktemp</c>) is missing.</summary>
    RequiredToolMissing,
}
