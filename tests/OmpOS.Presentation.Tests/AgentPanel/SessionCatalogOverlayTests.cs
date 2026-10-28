using OmpOS.Contracts.Agents;
using OmpOS.Presentation.AgentPanel;
using OmpOS.Presentation.Cloud;
using OmpOS.Presentation.Remote;
using OmpOS.Presentation.Tests.Cloud;
using Shouldly;
using Xunit;
using static OmpOS.Presentation.Tests.AgentPanel.AgentPanelTestData;

namespace OmpOS.Presentation.Tests.AgentPanel;

/// <summary>
/// The two overlays of the Agents rail: cloud agents (their status comes from <see cref="CloudAgentRailMap"/>) and the connection state of a
/// remote session (from <see cref="RemoteRailMap"/>). Each replaces the status of the row it names, survives catalog reloads, is pruned when the
/// session leaves the catalog and never touches another row.
/// </summary>
public sealed class SessionCatalogOverlayTests
{
    private readonly ManualTimeProvider _clock = new();
    private readonly FakeSessionCatalog _catalog = new();

    /// <summary>Gets one case for every connection state the rail maps, with the row look it must give.</summary>
    public static TheoryData<RemoteEnvironmentStatus, AgentRowStatus, AgentRowGroup, string, string> MappedStates => new()
    {
        { RemoteEnvironmentStatus.Authenticating, AgentRowStatus.Queued, AgentRowGroup.Running, "Signing in", StatusIcon.Clock },
        { RemoteEnvironmentStatus.Connecting, AgentRowStatus.Queued, AgentRowGroup.Running, "Connecting", StatusIcon.Clock },
        { RemoteEnvironmentStatus.Reconnecting, AgentRowStatus.Queued, AgentRowGroup.Running, "Reconnecting", StatusIcon.Clock },
        { RemoteEnvironmentStatus.HostKeyUnconfirmed, AgentRowStatus.NeedsApproval, AgentRowGroup.NeedsYou, "Host key not trusted yet", StatusIcon.Alert },
        { RemoteEnvironmentStatus.Interrupted, AgentRowStatus.WaitingForYou, AgentRowGroup.NeedsYou, "Interrupted", StatusIcon.Bubble },
        { RemoteEnvironmentStatus.AuthFailed, AgentRowStatus.Failed, AgentRowGroup.Finished, "Sign-in failed", StatusIcon.Cross },
        { RemoteEnvironmentStatus.HostKeyChanged, AgentRowStatus.Failed, AgentRowGroup.Finished, "Host key changed", StatusIcon.Cross },
        { RemoteEnvironmentStatus.Unreachable, AgentRowStatus.Lost, AgentRowGroup.Lost, "Unreachable", StatusIcon.Question },
        { RemoteEnvironmentStatus.Disconnected, AgentRowStatus.Closed, AgentRowGroup.Idle, "Disconnected", StatusIcon.Closed },
    };

    /// <summary>A cloud agent's session row takes the status words, glyph, status, group and accessible name of the cloud rail map; other rows stay as they were.</summary>
    [Fact]
    public void ACloudAgentRowTakesItsLookFromTheCloudMapAndOtherRowsAreUnchanged()
    {
        _catalog.Sessions.AddRange([Session("cloud-1", SessionLifecycle.Completed, minutesAgo: 3), Session("plain", SessionLifecycle.Completed, minutesAgo: 4)]);
        using var model = NewModel();
        var plainBefore = Find(model, "plain");

        model.SetCloudAgents([CloudFixtures.Agent("1", CloudAgentStatus.LimitReached, sessionId: "cloud-1")]);

        var cloud = Find(model, "cloud-1");
        cloud.Status.ShouldBe(AgentRowStatus.NeedsApproval);
        cloud.Group.ShouldBe(AgentRowGroup.NeedsYou);
        cloud.StatusText.ShouldBe("Limit reached");
        cloud.IconKey.ShouldBe(StatusIcon.Alert);
        cloud.AccessibleName.ShouldBe("Agent cloud-1, cloud agent, Limit reached, 3 min ago");
        (Find(model, "plain") with { IsGroupStart = plainBefore.IsGroupStart }).ShouldBe(plainBefore);
        model.Rows.Select(row => row.Id).ShouldBe(["cloud-1", "plain"]);
    }

