using Autonogrammer.Presentation.Cloud;
using Autonogrammer.Presentation.Kanban;

namespace Autonogrammer.Presentation.AgentPanel;

/// <summary>
/// How a cloud agent appears as a row of the Agents rail: its status words and glyph, the <see cref="AgentRowStatus"/> that sets the
/// tone, the rail group and the accessible name. It is additive to <see cref="AgentRowStatusMap"/>, which is unchanged: the rail asks this
/// map for rows whose session is a cloud agent.
/// </summary>
public static class CloudAgentRailMap
{
    /// <summary>Resolves the rail look of a cloud agent status.</summary>
    /// <param name="status">The status.</param>
    /// <returns>The status words, glyph, row status, runtime status and group.</returns>
    public static CloudRailMapping Resolve(CloudAgentStatus status)
    {
        var text = CloudAgentStatusMap.TextOf(status);
        var icon = CloudAgentStatusMap.IconOf(status);
        return status switch
        {
            CloudAgentStatus.Provisioning or CloudAgentStatus.Starting =>
                new CloudRailMapping(AgentRowStatus.Queued, RuntimeStatus.Queued, AgentRowGroup.Running, text, icon),
            CloudAgentStatus.Running =>
                new CloudRailMapping(AgentRowStatus.Running, RuntimeStatus.Running, AgentRowGroup.Running, text, icon),
            CloudAgentStatus.Idle =>
                new CloudRailMapping(AgentRowStatus.Ready, RuntimeStatus.Queued, AgentRowGroup.Idle, text, icon),
            CloudAgentStatus.Stopping or CloudAgentStatus.Stopped =>
                new CloudRailMapping(AgentRowStatus.Paused, RuntimeStatus.Paused, AgentRowGroup.Idle, text, icon),
            CloudAgentStatus.Deleting =>
                new CloudRailMapping(AgentRowStatus.Closed, RuntimeStatus.Paused, AgentRowGroup.Idle, text, icon),
            CloudAgentStatus.Failed =>
                new CloudRailMapping(AgentRowStatus.Failed, RuntimeStatus.Failed, AgentRowGroup.Finished, text, icon),
            CloudAgentStatus.Orphaned =>
                new CloudRailMapping(AgentRowStatus.Lost, RuntimeStatus.Unreachable, AgentRowGroup.Lost, text, icon),
            CloudAgentStatus.LimitReached =>
                new CloudRailMapping(AgentRowStatus.NeedsApproval, RuntimeStatus.NeedsAttention, AgentRowGroup.NeedsYou, text, icon),
            _ => new CloudRailMapping(AgentRowStatus.Unknown, null, AgentRowGroup.Idle, text, icon),
        };
    }

    /// <summary>Builds the accessible name of a cloud agent row: the title, that it is a cloud agent, the status and the last activity.</summary>
    /// <param name="title">The row title.</param>
    /// <param name="status">The cloud agent status.</param>
    /// <param name="lastActivityText">The relative time of the last activity.</param>
    /// <returns>The accessible name.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="title"/> or <paramref name="lastActivityText"/> is null.</exception>
    public static string AccessibleName(string title, CloudAgentStatus status, string lastActivityText)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(lastActivityText);

        return CloudCopy.RowAccessibleName(title, CloudAgentStatusMap.TextOf(status), lastActivityText);
    }
}
