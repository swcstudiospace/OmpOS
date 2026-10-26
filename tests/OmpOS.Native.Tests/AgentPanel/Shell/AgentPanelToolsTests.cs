using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.AgentPanel.Shell;

/// <summary>Every existing tool stays reachable from the Tools view of the rail (Where Every Existing Tool Lives).</summary>
public sealed class AgentPanelToolsTests
{
    /// <summary>The twelve toggles with their <c>x:Name</c> and accessible name, as the UI spec lists them.</summary>
    public static TheoryData<string, string> ToggleNames => new()
    {
        { "AgentsToggle", "Agents" },
        { "SideChatsToggle", "Show side chats" },
        { "SuggestionsToggle", "Inline suggestions" },
        { "EditorToggle", "Editor" },
        { "ExplorerToggle", "Explorer" },
        { "TerminalToggle", "Terminal" },
        { "DiffToggle", "Diff review" },
        { "InlineEditToggle", "Inline edit" },
        { "ConventionsToggle", "Workspace conventions" },
        { "WorkspaceSearchToggle", "Workspace search" },
        { "McpToggle", "MCP tools" },
        { "ExportToggle", "Transcript export" },
    };

    /// <summary>Each toggle keeps its name and sits in the Tools view: hidden, and no tab stop, while the Sessions view shows.</summary>
    [Theory]
    [MemberData(nameof(ToggleNames))]
    public async Task EveryToolToggleKeepsItsNamesAndLivesInTheToolsView(string name, string accessibleName)
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.Dispatch(() =>
        {
            var window = ShellHarness.Show();
            var toggle = ShellHarness.MainViewOf(window).FindControl<ToggleButton>(name)!;

            AutomationProperties.GetName(toggle).ShouldBe(accessibleName);
            ShellTree.IsShown(toggle).ShouldBeFalse("the Sessions view shows the session list in place of the tools");

            ShellHarness.ShowToolsView(window);

            ShellTree.IsShown(toggle).ShouldBeTrue("the Tools view lists every toggle");
            toggle.MinHeight.ShouldBeGreaterThanOrEqualTo(44);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The rail's two view toggles are exclusive: exactly one is checked, and the checked one cannot be unchecked.</summary>
    [Fact]
    public async Task TheRailViewTogglesAreExclusive()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.Dispatch(() =>
        {
            var window = ShellHarness.Show();
            var main = ShellHarness.MainViewOf(window);
            var sessions = main.FindControl<ToggleButton>("SessionsViewToggle")!;
            var tools = main.FindControl<ToggleButton>("ToolsViewToggle")!;

            AutomationProperties.GetName(sessions).ShouldBe("Sessions view");
            AutomationProperties.GetName(tools).ShouldBe("Tools view");
            sessions.IsChecked.ShouldBe(true);
            tools.IsChecked.ShouldNotBe(true);

            tools.IsChecked = true;
            ShellHarness.Settle(window);
            sessions.IsChecked.ShouldBe(false);
            main.FindControl<ScrollViewer>("ToolsViewScroll")!.IsVisible.ShouldBeTrue();
            main.FindControl<ContentControl>("RailHost")!.IsVisible.ShouldBeFalse();

            tools.IsChecked = false;
            ShellHarness.Settle(window);
            tools.IsChecked.ShouldBe(true, "the checked view cannot be unchecked");
            sessions.IsChecked.ShouldBe(false);

            sessions.IsChecked = true;
            ShellHarness.Settle(window);
            tools.IsChecked.ShouldBe(false);
            main.FindControl<ContentControl>("RailHost")!.IsVisible.ShouldBeTrue();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Opening a tool replaces only the transcript, draws a tool header, and Close tool returns focus to the toggle that opened it.</summary>
    [Fact]
    public async Task CloseToolReturnsFocusToTheOpeningToggle()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.Dispatch(() =>
        {
            var window = ShellHarness.Show();
            var main = ShellHarness.MainViewOf(window);
            ShellHarness.ShowToolsView(window);
            var toggle = main.FindControl<ToggleButton>("TerminalToggle")!;
            var header = main.FindControl<Border>("ToolHeader")!;
            header.IsVisible.ShouldBeFalse();

            toggle.IsChecked = true;
            ShellHarness.Settle(window);

            main.FindControl<ContentControl>("ToolHost")!.IsVisible.ShouldBeTrue();
            main.FindControl<ContentControl>("TranscriptHost")!.IsVisible.ShouldBeFalse();
            header.IsVisible.ShouldBeTrue("an open tool draws its header");
            main.FindControl<TextBlock>("ToolTitle")!.Text.ShouldBe("Terminal");
            ShellHarness.Composer(window).IsEffectivelyVisible.ShouldBeTrue("the composer and the rail stay while a tool is open");
            ShellTree.NamedVisible<ListBox>(main, "AgentRows").ShouldBeNull("the session list is replaced by the Tools view, not the thread");

            var close = main.FindControl<Button>("CloseToolButton")!;
            AutomationProperties.GetName(close).ShouldBe("Close tool");
            close.MinHeight.ShouldBeGreaterThanOrEqualTo(44);

            ShellHarness.Click(window, close);

            main.FindControl<ContentControl>("ToolHost")!.IsVisible.ShouldBeFalse();
            main.FindControl<ContentControl>("TranscriptHost")!.IsVisible.ShouldBeTrue();
            header.IsVisible.ShouldBeFalse();
            toggle.IsChecked.ShouldBe(false);
            toggle.IsFocused.ShouldBeTrue("focus returns to the toggle that opened the tool");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Close tool also works after the user switched back to the Sessions view: the rail shows the Tools view again and focuses the toggle.</summary>
    [Fact]
    public async Task CloseToolFindsTheToggleAfterTheRailChangedView()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.Dispatch(() =>
        {
            var window = ShellHarness.Show();
            var main = ShellHarness.MainViewOf(window);
            ShellHarness.ShowToolsView(window);
            var toggle = main.FindControl<ToggleButton>("ExplorerToggle")!;
            toggle.IsChecked = true;
            ShellHarness.ShowSessionsView(window);

            ShellHarness.Click(window, main.FindControl<Button>("CloseToolButton")!);

            toggle.IsEffectivelyVisible.ShouldBeTrue();
            toggle.IsFocused.ShouldBeTrue();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The Agents window opens in the thread region with a header, like a tool, and the Agents toggle has focus again when it closes.</summary>
    [Fact]
    public async Task TheAgentsViewOpensInTheThreadRegionAndCloses()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.Dispatch(() =>
        {
            var window = ShellHarness.Show();
            var main = ShellHarness.MainViewOf(window);
            ShellHarness.ShowToolsView(window);
            var toggle = main.FindControl<ToggleButton>("AgentsToggle")!;

            toggle.IsChecked = true;
            ShellHarness.Settle(window);

            main.FindControl<ContentControl>("AgentsHost")!.IsVisible.ShouldBeTrue();
            main.FindControl<ContentControl>("TranscriptHost")!.IsVisible.ShouldBeFalse();
            main.FindControl<Border>("ToolHeader")!.IsVisible.ShouldBeTrue();

            ShellHarness.Click(window, main.FindControl<Button>("CloseToolButton")!);

            main.FindControl<ContentControl>("AgentsHost")!.IsVisible.ShouldBeFalse();
            main.FindControl<ContentControl>("TranscriptHost")!.IsVisible.ShouldBeTrue();
            toggle.IsChecked.ShouldBe(false);
            toggle.IsFocused.ShouldBeTrue();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Opening one tool closes the one that was open, and selecting an agent closes the open tool and shows the thread.</summary>
    [Fact]
    public async Task OpeningAToolClosesTheOtherAndSelectingAnAgentClosesIt()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.Dispatch(() =>
        {
            var fixture = ShellFixture.CreateWithSession();
            fixture.AddSession("session-2", "Agent two");
            var window = ShellHarness.Show(fixture);
            var main = ShellHarness.MainViewOf(window);
            ShellHarness.ShowToolsView(window);
            var editor = main.FindControl<ToggleButton>("EditorToggle")!;
            var terminal = main.FindControl<ToggleButton>("TerminalToggle")!;

            editor.IsChecked = true;
            terminal.IsChecked = true;
            ShellHarness.Settle(window);

            editor.IsChecked.ShouldBe(false, "opening a tool closes the others");
            terminal.IsChecked.ShouldBe(true);

            fixture.Panel.SelectSession("session-2");
            ShellHarness.Settle(window);

            terminal.IsChecked.ShouldBe(false, "selecting an agent closes the open tool");
            main.FindControl<ContentControl>("ToolHost")!.IsVisible.ShouldBeFalse();
            main.FindControl<ContentControl>("TranscriptHost")!.IsVisible.ShouldBeTrue();
            ShellHarness.Transcript(window).DataContext.ShouldBeSameAs(fixture.Runtime("session-2").Session);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Each toggle opens its host: the editor, the explorer and the export open in place of the transcript; side chats and suggestions open beside the composer.</summary>
    [Theory]
    [InlineData("EditorToggle")]
    [InlineData("ExplorerToggle")]
    [InlineData("DiffToggle")]
    [InlineData("InlineEditToggle")]
    [InlineData("ConventionsToggle")]
    [InlineData("WorkspaceSearchToggle")]
    [InlineData("McpToggle")]
    [InlineData("ExportToggle")]
    public async Task ToolTogglesOpenTheirHost(string toggleName)
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.Dispatch(() =>
        {
            var window = ShellHarness.Show(ShellFixture.CreateWithSession(), workspaceRoot: ShellHarness.RepoRoot());
            var main = ShellHarness.MainViewOf(window);
            ShellHarness.ShowToolsView(window);

            main.FindControl<ToggleButton>(toggleName)!.IsChecked = true;
            ShellHarness.Settle(window);

            var host = main.FindControl<ContentControl>("ToolHost")!;
            host.IsVisible.ShouldBeTrue($"{toggleName} opens the tool host");
            host.Content.ShouldNotBeNull();
            main.FindControl<ContentControl>("TranscriptHost")!.IsVisible.ShouldBeFalse();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The side chats open under the composer and the suggestions box opens under the prompt, each from its toggle.</summary>
    [Fact]
    public async Task SideChatsAndSuggestionsOpenAroundTheComposer()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.Dispatch(() =>
        {
            var window = ShellHarness.Show();
            var main = ShellHarness.MainViewOf(window);
            ShellHarness.ShowToolsView(window);

            main.FindControl<ToggleButton>("SideChatsToggle")!.IsChecked = true;
            main.FindControl<ToggleButton>("SuggestionsToggle")!.IsChecked = true;
            ShellHarness.Settle(window);

            main.FindControl<ContentControl>("SideChatHost")!.IsVisible.ShouldBeTrue();
            ShellHarness.Composer(window).SuggestionsShown.ShouldBeTrue();
            main.FindControl<ContentControl>("TranscriptHost")!.IsVisible.ShouldBeTrue("these two never replace the transcript");
            main.FindControl<ContentControl>("ToolHost")!.IsVisible.ShouldBeFalse();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The skip link lands on the transcript of the selected agent, and on the open tool while one replaces the transcript.</summary>
    [Fact]
    public async Task TheSkipLinkLandsOnTheTranscriptOrTheOpenTool()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.Dispatch(() =>
        {
            var window = ShellHarness.Show();
            var main = ShellHarness.MainViewOf(window);

            ShellHarness.Click(window, ShellHarness.SkipLink(window));
            ShellHarness.Scroll(window).IsFocused.ShouldBeTrue("the skip link focuses the transcript of the selected agent");

            ShellHarness.ShowToolsView(window);
            main.FindControl<ToggleButton>("DiffToggle")!.IsChecked = true;
            ShellHarness.Settle(window);
            ShellHarness.Click(window, ShellHarness.SkipLink(window));

            ShellHarness.Scroll(window).IsFocused.ShouldBeFalse("the transcript is not shown while the tool stands in for it");
            var focused = window.FocusManager!.GetFocusedElement() as Visual;
            focused.ShouldNotBeNull();
            main.FindControl<ContentControl>("ToolHost")!.IsVisualAncestorOf(focused).ShouldBeTrue("the skip link lands on the open tool's first control");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }
}