    /// <summary>A cloud agent that runs is Running in the rail, and the quiet flag then follows the session's last activity like any Running row.</summary>
    [Fact]
    public void ARunningCloudAgentIsRunningAndCanBeStale()
    {
        _catalog.Sessions.Add(Session("cloud-1", SessionLifecycle.Completed, minutesAgo: 2));
        using var model = NewModel();

        model.SetCloudAgents([CloudFixtures.Agent("1", CloudAgentStatus.Running, sessionId: "cloud-1")]);

        var row = Find(model, "cloud-1");
        row.Status.ShouldBe(AgentRowStatus.Running);
        row.Group.ShouldBe(AgentRowGroup.Running);
        row.IsStale.ShouldBeTrue();
        row.AccessibleName.ShouldBe("Agent cloud-1, cloud agent, Running, 2 min ago, quiet for 2 min");
    }

    /// <summary>Records with no session identifier or an unknown session change nothing, and an empty list clears every cloud look.</summary>
    [Fact]
    public void CloudRecordsWithoutAMatchingSessionChangeNothingAndAnEmptyListClears()
    {
        _catalog.Sessions.Add(Session("cloud-1", SessionLifecycle.Completed, minutesAgo: 3));
        using var model = NewModel();
        var before = model.Rows.ToList();

        model.SetCloudAgents([CloudFixtures.Agent("2", CloudAgentStatus.Failed, sessionId: "other"), CloudFixtures.Agent("3", CloudAgentStatus.Failed) with { SessionId = null }]);
        model.Rows.ShouldBe(before);

        model.SetCloudAgents([CloudFixtures.Agent("1", CloudAgentStatus.Failed, sessionId: "cloud-1")]);
        Find(model, "cloud-1").Status.ShouldBe(AgentRowStatus.Failed);

        model.SetCloudAgents([]);
        model.Rows.ShouldBe(before);
    }

    /// <summary>The cloud look survives a catalog reload and applies to a session that registers after the host reported it.</summary>
    [Fact]
    public void TheCloudLookSurvivesReloadsAndAppliesToALateSession()
    {
        _catalog.Sessions.Add(Session("cloud-1", SessionLifecycle.Completed, minutesAgo: 3));
        using var model = NewModel();
        model.SetCloudAgents(
        [
            CloudFixtures.Agent("1", CloudAgentStatus.Stopped, sessionId: "cloud-1"),
            CloudFixtures.Agent("2", CloudAgentStatus.Provisioning, sessionId: "cloud-2"),
        ]);

        _catalog.Put(Session("cloud-1", SessionLifecycle.Failed, minutesAgo: 1));
        Find(model, "cloud-1").Status.ShouldBe(AgentRowStatus.Paused);
        Find(model, "cloud-1").StatusText.ShouldBe("Stopped");

        _catalog.Put(Session("cloud-2", SessionLifecycle.Created, minutesAgo: 1));
        Find(model, "cloud-2").Status.ShouldBe(AgentRowStatus.Queued);
        Find(model, "cloud-2").StatusText.ShouldBe("Provisioning");
    }

    /// <summary>The cloud look of a session that left the catalog is dropped: the session shows its own status if it comes back.</summary>
    [Fact]
    public void ACloudLookIsPrunedWhenItsSessionLeavesTheCatalog()
    {
        _catalog.Sessions.Add(Session("cloud-1", SessionLifecycle.Completed, minutesAgo: 3));
        using var model = NewModel();
        model.SetCloudAgents([CloudFixtures.Agent("1", CloudAgentStatus.Failed, sessionId: "cloud-1")]);

        _catalog.Remove("cloud-1");
        _catalog.Put(Session("cloud-1", SessionLifecycle.Completed, minutesAgo: 3));

        Find(model, "cloud-1").Status.ShouldBe(AgentRowStatus.Completed);
        Find(model, "cloud-1").StatusText.ShouldBe("Completed");
    }

    /// <summary>Every mapped connection state gives the row its status, group, words, glyph and a card state of the matching runtime status.</summary>
    [Theory]
    [MemberData(nameof(MappedStates))]
    public void AConnectionStateGivesTheRowItsLook(RemoteEnvironmentStatus state, AgentRowStatus status, AgentRowGroup group, string text, string icon)
    {
        _catalog.Sessions.Add(Session("ssh-1", SessionLifecycle.Running, minutesAgo: 3));
        using var model = NewModel();

        model.SetRemoteStatus("ssh-1", state);

        var row = Find(model, "ssh-1");
        row.Status.ShouldBe(status);
        row.Group.ShouldBe(group);
        row.StatusText.ShouldBe(text);
        row.IconKey.ShouldBe(icon);
        row.Card.ShouldNotBeNull();
        row.IsStale.ShouldBeFalse();
        row.IsLost.ShouldBe(status == AgentRowStatus.Lost);
        row.AccessibleName.ShouldContain(RemoteStatusDescriber.LabelOf(state), Case.Sensitive);
    }

