using Bunit;
using OmpOS.GenUi;
using OmpOS.Presentation.State;
using OmpOS.UI.Components;
using Shouldly;
using Xunit;

namespace OmpOS.UI.Tests;

/// <summary>The awaiting-approval state renders only from the server session status, with no approval action (UI-02).</summary>
public sealed class ApprovalStateTests : UiTestContext
{
    /// <summary>Marking approval enters the state with its announcement and no stop or retry affordance.</summary>
    [Fact]
    public void MarkAwaitingApprovalSetsStateAndAnnouncement()
    {
        var model = new SessionViewModel();
        model.MarkAwaitingApproval();

        model.State.ShouldBe(SessionViewState.AwaitingApproval);
        model.Announcement.ShouldBe("Approval needed to continue.");
        model.CanStop.ShouldBeFalse();
        model.CanRetry.ShouldBeFalse();
    }

    /// <summary>The awaiting-approval state renders a read-only notice and no action buttons.</summary>
    [Fact]
    public void AwaitingApprovalRendersNoticeWithoutActions()
    {
        var cut = Render<SessionView>(parameters => parameters
            .Add(p => p.Tree, UiTree.Empty)
            .Add(p => p.ViewState, SessionViewState.AwaitingApproval));

        cut.Find("[role=\"status\"]").TextContent.ShouldContain("Approval needed to continue.");
        cut.FindAll("button").Count.ShouldBe(0);
        cut.Markup.ShouldNotContain("Unsupported content");
    }

    /// <summary>Reconnecting and offline keep output visible behind a status notice.</summary>
    /// <param name="state">The transport state.</param>
    /// <param name="expected">The expected notice text.</param>
    [Theory]
    [InlineData(SessionViewState.Reconnecting, "Reconnecting…")]
    [InlineData(SessionViewState.Offline, "You are offline.")]
    public void TransportStatesRenderNotices(SessionViewState state, string expected)
    {
        var cut = Render<SessionView>(parameters => parameters
            .Add(p => p.Tree, UiTree.Empty)
            .Add(p => p.ViewState, state));

        cut.Find("[role=\"status\"]").TextContent.ShouldContain(expected);
    }
}
