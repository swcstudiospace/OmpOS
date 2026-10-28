using System.Globalization;
using OmpOS.Contracts.Environments;

namespace OmpOS.Presentation.Remote;

/// <summary>The words and the emphasis of every <see cref="RemoteEnvironmentStatus"/>, and the rules that turn a test report or a live connection state into one.</summary>
public static class RemoteStatusDescriber
{
    /// <summary>Returns the short label of a status, as shown in the chip.</summary>
    /// <param name="status">The status.</param>
    /// <returns>The label.</returns>
    public static string LabelOf(RemoteEnvironmentStatus status) => status switch
    {
        RemoteEnvironmentStatus.NotChecked => "Not checked",
        RemoteEnvironmentStatus.Testing => "Testing",
        RemoteEnvironmentStatus.HostKeyUnconfirmed => "Host key not trusted yet",
        RemoteEnvironmentStatus.HostKeyChanged => "Host key changed",
        RemoteEnvironmentStatus.Authenticating => "Signing in",
        RemoteEnvironmentStatus.Connecting => "Connecting",
        RemoteEnvironmentStatus.Connected => "Connected",
        RemoteEnvironmentStatus.Reconnecting => "Reconnecting",
        RemoteEnvironmentStatus.AuthFailed => "Sign-in failed",
        RemoteEnvironmentStatus.Unreachable => "Unreachable",
        RemoteEnvironmentStatus.VersionMismatch => "omp missing or too old",
        RemoteEnvironmentStatus.NeedsSetup => "Needs setup",
        RemoteEnvironmentStatus.Ready => "Ready",
        RemoteEnvironmentStatus.ReadyWithWarnings => "Ready with warnings",
        RemoteEnvironmentStatus.Disconnected => "Disconnected",
        RemoteEnvironmentStatus.Interrupted => "Disconnected, turn interrupted",
        RemoteEnvironmentStatus.RuntimeUnavailable => "Runtime not available",
        RemoteEnvironmentStatus.NotRunning => "Container not running",
        RemoteEnvironmentStatus.NotFound => "Not found",
        _ => "Unknown",
    };

    /// <summary>Returns the one-sentence explanation of a status, with what the user can do.</summary>
    /// <param name="status">The status.</param>
    /// <param name="reconnectAttempt">The retry number while reconnecting (1-based), or 0 when unknown.</param>
    /// <returns>The sentence.</returns>
    public static string DetailOf(RemoteEnvironmentStatus status, int reconnectAttempt = 0) => status switch
    {
        RemoteEnvironmentStatus.NotChecked => "Press Test connection to check the host.",
        RemoteEnvironmentStatus.Testing => "Connecting to the host and checking omp. Nothing is installed or changed.",
        RemoteEnvironmentStatus.HostKeyUnconfirmed => "ssh does not know this host yet. Confirm its fingerprint in the terminal.",
        RemoteEnvironmentStatus.HostKeyChanged => "The host key differs from the one in known_hosts. Do not connect until you know why.",
        RemoteEnvironmentStatus.Authenticating => "The connection is open and ssh is signing in with your agent or identity file.",
        RemoteEnvironmentStatus.Connecting => "Opening the connection and starting omp on the host.",
        RemoteEnvironmentStatus.Connected => "The session is running on the host.",
        RemoteEnvironmentStatus.Reconnecting => ReconnectText(reconnectAttempt),
        RemoteEnvironmentStatus.AuthFailed => "The host refused the credentials ssh can offer without asking you. Load your key into ssh-agent or set an identity file.",
        RemoteEnvironmentStatus.Unreachable => "The host did not answer. Check the address, the port and your network or VPN.",
        RemoteEnvironmentStatus.VersionMismatch => "Install or update omp (and Bun) on the host, or set the remote omp path.",
        RemoteEnvironmentStatus.NeedsSetup => "Fix the findings below, then test the connection again.",
        RemoteEnvironmentStatus.Ready => "The host passed every check.",
        RemoteEnvironmentStatus.ReadyWithWarnings => "The host works, but a check raised a warning. See the findings.",
        RemoteEnvironmentStatus.Disconnected => "No session is connected to this host.",
        RemoteEnvironmentStatus.Interrupted => "The connection dropped while the agent was working. That turn did not finish: send the prompt again once the host is back.",
        RemoteEnvironmentStatus.RuntimeUnavailable => "podman (containers) or wsl.exe (WSL) is not installed or does not answer. Install it or start its service, then test again.",
        RemoteEnvironmentStatus.NotRunning => "The container is stopped. Start it, then test again. OmpOS never starts a container on its own.",
        RemoteEnvironmentStatus.NotFound => "The container or WSL distribution does not exist. Check its name in the profile.",
        _ => string.Empty,
    };