    /// <summary>An interrupted turn reads Interrupted on the row and the full Disconnected, turn interrupted in the accessible name.</summary>
    [Fact]
    public void AnInterruptedTurnReadsInterruptedAndItsAccessibleNameCarriesTheFullLabel()
    {
        _catalog.Sessions.Add(Session("ssh-1", SessionLifecycle.Running, minutesAgo: 3));
        using var model = NewModel();

        model.SetRemoteStatus("ssh-1", RemoteEnvironmentStatus.Interrupted);

        var row = Find(model, "ssh-1");
        row.StatusText.ShouldBe(RemoteCopy.InterruptedBadge);
        row.AccessibleName.ShouldBe("Agent ssh-1, ssh session, Disconnected, turn interrupted, 3 min ago");
    }

    /// <summary>States that say nothing about a session's connection leave the row exactly as its session makes it.</summary>
    [Theory]
    [InlineData(RemoteEnvironmentStatus.Connected)]
    [InlineData(RemoteEnvironmentStatus.NotChecked)]
    [InlineData(RemoteEnvironmentStatus.Testing)]
    [InlineData(RemoteEnvironmentStatus.Ready)]
    [InlineData(RemoteEnvironmentStatus.ReadyWithWarnings)]
    public void AStateThatIsNotAConnectionProblemLeavesTheRowAlone(RemoteEnvironmentStatus state)
    {
        _catalog.Sessions.Add(Session("ssh-1", SessionLifecycle.Running, minutesAgo: 0));
        using var model = NewModel();
        var before = Find(model, "ssh-1");

        model.SetRemoteStatus("ssh-1", state);

        Find(model, "ssh-1").ShouldBe(before);
        RemoteRailMap.Resolve(state).ShouldBeNull();
    }

    /// <summary>Setting null clears the state; a state for an unknown session changes no row; the other rows never change.</summary>
    [Fact]
    public void ClearingRestoresTheRowAndOtherRowsAreNeverTouched()
    {
        _catalog.Sessions.AddRange([Session("ssh-1", SessionLifecycle.Running, minutesAgo: 0), Session("local", SessionLifecycle.Running, minutesAgo: 1)]);
        using var model = NewModel();
        var before = model.Rows.ToList();

        model.SetRemoteStatus("missing", RemoteEnvironmentStatus.Unreachable);
        model.Rows.ShouldBe(before);

        model.SetRemoteStatus("ssh-1", RemoteEnvironmentStatus.Unreachable);
        Find(model, "ssh-1").Status.ShouldBe(AgentRowStatus.Lost);
        (Find(model, "local") with { IsGroupStart = false }).ShouldBe(before.Single(row => string.Equals(row.Id, "local", StringComparison.Ordinal)));

        model.SetRemoteStatus("ssh-1", null);
        model.Rows.ShouldBe(before);
    }

    /// <summary>A connection state moves the row to its new group in the usual order and starts that group's header.</summary>
    [Fact]
    public void AConnectionStateMovesTheRowToItsGroup()
    {
        _catalog.Sessions.AddRange(
        [
            Session("local-run", SessionLifecycle.Running, minutesAgo: 0),
            Session("ssh-1", SessionLifecycle.Running, minutesAgo: 1),
            Session("done", SessionLifecycle.Completed, minutesAgo: 2),
        ]);
        using var model = NewModel();
        model.Rows.Select(row => row.Id).ShouldBe(["local-run", "ssh-1", "done"]);

        model.SetRemoteStatus("ssh-1", RemoteEnvironmentStatus.Interrupted);

        model.Rows.Select(row => row.Id).ShouldBe(["ssh-1", "local-run", "done"]);
        model.Rows.Where(row => row.IsGroupStart).Select(row => row.GroupHeader).ShouldBe(["Needs you", "Running", "Finished"]);
    }

