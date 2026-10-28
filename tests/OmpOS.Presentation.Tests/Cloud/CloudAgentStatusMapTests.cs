using OmpOS.Presentation.AgentPanel;
using OmpOS.Presentation.Cloud;
using Shouldly;
using Xunit;

namespace OmpOS.Presentation.Tests.Cloud;

/// <summary>What each cloud status says, shows and allows: the words, the glyphs and which commands each status enables.</summary>
public sealed class CloudAgentStatusMapTests
{
    /// <summary>Status, Wake, Stop, Delete, Send a message, Open in Agents.</summary>
    public static TheoryData<CloudAgentStatus, bool, bool, bool, bool, bool> Permissions => new()
    {
        { CloudAgentStatus.Provisioning, false, false, true, false, false },
        { CloudAgentStatus.Starting, false, true, true, false, true },
        { CloudAgentStatus.Running, false, true, true, true, true },
        { CloudAgentStatus.Idle, false, true, true, true, true },
        { CloudAgentStatus.Stopping, false, false, true, false, true },
        { CloudAgentStatus.Stopped, true, false, true, false, true },
        { CloudAgentStatus.Failed, false, false, true, false, true },
        { CloudAgentStatus.Deleting, false, false, false, false, false },
        { CloudAgentStatus.Orphaned, false, false, true, false, false },
        { CloudAgentStatus.LimitReached, false, false, true, false, true },
    };

    /// <summary>The ten statuses read as ten different phrases, each with its own explanation and a known glyph.</summary>
    [Fact]
    public void EveryStatusHasItsOwnWordsExplanationAndGlyph()
    {
        var statuses = CloudAgentStatusMap.All;

        statuses.Count.ShouldBe(10);
        statuses.Select(CloudAgentStatusMap.TextOf).Distinct(StringComparer.Ordinal).Count().ShouldBe(10);
        statuses.Select(CloudAgentStatusMap.DescriptionOf).Distinct(StringComparer.Ordinal).Count().ShouldBe(10);
        foreach (var status in statuses)
        {
            CloudAgentStatusMap.TextOf(status).ShouldNotBeNullOrWhiteSpace();
            (string.Equals(CloudAgentStatusMap.IconOf(status), StatusIcon.Info, StringComparison.Ordinal)).ShouldBeFalse($"{status} has its own glyph");
        }
    }

    /// <summary>A value outside the enum reads as unknown, shows the unknown glyph and allows nothing.</summary>
    [Fact]
    public void AnUndefinedStatusReadsAsUnknownAndAllowsNothing()
    {
        var undefined = (CloudAgentStatus)99;

        CloudAgentStatusMap.TextOf(undefined).ShouldBe(CloudCopy.StatusUnknown);
        CloudAgentStatusMap.IconOf(undefined).ShouldBe(StatusIcon.Info);
        CloudAgentStatusMap.CanDelete(undefined).ShouldBeFalse();
        CloudAgentStatusMap.CanOpenInAgents(undefined).ShouldBeFalse();
        CloudAgentStatusMap.CanWake(undefined).ShouldBeFalse();
        CloudAgentStatusMap.CanStop(undefined).ShouldBeFalse();
    }

    /// <summary>Each status enables exactly the commands that make sense for it: only a stopped worker wakes, only a live one stops or hears a message.</summary>
    [Theory]
    [MemberData(nameof(Permissions))]
    public void EachStatusEnablesTheRightCommands(CloudAgentStatus status, bool wake, bool stop, bool delete, bool prompt, bool open)
    {
        CloudAgentStatusMap.CanWake(status).ShouldBe(wake);
        CloudAgentStatusMap.CanStop(status).ShouldBe(stop);
        CloudAgentStatusMap.CanDelete(status).ShouldBe(delete);
        CloudAgentStatusMap.CanPrompt(status).ShouldBe(prompt);
        CloudAgentStatusMap.CanOpenInAgents(status).ShouldBe(open);
    }

    /// <summary>Provisioning, starting, stopping and deleting pass by themselves; the others wait for the user or the service.</summary>
    [Fact]
    public void OnlyThePassingStatusesAreTransitional()
    {
        CloudAgentStatusMap.All.Where(CloudAgentStatusMap.IsTransitional).ShouldBe(
            [CloudAgentStatus.Provisioning, CloudAgentStatus.Starting, CloudAgentStatus.Stopping, CloudAgentStatus.Deleting]);
    }

    /// <summary>The rail reads each cloud status with its own words and the glyph of the page, and places it in the group that tells the user what to do.</summary>
    [Theory]
    [InlineData(CloudAgentStatus.Provisioning, AgentRowStatus.Queued, AgentRowGroup.Running)]
    [InlineData(CloudAgentStatus.Starting, AgentRowStatus.Queued, AgentRowGroup.Running)]
    [InlineData(CloudAgentStatus.Running, AgentRowStatus.Running, AgentRowGroup.Running)]
    [InlineData(CloudAgentStatus.Idle, AgentRowStatus.Ready, AgentRowGroup.Idle)]
    [InlineData(CloudAgentStatus.Stopping, AgentRowStatus.Paused, AgentRowGroup.Idle)]
    [InlineData(CloudAgentStatus.Stopped, AgentRowStatus.Paused, AgentRowGroup.Idle)]
    [InlineData(CloudAgentStatus.Failed, AgentRowStatus.Failed, AgentRowGroup.Finished)]
    [InlineData(CloudAgentStatus.Deleting, AgentRowStatus.Closed, AgentRowGroup.Idle)]
    [InlineData(CloudAgentStatus.Orphaned, AgentRowStatus.Lost, AgentRowGroup.Lost)]
    [InlineData(CloudAgentStatus.LimitReached, AgentRowStatus.NeedsApproval, AgentRowGroup.NeedsYou)]
    public void TheRailMapsEachStatusToARowStatusAndGroup(CloudAgentStatus status, AgentRowStatus row, AgentRowGroup group)
    {
        var mapping = CloudAgentRailMap.Resolve(status);

        mapping.Status.ShouldBe(row);
        mapping.Group.ShouldBe(group);
        mapping.StatusText.ShouldBe(CloudAgentStatusMap.TextOf(status));
        mapping.IconKey.ShouldBe(CloudAgentStatusMap.IconOf(status));
        mapping.RuntimeStatus.ShouldNotBeNull();
    }

    /// <summary>An undefined status is unknown in the rail and has no runtime status, so no card claims to know it.</summary>
    [Fact]
    public void AnUndefinedStatusIsUnknownInTheRail()
    {
        var mapping = CloudAgentRailMap.Resolve((CloudAgentStatus)99);

        mapping.Status.ShouldBe(AgentRowStatus.Unknown);
        mapping.RuntimeStatus.ShouldBeNull();
        mapping.Group.ShouldBe(AgentRowGroup.Idle);
    }

    /// <summary>A cloud row says it is a cloud agent in its accessible name, followed by the status and the time.</summary>
    [Fact]
    public void TheRailNameSaysTheRowIsACloudAgent()
    {
        CloudAgentRailMap.AccessibleName("Fix the build", CloudAgentStatus.Idle, "5 min ago")
            .ShouldBe("Fix the build, cloud agent, Idle, 5 min ago");
    }
}
