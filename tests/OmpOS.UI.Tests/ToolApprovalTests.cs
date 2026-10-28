using Bunit;
using OmpOS.Contracts.Agents;
using OmpOS.Presentation.AgentPanel;
using OmpOS.Presentation.Approvals;
using OmpOS.UI.Components.Approvals;
using OmpOS.UI.Tests.Fakes;
using Shouldly;
using Xunit;

namespace OmpOS.UI.Tests;

/// <summary>Pending tool approvals sit inside the thread with Allow once and Deny, call the host through the coordinator, and show every outcome in words (PX-09).</summary>
public sealed class ToolApprovalTests : UiTestContext
{
    private static async Task<(PanelHarness Harness, AgentSessionViewModel Session, PendingToolApproval Approval)> OpenAsync(PanelBridge? bridge)
    {
        var harness = PanelHarness.With(("a", SessionLifecycle.Running, "Alpha"));
        harness.Host.Bridge = bridge;
        var session = await harness.SelectAsync("a").ConfigureAwait(true);
        var approval = new PendingToolApproval("a", "req-1", "run_shell", "rm -rf build");
        session.PendingApprovals.Add(approval);
        return (harness, session, approval);
    }

    private IRenderedComponent<ToolApprovalList> RenderList(AgentSessionViewModel session, TimeProvider? clock = null) =>
        Render<ToolApprovalList>(parameters =>
        {
            parameters.Add(p => p.Session, session);
            if (clock is not null)
            {
                parameters.Add(p => p.Time, clock);
            }
        });

    /// <summary>A pending request names the tool and its arguments, offers Allow once and Deny with the tool in their accessible names, and says how policy applies.</summary>
    [Fact]
    public async Task APendingRequestOffersAllowOnceAndDeny()
    {
        var (harness, session, _) = await OpenAsync(new PanelBridge()).ConfigureAwait(true);
        using var owned = harness;

        var cut = RenderList(session);

        cut.Find("section.ompos-approvals").GetAttribute("aria-label").ShouldBe("Pending tool approvals");
        cut.Find("[role='group']").GetAttribute("aria-label").ShouldBe("Tool approval: run_shell");
        cut.Find(".ompos-approval-tool code").TextContent.ShouldBe("run_shell");
        cut.Find(".ompos-approval-arguments code").TextContent.ShouldBe("rm -rf build");
        var buttons = cut.FindAll(".ompos-approval-actions button");
        buttons.Select(static button => button.TextContent).ShouldBe(["Allow once", "Deny"]);
        buttons.Select(static button => button.GetAttribute("aria-label")).ShouldBe(["Allow once: run_shell", "Deny: run_shell"]);
        cut.Find(".ompos-approval-hint").TextContent.ShouldBe(ToolApprovalRow.PolicyHint);
    }

    /// <summary>Allow once sends the allow decision, and the row then reads as resolved with no buttons.</summary>
    [Fact]
    public async Task AllowOnceIsRecordedAndResolvesTheRow()
    {
        var bridge = new PanelBridge();
        var (harness, session, approval) = await OpenAsync(bridge).ConfigureAwait(true);
        using var owned = harness;
        var cut = RenderList(session);

        await cut.Find("button[aria-label='Allow once: run_shell']").ClickAsync().ConfigureAwait(true);

        bridge.Decisions.ShouldBe(["req-1:Allow"]);
        approval.Status.ShouldBe(ToolApprovalStatus.Allowed);
        await cut.WaitForAssertionAsync(() => cut.Find(".ompos-approval-message").TextContent.ShouldBe("Allowed. The decision is recorded.")).ConfigureAwait(true);
        cut.Find(".ompos-approval-message").GetAttribute("role").ShouldBe("status");
        cut.FindAll(".ompos-approval-actions").ShouldBeEmpty();
    }

    /// <summary>Deny sends the deny decision and resolves the row.</summary>
    [Fact]
    public async Task DenyIsRecordedAndResolvesTheRow()
    {
        var bridge = new PanelBridge();
        var (harness, session, approval) = await OpenAsync(bridge).ConfigureAwait(true);
        using var owned = harness;
        var cut = RenderList(session);

        await cut.Find("button[aria-label='Deny: run_shell']").ClickAsync().ConfigureAwait(true);

        bridge.Decisions.ShouldBe(["req-1:Deny"]);
        approval.Status.ShouldBe(ToolApprovalStatus.Denied);
        await cut.WaitForAssertionAsync(() => cut.Find(".ompos-approval-message").TextContent.ShouldBe("Denied. The decision is recorded.")).ConfigureAwait(true);
    }

