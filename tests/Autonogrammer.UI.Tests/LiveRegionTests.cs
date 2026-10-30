using Autonogrammer.Contracts.Agents;
using Autonogrammer.Presentation.AgentPanel;
using Autonogrammer.Presentation.Approvals;
using Autonogrammer.Presentation.Changes;
using Autonogrammer.UI.Components;
using Autonogrammer.UI.Components.Approvals;
using Autonogrammer.UI.Components.Changes;
using Autonogrammer.UI.Components.Thread;
using Autonogrammer.UI.Tests.Fakes;
using Bunit;
using Shouldly;
using Xunit;

namespace Autonogrammer.UI.Tests;

/// <summary>
/// The thread, the approvals and the Changes bar announce through persistent polite regions whose text changes (WCAG 4.1.3): a notice is never a
/// <c>role="status"</c> node that is inserted together with its text, because assistive technology announces a change inside a region it already knows.
/// </summary>
public sealed class LiveRegionTests : UiTestContext
{
    private static async Task<(PanelHarness Harness, AgentSessionViewModel Session)> OpenSessionAsync(SessionLifecycle lifecycle)
    {
        var harness = PanelHarness.With(("a", lifecycle, "Alpha"));
        harness.Host.Bridge = null;
        var session = await harness.SelectAsync("a").ConfigureAwait(true);
        session.Session.MarkHistoryLoaded();
        return (harness, session);
    }

    /// <summary>The thread's notice, run summary and header notice are in the page from the first render, empty and collapsed, and each is a polite status.</summary>
    [Fact]
    public async Task TheThreadKeepsItsNoticeRegionsInThePageWhileTheyAreEmpty()
    {
        var (harness, session) = await OpenSessionAsync(SessionLifecycle.Running).ConfigureAwait(true);
        using var owned = harness;

        var cut = Render<ThreadView>(parameters => parameters.Add(p => p.Session, session));

        foreach (var selector in new[] { ".autonogrammer-thread-notice", ".autonogrammer-run-summary" })
        {
            var region = cut.Find(selector);
            region.GetAttribute("role").ShouldBe("status", $"{selector} must be a status region");
            region.GetAttribute("aria-live").ShouldBe("polite");
            region.GetAttribute("data-empty").ShouldBe("true", $"{selector} must be collapsed while it is empty");
            region.TextContent.Trim().ShouldBeEmpty();
        }

        var headerNotice = cut.Find($"[role='status'][aria-label='{AgentPanelViewText.PanelNoticeName}']");
        headerNotice.TextContent.ShouldBeEmpty();
        cut.FindAll(".autonogrammer-panel-notice").ShouldBeEmpty();
    }

    /// <summary>Closing the agent changes the text of a region that was already in the page; the visible closed note carries no role of its own.</summary>
    [Fact]
    public async Task AClosedAgentIsAnnouncedThroughAPersistentRegion()
    {
        var (harness, session) = await OpenSessionAsync(SessionLifecycle.Paused).ConfigureAwait(true);
        using var owned = harness;
        var cut = Render<ThreadView>(parameters => parameters.Add(p => p.Session, session));
        cut.FindAll(".autonogrammer-thread > .autonogrammer-live-region")[0].TextContent.ShouldBeEmpty();

        await harness.Panel.CloseSessionAsync("a", Xunit.TestContext.Current.CancellationToken).ConfigureAwait(true);

        await cut.WaitForAssertionAsync(() => cut.FindAll(".autonogrammer-thread > .autonogrammer-live-region")[0].TextContent.ShouldContain(AgentPanelCopy.ClosedHeading, Case.Sensitive)).ConfigureAwait(true);
        cut.Find(".autonogrammer-closed-note").GetAttribute("role").ShouldBeNull();
    }

    /// <summary>A pending approval is announced by a region that exists before the first request, and its message region is in the row from the start.</summary>
    [Fact]
    public async Task PendingApprovalsAreAnnouncedByAPersistentRegion()
    {
        var (harness, session) = await OpenSessionAsync(SessionLifecycle.Running).ConfigureAwait(true);
        using var owned = harness;
        var cut = Render<ToolApprovalList>(parameters => parameters.Add(p => p.Session, session));
        var region = cut.Find(".autonogrammer-live-region");
        region.GetAttribute("role").ShouldBe("status");
        region.GetAttribute("aria-live").ShouldBe("polite");
        region.TextContent.ShouldBeEmpty();

        session.PendingApprovals.Add(new PendingToolApproval("a", "req-1", "run_shell", "rm -rf build"));

        await cut.WaitForAssertionAsync(() => cut.Find(".autonogrammer-live-region").TextContent.ShouldBe("1 tool approval is waiting for a decision.")).ConfigureAwait(true);
        var message = cut.Find(".autonogrammer-approval-message");
        message.GetAttribute("role").ShouldBe("status");
        message.GetAttribute("data-empty").ShouldBeNull();
        message.TextContent.ShouldBe("Approval needed. The decision is made by policy.");
    }

    /// <summary>The Changes bar's result region outlives the bar, so the answer to the last Keep or Undo is announced even when the bar then disappears.</summary>
    [Fact]
    public void TheChangesAnnouncementRegionOutlivesTheBar()
    {
        using var model = new ChangeSetModel(new MemoryWorkspace());

        var cut = Render<ChangesBar>(parameters => parameters.Add(p => p.Model, model));

        cut.FindAll(".autonogrammer-changes").ShouldBeEmpty();
        var region = cut.Find(".autonogrammer-sr-only[role='status']");
        region.GetAttribute("aria-live").ShouldBe("polite");
        region.GetAttribute("aria-atomic").ShouldBe("true");
    }
}
