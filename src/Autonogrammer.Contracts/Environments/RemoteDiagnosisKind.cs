namespace Autonogrammer.Contracts.Environments;

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

    /// <summary>omp is older than the oldest version Autonogrammer supports.</summary>
    OmpTooOld,

    /// <summary>omp needs Bun and the remote machine has none.</summary>
    BunMissing,

    /// <summary>The remote operating system or CPU is not one Autonogrammer has verified.</summary>
    UnsupportedPlatform,

    /// <summary>A standard tool Autonogrammer needs on the remote machine (for example <c>mktemp</c>) is missing.</summary>
    RequiredToolMissing,

    /// <summary>The <c>podman</c> program is not installed or cannot be started on this machine.</summary>
    ContainerRuntimeMissing,

    /// <summary>podman is installed but its service cannot be reached (for example the podman machine is stopped).</summary>
    ContainerRuntimeUnavailable,

    /// <summary>No container has the name or id of the profile.</summary>
    ContainerMissing,

    /// <summary>The container exists but is not running.</summary>
    ContainerNotRunning,

    /// <summary>The user of the profile does not exist inside the container.</summary>
    ContainerUserMissing,

    /// <summary>The <c>wsl.exe</c> program is not available: WSL is not installed, or this machine is not Windows.</summary>
    WslMissing,

    /// <summary>No WSL distribution has the name of the profile.</summary>
    WslDistroMissing,

    /// <summary>The user of the profile does not exist in the WSL distribution.</summary>
    WslUserMissing,
}
