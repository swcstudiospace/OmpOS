using Autonogrammer.Contracts.Cowork;
using Autonogrammer.Presentation.Cowork;
using Autonogrammer.Presentation.Dispatch;
using Autonogrammer.UI.Components.Cowork;
using Autonogrammer.UI.Components.Cowork.Artifacts;
using Autonogrammer.UI.Components.Dispatch;
using Autonogrammer.UI.Components.Shell;
using Bunit;
using Shouldly;
using Xunit;

namespace Autonogrammer.UI.Tests;

/// <summary>Structural accessibility checks for the Cowork and Dispatch web surfaces.</summary>
public sealed class CoworkSurfaceAccessibilityTests : UiTestContext
{
    [Fact]
    public void SurfaceSwitchNamesItsTabsAndSelectedTabStop()
    {
        var cut = Render<SurfaceTabs>();
        cut.Find("[role=tablist]").GetAttribute("aria-label").ShouldBe("Workspace");
        var tabs = cut.FindAll("[role=tab]");
        tabs.Select(static tab => tab.TextContent.Trim()).ShouldBe(["Code", "Cowork"]);
        tabs.Select(static tab => tab.GetAttribute("tabindex")).ShouldBe(["0", "-1"]);
    }

    [Fact]
    public async Task F6CyclesFromTheFocusedRegionInBothDirections()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = Render<CoworkWorkbench>();
        var rail = cut.Find("[aria-label=Rail]");
        var conversation = cut.Find("[aria-label=Conversation]");
        var dock = cut.Find("[role=region][aria-label=Dock]");

