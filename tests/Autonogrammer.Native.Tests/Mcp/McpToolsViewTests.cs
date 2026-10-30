using Autonogrammer.Harness.Mcp;
using Autonogrammer.Native.Tests.Workspace;
using Autonogrammer.Native.Views.Mcp;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Mcp;

/// <summary>
/// The MCP tools panel: tools grouped by server with one enable toggle each, and the offered list for the view's session.
/// The tools come from <see cref="FakeMcpToolRegistry"/>, built from the production descriptors; no server is connected.
/// The panel toggles tools for one fixed session, <see cref="McpToolsModel.DefaultSessionId"/>.
/// </summary>
public sealed class McpToolsViewTests
{
    /// <summary>Tools are grouped under their server, one toggle per tool, in server then tool name order.</summary>
    [Fact]
    public async Task ToolsAreGroupedByServerWithOneToggleEach()
    {
        var registry = SampleRegistry();

        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var (view, _) = Open(registry);

            ToggleNames(view).ShouldBe(["docs/index", "docs/search", "files/read"]);
            ShownText(view.ServerGroups).Where(text => text is "docs" or "files").ShouldBe(["docs", "files"]);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Switching a tool off removes it from the offered list, and switching it back on offers it again.</summary>
    [Fact]
    public async Task DisablingAToolRemovesItFromTheOfferedList()
    {
        var registry = SampleRegistry();

        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var (view, window) = Open(registry);
            ShownText(view.OfferedRows).ShouldBe(["docs/index", "docs/search", "files/read"]);

            var search = Toggle(view, "docs/search");
            search.IsChecked = false;
            ShellHarness.Settle(window);

            search.IsChecked.ShouldBe(false);
            ShownText(view.OfferedRows).ShouldBe(["docs/index", "files/read"]);

            search.IsChecked = true;
            ShellHarness.Settle(window);

            ShownText(view.OfferedRows).ShouldBe(["docs/index", "docs/search", "files/read"]);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A toggle changes the view's session only: another session keeps the tool enabled.</summary>
    [Fact]
    public async Task ATogglePersistsOnlyForTheViewsSession()
    {
        var registry = SampleRegistry();
        var search = new McpToolId("docs", "search");

        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var (view, window) = Open(registry);

            Toggle(view, "docs/search").IsChecked = false;
            ShellHarness.Settle(window);

            registry.IsEnabled(McpToolsModel.DefaultSessionId, search).ShouldBeFalse();
            registry.IsEnabled("another-session", search).ShouldBeTrue();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The keyboard reaches every toggle, Tab moves from one to the next, and Enter switches the focused tool.</summary>
    [Fact]
    public async Task EveryToggleIsNamedReachableAndSwitchesWithTheKeyboard()
    {
        var registry = SampleRegistry();

        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var (view, window) = Open(registry);

            ViewHost.UnnamedTabStops(view).ShouldBeEmpty();

            Toggle(view, "docs/index").Focus();
            ViewHost.Press(window, Key.Tab, PhysicalKey.Tab);
            Toggle(view, "docs/search").IsFocused.ShouldBeTrue("Tab moves from one toggle to the next");

            ViewHost.Press(window, Key.Enter, PhysicalKey.Enter);
            Toggle(view, "docs/search").IsChecked.ShouldBe(false);
            ShownText(view.OfferedRows).ShouldNotShowText("docs/search");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Space switches each focused toggle like Enter does: the toggle, the offered list and its accessible state agree.</summary>
    [Fact]
    public async Task EveryToggleSwitchesWithSpaceLikeEnter()
    {
        var registry = SampleRegistry();

        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var (view, window) = Open(registry);

            foreach (var name in ToggleNames(view))
            {
                var toggle = Toggle(view, name);
                toggle.Focus();
                ShellHarness.Settle(window);
                toggle.IsFocused.ShouldBeTrue(name + " takes focus");
                var wasOn = toggle.IsChecked;

                ViewHost.PressAndRelease(window, Key.Space, PhysicalKey.Space);

                toggle.IsChecked.ShouldBe(!wasOn, name + " changes state on Space");
                AccessibleState(toggle).ShouldBe(!wasOn, name + " accessible state follows Space");
                ShownText(view.OfferedRows).Contains(name).ShouldBe(toggle.IsChecked == true, name + " offered list follows Space");

                ViewHost.PressAndRelease(window, Key.Enter, PhysicalKey.Enter);

                toggle.IsChecked.ShouldBe(wasOn, name + " changes back on Enter");
                AccessibleState(toggle).ShouldBe(wasOn, name + " accessible state follows Enter");
            }
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The accessible toggle state of a switch: true when on, false when off.</summary>
    private static bool? AccessibleState(ToggleSwitch toggle) =>
        ((IToggleProvider)ControlAutomationPeer.CreatePeerForElement(toggle)).ToggleState switch
        {
            ToggleState.On => true,
            ToggleState.Off => false,
            _ => null,
        };

    private static FakeMcpToolRegistry SampleRegistry() =>
        new(
        [
            FakeMcpToolRegistry.Describe("files", "read", "Read a file from the workspace."),
            FakeMcpToolRegistry.Describe("docs", "search", "Search the documentation."),
            FakeMcpToolRegistry.Describe("docs", "index"),
        ]);

    private static (McpToolsView View, Window Window) Open(IMcpToolSource source)
    {
        var view = new McpToolsView { DataContext = new McpToolsModel(source) };
        var window = ViewHost.Show(view);
        return (view, window);
    }

    private static ToggleSwitch Toggle(McpToolsView view, string name) =>
        view.GetVisualDescendants()
            .OfType<ToggleSwitch>()
            .Single(toggle => string.Equals(AutomationProperties.GetName(toggle), name, StringComparison.Ordinal));

    private static List<string> ToggleNames(McpToolsView view) =>
        view.GetVisualDescendants()
            .OfType<ToggleSwitch>()
            .Select(toggle => AutomationProperties.GetName(toggle) ?? string.Empty)
            .ToList();

    private static List<string> ShownText(Control root) =>
        root.GetVisualDescendants()
            .OfType<TextBlock>()
            .Select(text => text.Text ?? string.Empty)
            .ToList();
}