    /// <summary>Returns how a status is emphasised.</summary>
    /// <param name="status">The status.</param>
    /// <returns>The severity.</returns>
    public static RemoteStatusSeverity SeverityOf(RemoteEnvironmentStatus status) => status switch
    {
        RemoteEnvironmentStatus.Testing or RemoteEnvironmentStatus.Authenticating or RemoteEnvironmentStatus.Connecting => RemoteStatusSeverity.Progress,
        RemoteEnvironmentStatus.Connected or RemoteEnvironmentStatus.Ready => RemoteStatusSeverity.Good,
        RemoteEnvironmentStatus.Reconnecting or RemoteEnvironmentStatus.ReadyWithWarnings or RemoteEnvironmentStatus.HostKeyUnconfirmed or RemoteEnvironmentStatus.Interrupted
            or RemoteEnvironmentStatus.NotRunning => RemoteStatusSeverity.Warning,
        RemoteEnvironmentStatus.HostKeyChanged or RemoteEnvironmentStatus.AuthFailed or RemoteEnvironmentStatus.Unreachable
            or RemoteEnvironmentStatus.VersionMismatch or RemoteEnvironmentStatus.NeedsSetup
            or RemoteEnvironmentStatus.RuntimeUnavailable or RemoteEnvironmentStatus.NotFound => RemoteStatusSeverity.Error,
        _ => RemoteStatusSeverity.Neutral,
    };

    /// <summary>Tells whether a session cannot start on a host in this status until the user fixes something.</summary>
    /// <param name="status">The status.</param>
    /// <returns><see langword="true"/> for the failed test outcomes.</returns>
    public static bool BlocksStart(RemoteEnvironmentStatus status) => status is
        RemoteEnvironmentStatus.HostKeyUnconfirmed or RemoteEnvironmentStatus.HostKeyChanged or RemoteEnvironmentStatus.AuthFailed
        or RemoteEnvironmentStatus.Unreachable or RemoteEnvironmentStatus.VersionMismatch or RemoteEnvironmentStatus.NeedsSetup
        or RemoteEnvironmentStatus.RuntimeUnavailable or RemoteEnvironmentStatus.NotRunning or RemoteEnvironmentStatus.NotFound;

    /// <summary>Maps the report of a connection test to a status.</summary>
    /// <param name="report">The report.</param>
    /// <returns>The status.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="report"/> is null.</exception>
    public static RemoteEnvironmentStatus FromReport(RemotePreflightReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        if (report.Succeeded)
        {
            return report.Diagnoses.Count == 0 ? RemoteEnvironmentStatus.Ready : RemoteEnvironmentStatus.ReadyWithWarnings;
        }

        var first = report.Diagnoses.FirstOrDefault(diagnosis => diagnosis.Severity == DiagnosisSeverity.Error);
        return first is null ? RemoteEnvironmentStatus.NeedsSetup : FromKind(first.Kind);
    }

    /// <summary>Maps the live state of a session connection to a status.</summary>
    /// <param name="state">The connection state.</param>
    /// <param name="turnInterrupted">Whether a turn was interrupted by the loss of the connection.</param>
    /// <returns>The status.</returns>
    public static RemoteEnvironmentStatus FromConnection(RemoteConnectionState state, bool turnInterrupted) => state switch
    {
        RemoteConnectionState.Connecting => RemoteEnvironmentStatus.Connecting,
        RemoteConnectionState.Connected => RemoteEnvironmentStatus.Connected,
        RemoteConnectionState.Reconnecting => RemoteEnvironmentStatus.Reconnecting,
        RemoteConnectionState.Unreachable => turnInterrupted ? RemoteEnvironmentStatus.Interrupted : RemoteEnvironmentStatus.Unreachable,
        _ => turnInterrupted ? RemoteEnvironmentStatus.Interrupted : RemoteEnvironmentStatus.Disconnected,
    };

    private static RemoteEnvironmentStatus FromKind(RemoteDiagnosisKind kind) => kind switch
    {
        RemoteDiagnosisKind.HostKeyUnknown => RemoteEnvironmentStatus.HostKeyUnconfirmed,
        RemoteDiagnosisKind.HostKeyChanged => RemoteEnvironmentStatus.HostKeyChanged,
        RemoteDiagnosisKind.AuthFailed => RemoteEnvironmentStatus.AuthFailed,
        RemoteDiagnosisKind.HostUnreachable or RemoteDiagnosisKind.SshMissing => RemoteEnvironmentStatus.Unreachable,
        RemoteDiagnosisKind.OmpMissing or RemoteDiagnosisKind.OmpTooOld or RemoteDiagnosisKind.BunMissing => RemoteEnvironmentStatus.VersionMismatch,
        RemoteDiagnosisKind.ContainerRuntimeMissing or RemoteDiagnosisKind.ContainerRuntimeUnavailable or RemoteDiagnosisKind.WslMissing => RemoteEnvironmentStatus.RuntimeUnavailable,
        RemoteDiagnosisKind.ContainerNotRunning => RemoteEnvironmentStatus.NotRunning,
        RemoteDiagnosisKind.ContainerMissing or RemoteDiagnosisKind.WslDistroMissing => RemoteEnvironmentStatus.NotFound,
        _ => RemoteEnvironmentStatus.NeedsSetup,
    };

    private static string ReconnectText(int attempt) => attempt > 0
        ? string.Create(CultureInfo.InvariantCulture, $"The connection dropped. Trying to reconnect (attempt {attempt}).")
        : "The connection dropped. Trying to reconnect.";
}