        await rail.FocusInAsync().ConfigureAwait(true);
        await rail.KeyDownAsync(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "F6" }).ConfigureAwait(true);
        JSInterop.Invocations["Blazor._internal.domWrapper.focus"][^1].Arguments[0].ShouldBeElementReferenceTo(conversation);
        await conversation.FocusInAsync().ConfigureAwait(true);
        await conversation.KeyDownAsync(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "F6" }).ConfigureAwait(true);
        JSInterop.Invocations["Blazor._internal.domWrapper.focus"][^1].Arguments[0].ShouldBeElementReferenceTo(dock);
        await dock.FocusInAsync().ConfigureAwait(true);
        await dock.KeyDownAsync(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "F6", ShiftKey = true }).ConfigureAwait(true);
        JSInterop.Invocations["Blazor._internal.domWrapper.focus"][^1].Arguments[0].ShouldBeElementReferenceTo(conversation);
    }

    [Fact]
    public void DockNamesItsTabsAndUpdatesSelection()
    {
        using var shell = new CoworkShellModel(new EmptyCatalog());
        shell.RegisterPanel(new Panel("files", "Files"));
        shell.RegisterPanel(new Panel("browser", "Browser"));
        var cut = Render<CoworkWorkbench>(parameters => parameters.Add(component => component.Shell, shell));
        var tabs = cut.FindAll("[role=tablist][aria-label=Dock] [role=tab]");
        tabs.Count.ShouldBe(2);
        tabs[0].GetAttribute("aria-selected").ShouldBe("true");
        tabs[1].Click();
        cut.FindAll("[role=tablist][aria-label=Dock] [role=tab]")[1].GetAttribute("aria-selected").ShouldBe("true");
    }

    [Fact]
    public async Task DockArrowKeysMoveSelectionAndTheRovingFocus()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        using var shell = new CoworkShellModel(new EmptyCatalog());
        shell.RegisterPanel(new Panel("files", "Files"));
        shell.RegisterPanel(new Panel("browser", "Browser"));
        shell.RegisterPanel(new Panel("notes", "Notes"));
        var cut = Render<CoworkWorkbench>(parameters => parameters.Add(component => component.Shell, shell));
        var list = cut.Find("[role=tablist][aria-label=Dock]");
        var tabs = cut.FindAll("[role=tablist][aria-label=Dock] [role=tab]");
        tabs.Select(static tab => tab.GetAttribute("tabindex")).ShouldBe(["0", "-1", "-1"]);

        await list.KeyDownAsync(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "ArrowLeft" }).ConfigureAwait(true);
        shell.SelectedPanelId.ShouldBe("notes");
        JSInterop.Invocations["Blazor._internal.domWrapper.focus"][^1].Arguments[0].ShouldBeElementReferenceTo(tabs[2]);
        cut.FindAll("[role=tablist][aria-label=Dock] [role=tab]").Select(static tab => tab.GetAttribute("tabindex")).ShouldBe(["-1", "-1", "0"]);

        await list.KeyDownAsync(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Home" }).ConfigureAwait(true);
        shell.SelectedPanelId.ShouldBe("files");
        await list.KeyDownAsync(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "ArrowRight" }).ConfigureAwait(true);
        shell.SelectedPanelId.ShouldBe("browser");
        JSInterop.Invocations["Blazor._internal.domWrapper.focus"][^1].Arguments[0].ShouldBeElementReferenceTo(tabs[1]);
        await list.KeyDownAsync(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "End" }).ConfigureAwait(true);
        shell.SelectedPanelId.ShouldBe("notes");
    }

    [Fact]
    public void ApprovalNamesItsActionsAndIsNotAModalDialog()
    {
        var model = DispatchApprovalModel.From("approval-1", "Office Mac", "run", "computer.use", ["shell"], "Fix the build");
        var cut = Render<DispatchApprovalPanel>(parameters => parameters.Add(component => component.Model, model));
        var headingId = cut.Find("section").GetAttribute("aria-labelledby");
        headingId.ShouldNotBeNullOrWhiteSpace();
        cut.Find("h3").Id.ShouldBe(headingId);
        cut.FindAll("button").Select(static button => button.TextContent.Trim()).ShouldBe([DispatchCopy.ApproveOnceLabel, DispatchCopy.DenyLabel, DispatchCopy.DenyAndPauseLabel]);
        cut.FindAll("[aria-modal=true]").ShouldBeEmpty();
    }

    [Fact]
    public void MultipleApprovalPanelsHaveDistinctHeadingReferences()
    {
        var model = DispatchApprovalModel.From("approval-1", "Office Mac", "run", "computer.use", ["shell"], "Fix the build");
        var first = Render<DispatchApprovalPanel>(parameters => parameters.Add(component => component.Model, model));
        var second = Render<DispatchApprovalPanel>(parameters => parameters.Add(component => component.Model, model));

        var firstId = first.Find("section").GetAttribute("aria-labelledby");
        var secondId = second.Find("section").GetAttribute("aria-labelledby");
        firstId.ShouldNotBeNullOrWhiteSpace();
        secondId.ShouldNotBeNullOrWhiteSpace();
        string.Equals(firstId, secondId, StringComparison.Ordinal).ShouldBeFalse();
        first.Find("h3").Id.ShouldBe(firstId);
        second.Find("h3").Id.ShouldBe(secondId);
    }

    [Fact]
    public void ArtifactFrameHasAnAccessibleTitle()
    {
        var cut = Render<ArtifactFrame>(parameters => parameters.Add(component => component.Title, "Generated report").Add(component => component.SrcDoc, "<p>Report</p>"));
        cut.Find("iframe").GetAttribute("title").ShouldBe("Generated report");
    }

    [Fact]
    public void DispatchFailureIsAnnouncedAssertively()
    {
        var model = new DispatchPageModel(DevicesViewModel.Loaded([]), [], PairThisDevice: null, Problem: "Approval failed.");
        var cut = Render<DispatchPageView>(parameters => parameters.Add(component => component.Model, model));
        cut.Find("[aria-live=assertive]").TextContent.ShouldContain("Approval failed.");
    }

    private sealed class EmptyCatalog : ICoworkSessionCatalog
    {
        public event EventHandler? Changed { add { } remove { } }
        public IReadOnlyList<CoworkSessionSummary> Snapshot() => [];
    }

    private sealed class Panel(string id, string title) : IDockPanel
    {
        public string Id { get; } = id;
        public string Title { get; } = title;
        public object Content { get; } = new();
    }
}