    /// <summary>A tool policy does not grant stays pending: the refusal is an alert and the buttons remain, so the user can still deny it.</summary>
    [Fact]
    public async Task APolicyRefusalStaysPendingWithAnAlert()
    {
        var bridge = new PanelBridge { Answer = static (_, _) => Task.FromResult(ToolApprovalOutcome.Refused) };
        var (harness, session, approval) = await OpenAsync(bridge).ConfigureAwait(true);
        using var owned = harness;
        var cut = RenderList(session);

        await cut.Find("button[aria-label='Allow once: run_shell']").ClickAsync().ConfigureAwait(true);

        approval.Status.ShouldBe(ToolApprovalStatus.Refused);
        await cut.WaitForAssertionAsync(() => cut.Find(".ompos-approval-message[role='alert']").TextContent.ShouldBe("Policy does not grant this tool.")).ConfigureAwait(true);
        cut.FindAll(".ompos-approval-actions button").Count.ShouldBe(2);
    }

    /// <summary>A decision the server did not record says so and offers the buttons again.</summary>
    [Fact]
    public async Task AFailedDecisionCanBeTriedAgain()
    {
        var bridge = new PanelBridge { Answer = static (_, _) => Task.FromResult(ToolApprovalOutcome.Failed) };
        var (harness, session, _) = await OpenAsync(bridge).ConfigureAwait(true);
        using var owned = harness;
        var cut = RenderList(session);

        await cut.Find("button[aria-label='Allow once: run_shell']").ClickAsync().ConfigureAwait(true);

        await cut.WaitForAssertionAsync(() => cut.Find(".ompos-approval-message[role='alert']").TextContent.ShouldBe("The decision was not recorded. Try again.")).ConfigureAwait(true);
        bridge.Answer = static (_, _) => Task.FromResult(ToolApprovalOutcome.Accepted);
        await cut.Find("button[aria-label='Allow once: run_shell']").ClickAsync().ConfigureAwait(true);
        bridge.Decisions.ShouldBe(["req-1:Allow", "req-1:Allow"]);
    }

    /// <summary>While the server has not answered the buttons are disabled; after the timeout the row says so and offers the buttons again.</summary>
    [Fact]
    public async Task ADecisionThatGetsNoAnswerTimesOut()
    {
        var bridge = new PanelBridge { Answer = static async (_, token) => { await Task.Delay(Timeout.InfiniteTimeSpan, token).ConfigureAwait(true); return ToolApprovalOutcome.Accepted; } };
        var (harness, session, approval) = await OpenAsync(bridge).ConfigureAwait(true);
        using var owned = harness;
        var cut = RenderList(session, harness.Clock);

        var click = cut.Find("button[aria-label='Allow once: run_shell']").ClickAsync();
        await cut.WaitForAssertionAsync(() =>
        {
            cut.Find(".ompos-approval-message").TextContent.ShouldBe("Sending the decision…");
            cut.FindAll(".ompos-approval-actions button").ShouldAllBe(static button => button.HasAttribute("disabled"));
        }).ConfigureAwait(true);

        harness.Clock.Advance(TimeSpan.FromSeconds(16));
        await click.ConfigureAwait(true);

        approval.Status.ShouldBe(ToolApprovalStatus.Pending);
        await cut.WaitForAssertionAsync(() => cut.Find(".ompos-approval-message[role='alert']").TextContent.ShouldBe(ToolApprovalRow.TimedOutMessage)).ConfigureAwait(true);
        cut.FindAll(".ompos-approval-actions button").ShouldAllBe(static button => !button.HasAttribute("disabled"));
    }

    /// <summary>A session that cannot decide approvals shows a read-only notice and no buttons.</summary>
    [Fact]
    public async Task ASessionWithoutACoordinatorShowsAReadOnlyNotice()
    {
        var (harness, session, _) = await OpenAsync(null).ConfigureAwait(true);
        using var owned = harness;

        var cut = RenderList(session);

        cut.Find(".ompos-approval-message").TextContent.ShouldBe("Approval needed. The decision is made by policy.");
        cut.FindAll("button").ShouldBeEmpty();
    }

    /// <summary>With no pending approval nothing renders; a request added later appears.</summary>
    [Fact]
    public async Task NothingRendersUntilARequestIsPending()
    {
        var (harness, session, approval) = await OpenAsync(new PanelBridge()).ConfigureAwait(true);
        using var owned = harness;
        session.PendingApprovals.Clear();

        var cut = RenderList(session);
        cut.FindAll("section").ShouldBeEmpty();

        session.PendingApprovals.Add(approval);

        await cut.WaitForAssertionAsync(() => cut.FindAll(".ompos-approval").Count.ShouldBe(1)).ConfigureAwait(true);
        cut.FindAll("script").ShouldBeEmpty();
        cut.FindAll("[style]").ShouldBeEmpty();
    }
}
