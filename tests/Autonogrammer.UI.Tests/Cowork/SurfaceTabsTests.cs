using Autonogrammer.Presentation.Cowork;
using Autonogrammer.UI.Components.Shell;
using Bunit;
using Shouldly;
using Xunit;

namespace Autonogrammer.UI.Tests.Cowork;

/// <summary>The workspace switch is a tablist with a roving tab stop and a polite chip.</summary>
public sealed class SurfaceTabsTests : UiTestContext
{
    /// <summary>The two tabs name Code and Cowork and point at the view panel.</summary>
    [Fact]
    public void TheTabsAreAWorkspaceTablist()
    {
        var cut = Render<SurfaceTabs>();

        var list = cut.Find("[role=tablist]");
        list.GetAttribute("aria-label").ShouldBe("Workspace");
        var tabs = cut.FindAll("[role=tab]");
        tabs.Count.ShouldBe(2);
        tabs[0].TextContent.ShouldBe("Code");
        tabs[0].Id.ShouldBe(SurfaceTabs.CodeTabId);
        tabs[0].GetAttribute("aria-controls").ShouldBe(ViewTabs.PanelId);
        tabs[0].GetAttribute("aria-keyshortcuts").ShouldBe("Control+Shift+1");
        tabs[0].GetAttribute("aria-selected").ShouldBe("true");
        tabs[0].GetAttribute("tabindex").ShouldBe("0");
        tabs[1].TextContent.ShouldBe("Cowork");
        tabs[1].Id.ShouldBe(SurfaceTabs.CoworkTabId);
        tabs[1].GetAttribute("aria-keyshortcuts").ShouldBe("Control+Shift+2");
        tabs[1].GetAttribute("tabindex").ShouldBe("-1");
    }

    /// <summary>Arrows, Home and End move the selection the way the view tabs do.</summary>
    [Fact]
    public async Task ArrowsMoveTheSelection()
    {
        var selected = SurfaceTabs.CodeView;
        var cut = Render<SurfaceTabs>(p => p
            .Add(c => c.Selected, selected)
            .Add(c => c.SelectedChanged, (string value) => selected = value));

        await cut.Find("[role=tablist]").KeyDownAsync(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "ArrowRight" }).ConfigureAwait(true);
        selected.ShouldBe(SurfaceTabs.CoworkView);
        cut.Render(p => p.Add(c => c.Selected, selected));

        await cut.Find("[role=tablist]").KeyDownAsync(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Home" }).ConfigureAwait(true);
        selected.ShouldBe(SurfaceTabs.CodeView);
        cut.Render(p => p.Add(c => c.Selected, selected));

        await cut.Find("[role=tablist]").KeyDownAsync(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "End" }).ConfigureAwait(true);
        selected.ShouldBe(SurfaceTabs.CoworkView);
    }
    /// <summary>The hidden-surface chip is polite status with the exact sentence.</summary>
    /// <param name="text">The chip sentence.</param>
    [Theory]
    [InlineData("Cowork, 2 running")]
    [InlineData("Code, needs approval")]
    public void TheChipIsPoliteStatus(string text)
    {
        text.ShouldBe(string.Equals(text, "Cowork, 2 running", StringComparison.Ordinal) ? CoworkCopy.RunningChip("Cowork", 2) : CoworkCopy.NeedsApprovalChip("Code"));
        var cut = Render<SurfaceTabs>(p => p.Add(c => c.ChipText, text));

        var chip = cut.Find("[role=status]");
        chip.TextContent.ShouldBe(text);
    }
}
