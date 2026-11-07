using Autonogrammer.Contracts.Agents;
using Autonogrammer.Contracts.Cowork;
using Autonogrammer.Contracts.Sessions;
using Autonogrammer.Presentation.AgentPanel;
using Autonogrammer.Presentation.Cowork;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Cowork;

/// <summary>Hidden-surface counts and the polite chip.</summary>
public sealed class SurfaceActivityModelTests
{
    /// <summary>Each surface counts running, waiting-for-decision and failed rows from its own catalog.</summary>
    [Fact]
    public void CountsRunningNeedsApprovalAndFailedPerSurface()
    {
        var code = new List<AgentRow>
        {
            Row(AgentRowStatus.Running),
            Row(AgentRowStatus.Queued),
            Row(AgentRowStatus.NeedsApproval),
            Row(AgentRowStatus.Failed),
            Row(AgentRowStatus.Completed),
        };
        var cowork = new ScriptedCatalog(
        [
            Summary(CoworkSessionStatus.Running),
            Summary(CoworkSessionStatus.WaitingForDecision),
            Summary(CoworkSessionStatus.Failed),
            Summary(CoworkSessionStatus.Paused),
        ]);
        var surface = new SurfaceSwitchModel();
        using var activity = new SurfaceActivityModel(surface, () => code, _ => { }, _ => { }, cowork);

        activity.CodeRunning.ShouldBe(2);
        activity.CodeNeedsApproval.ShouldBe(1);
        activity.CodeFailed.ShouldBe(1);
        activity.CoworkRunning.ShouldBe(1);
        activity.CoworkNeedsApproval.ShouldBe(1);
        activity.CoworkFailed.ShouldBe(1);
    }

    /// <summary>The hidden surface chip uses the quoted shapes, and the announcement is that status, politely.</summary>
    [Fact]
    public void HiddenSurfaceChipUsesTheQuotedShapesAndAPoliteAnnouncement()
    {
        var code = new List<AgentRow> { Row(AgentRowStatus.NeedsApproval) };
        var cowork = new ScriptedCatalog([Summary(CoworkSessionStatus.Running), Summary(CoworkSessionStatus.Running)]);
        var surface = new SurfaceSwitchModel();
        surface.SetCoworkAvailable(true);
        using var activity = new SurfaceActivityModel(surface, () => code, _ => { }, _ => { }, cowork);

        activity.ChipText.ShouldBe("Cowork, 2 running");
        activity.PoliteAnnouncement.ShouldBe("Cowork, 2 running");
        activity.PoliteAnnouncement.ShouldNotContain("!");
        SurfaceActivityModel.LiveSetting.ShouldBe(CoworkCopy.PoliteLiveSetting);
        SurfaceActivityModel.LiveSetting.Contains("Assertive", StringComparison.Ordinal).ShouldBeFalse();

        surface.Select(SessionSurface.Cowork);
        activity.ChipText.ShouldBe("Code, needs approval");
        activity.PoliteAnnouncement.ShouldBe("Code, needs approval");
    }

    private static AgentRow Row(AgentRowStatus status) => new()
    {
        Summary = new SessionSummary
        {
            Id = "code-" + status,
            Title = "Code",
            Lifecycle = SessionLifecycle.Created,
            CreatedAt = DateTimeOffset.UnixEpoch,
            LastActivity = DateTimeOffset.UnixEpoch,
        },
        Id = "code-" + status,
        Title = "Code",
        Status = status,
        Group = AgentRowGroup.Running,
        StatusText = status.ToString(),
        IconKey = "dot",
        LastActivityText = "now",
        IsLongRunning = false,
        IsStale = false,
        IsLost = false,
        AccessibleName = "Code",
    };

    private static CoworkSessionSummary Summary(CoworkSessionStatus status) => new()
    {
        Id = "cowork-" + status,
        Title = "Task",
        Status = status,
        CreatedAt = DateTimeOffset.UnixEpoch,
        UpdatedAt = DateTimeOffset.UnixEpoch,
    };

    private sealed class ScriptedCatalog(IReadOnlyList<CoworkSessionSummary> rows) : ICoworkSessionCatalog
    {
        public event EventHandler? Changed;

        public IReadOnlyList<CoworkSessionSummary> Snapshot() => rows;

        public void Touch() => Changed?.Invoke(this, EventArgs.Empty);
    }
}
