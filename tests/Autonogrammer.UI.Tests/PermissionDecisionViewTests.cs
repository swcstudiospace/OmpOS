using Autonogrammer.Contracts.Ui;
using Autonogrammer.UI.Components.Blocks;
using Bunit;
using Shouldly;
using Xunit;

namespace Autonogrammer.UI.Tests;

/// <summary>The web head shows the decision recorded on a <see cref="PermissionBlock"/> as text.</summary>
public sealed class PermissionDecisionViewTests : BunitContext
{
    /// <summary>An approved request shows "Decision: Approved" and keeps its options and the policy note.</summary>
    [Fact]
    public void AnApprovedRequestShowsTheDecisionText()
    {
        var cut = RenderPermission(ApprovalDecisions.Approved);

        cut.Find(".autonogrammer-permission-decision").TextContent.ShouldBe("Decision: Approved");
        cut.Find(".autonogrammer-permission").GetAttribute("data-decision").ShouldBe("approved");
        cut.FindAll(".autonogrammer-permission-option").Select(static option => option.TextContent).ShouldBe(["Allow once", "Deny"]);
        cut.Find(".autonogrammer-permission-note").TextContent.ShouldBe("The decision is made by policy.");
    }

    /// <summary>A denied request shows "Decision: Denied".</summary>
    [Fact]
    public void ADeniedRequestShowsTheDecisionText()
    {
        var cut = RenderPermission(ApprovalDecisions.Denied);

        cut.Find(".autonogrammer-permission-decision").TextContent.ShouldBe("Decision: Denied");
    }

    /// <summary>A decision value this version does not know is shown as it came, never hidden or translated.</summary>
    [Fact]
    public void AnUnknownDecisionIsShownVerbatim()
    {
        var cut = RenderPermission("deferred");

        cut.Find(".autonogrammer-permission-decision").TextContent.ShouldBe("Decision: deferred");
    }

    /// <summary>An undecided request shows no decision line and no decision attribute.</summary>
    [Fact]
    public void AnUndecidedRequestShowsNoDecision()
    {
        var cut = RenderPermission(null);

        cut.FindAll(".autonogrammer-permission-decision").ShouldBeEmpty();
        cut.Find(".autonogrammer-permission").HasAttribute("data-decision").ShouldBeFalse();
    }

    private IRenderedComponent<UiBlockRenderer> RenderPermission(string? decision) =>
        Render<UiBlockRenderer>(parameters => parameters.Add(p => p.Block, new PermissionBlock
        {
            Id = "perm-1",
            Prompt = "Run shell command",
            Options = ["Allow once", "Deny"],
            Decision = decision,
        }));
}
