using OmpOS.Presentation.Kanban;
using OmpOS.Presentation.Remote;

namespace OmpOS.Presentation.AgentPanel;

/// <summary>
/// How the connection state of a remote (ssh) session appears on its row of the Agents rail: the status words and glyph, the
/// <see cref="AgentRowStatus"/> that sets the tone, the rail group and the accessible name. It is additive to <see cref="AgentRowStatusMap"/>:
/// the rail asks this map only for rows whose session has a connection state, and the state replaces the row's own status while it lasts.
/// </summary>
/// <remarks>
/// A state that says nothing about the connection of a session (<see cref="RemoteEnvironmentStatus.Connected"/>, which is the normal case,
/// and the test outcomes NotChecked, Testing, Ready and ReadyWithWarnings) has no mapping: the row keeps the status its session has.
/// </remarks>
public static class RemoteRailMap
{
    private const string SshSessionName = "ssh session";

    /// <summary>Resolves the rail look of a remote connection state.</summary>
    /// <param name="status">The state.</param>
    /// <returns>The mapping, or null when the state leaves the row's own status unchanged.</returns>
    public static RemoteRailMapping? Resolve(RemoteEnvironmentStatus status)
    {
        var label = RemoteStatusDescriber.LabelOf(status);
        return status switch
        {
            RemoteEnvironmentStatus.Authenticating or RemoteEnvironmentStatus.Connecting or RemoteEnvironmentStatus.Reconnecting =>
                new RemoteRailMapping(AgentRowStatus.Queued, RuntimeStatus.Queued, AgentRowGroup.Running, label, StatusIcon.Clock),
            RemoteEnvironmentStatus.HostKeyUnconfirmed =>
                new RemoteRailMapping(AgentRowStatus.NeedsApproval, RuntimeStatus.NeedsAttention, AgentRowGroup.NeedsYou, label, StatusIcon.Alert),
            RemoteEnvironmentStatus.Interrupted =>
                new RemoteRailMapping(AgentRowStatus.WaitingForYou, RuntimeStatus.AwaitingInput, AgentRowGroup.NeedsYou, RemoteCopy.InterruptedBadge, StatusIcon.Bubble),
            RemoteEnvironmentStatus.AuthFailed or RemoteEnvironmentStatus.HostKeyChanged or RemoteEnvironmentStatus.VersionMismatch or RemoteEnvironmentStatus.NeedsSetup =>
                new RemoteRailMapping(AgentRowStatus.Failed, RuntimeStatus.Failed, AgentRowGroup.Finished, label, StatusIcon.Cross),
            RemoteEnvironmentStatus.Unreachable =>
                new RemoteRailMapping(AgentRowStatus.Lost, RuntimeStatus.Unreachable, AgentRowGroup.Lost, label, StatusIcon.Question),
            RemoteEnvironmentStatus.Disconnected =>
                new RemoteRailMapping(AgentRowStatus.Closed, RuntimeStatus.Paused, AgentRowGroup.Idle, label, StatusIcon.Closed),
            _ => null,
        };
    }

    /// <summary>Builds the accessible name of a remote session row: the title, that it is an ssh session, the full connection label and the last activity.</summary>
    /// <param name="title">The row title.</param>
    /// <param name="status">The connection state.</param>
    /// <param name="lastActivityText">The relative time of the last activity.</param>
    /// <returns>The accessible name, for example <c>Fix the build, ssh session, Disconnected, turn interrupted, 5 min ago</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="title"/> or <paramref name="lastActivityText"/> is null.</exception>
    public static string AccessibleName(string title, RemoteEnvironmentStatus status, string lastActivityText)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(lastActivityText);

        return $"{title}, {SshSessionName}, {RemoteStatusDescriber.LabelOf(status)}, {lastActivityText}";
    }
}