    /// <summary>The state survives catalog reloads and clock refreshes, and is pruned when its session leaves the catalog.</summary>
    [Fact]
    public void TheStateSurvivesReloadsAndIsPrunedWhenTheSessionLeaves()
    {
        _catalog.Sessions.Add(Session("ssh-1", SessionLifecycle.Running, minutesAgo: 0));
        using var model = NewModel();
        model.SetRemoteStatus("ssh-1", RemoteEnvironmentStatus.Reconnecting);

        _catalog.Put(Session("ssh-1", SessionLifecycle.Running, minutesAgo: 0));
        model.Refresh();
        model.RefreshTimes();
        Find(model, "ssh-1").StatusText.ShouldBe("Reconnecting");

        _catalog.Remove("ssh-1");
        _catalog.Put(Session("ssh-1", SessionLifecycle.Running, minutesAgo: 0));

        Find(model, "ssh-1").StatusText.ShouldBe("Running");
    }

    /// <summary>A state reported before its session registers is kept and shows once the session is listed.</summary>
    [Fact]
    public void AStateForASessionNotListedYetShowsWhenItIsListed()
    {
        _catalog.Sessions.Add(Session("local", SessionLifecycle.Running, minutesAgo: 0));
        using var model = NewModel();

        model.SetRemoteStatus("ssh-1", RemoteEnvironmentStatus.Connecting);
        model.Refresh();
        _catalog.Put(Session("ssh-1", SessionLifecycle.Created, minutesAgo: 0));

        Find(model, "ssh-1").StatusText.ShouldBe("Connecting");
    }

    /// <summary>A connection state wins over a cloud look on the same row, and the cloud look returns when it clears.</summary>
    [Fact]
    public void AConnectionStateWinsOverACloudLook()
    {
        _catalog.Sessions.Add(Session("s", SessionLifecycle.Completed, minutesAgo: 3));
        using var model = NewModel();
        model.SetCloudAgents([CloudFixtures.Agent("1", CloudAgentStatus.Idle, sessionId: "s")]);

        model.SetRemoteStatus("s", RemoteEnvironmentStatus.Unreachable);
        Find(model, "s").Status.ShouldBe(AgentRowStatus.Lost);

        model.SetRemoteStatus("s", null);
        Find(model, "s").StatusText.ShouldBe("Idle");
        Find(model, "s").AccessibleName.ShouldBe("Agent s, cloud agent, Idle, 3 min ago");
    }

    /// <summary>Both overlays announce the changed rows once, announce nothing when set again unchanged, and the filter matches the overlaid words.</summary>
    [Fact]
    public void OverlaysAnnounceChangesOnceAndTheFilterMatchesTheirWords()
    {
        _catalog.Sessions.AddRange([Session("ssh-1", SessionLifecycle.Running, minutesAgo: 0), Session("local", SessionLifecycle.Running, minutesAgo: 1)]);
        using var model = NewModel();
        var announced = PropertyChangeRecorder.Record(model);

        model.SetRemoteStatus("ssh-1", RemoteEnvironmentStatus.AuthFailed);
        announced.ShouldAnnounce(nameof(SessionCatalogModel.Rows));
        announced.ShouldAnnounce(nameof(SessionCatalogModel.Sessions));

        announced.Clear();
        model.SetRemoteStatus("ssh-1", RemoteEnvironmentStatus.AuthFailed);
        model.SetCloudAgents([]);
        announced.ShouldBeEmpty();

        model.Filter = "sign-in";
        model.Rows.Select(row => row.Id).ShouldBe(["ssh-1"]);
    }

    /// <summary>The overlay setters reject null arguments.</summary>
    [Fact]
    public void TheSettersRejectNull()
    {
        using var model = NewModel();

        Should.Throw<ArgumentNullException>(() => model.SetCloudAgents(null!));
        Should.Throw<ArgumentNullException>(() => model.SetRemoteStatus(null!, RemoteEnvironmentStatus.Connecting));
    }

    private static AgentRow Find(SessionCatalogModel model, string id) =>
        model.Sessions.Single(row => string.Equals(row.Id, id, StringComparison.Ordinal));

    private SessionCatalogModel NewModel() => new(_catalog, _clock);

    private SessionSummary Session(string id, SessionLifecycle lifecycle, int minutesAgo)
    {
        var now = _clock.GetUtcNow();
        return Summary(id, lifecycle, now.AddMinutes(-minutesAgo - 5), now.AddMinutes(-minutesAgo));
    }
}
