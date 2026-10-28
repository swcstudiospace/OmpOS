using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Autonogrammer.Native.Tests.Workspace;
using Autonogrammer.Native.Views.Mcp;
using Autonogrammer.Presentation.Mcp;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Mcp;

/// <summary>
/// The MCP settings page in a headless window: every state it draws, the consent flow, the add-from-template flow, and the accessibility contract
/// (every button named, no secret on screen). The data comes from <see cref="ScriptedMcpPageSource"/>; no server is started.
/// </summary>
public sealed class McpPageViewTests
{
    private static McpServerEntry NeedsConsent() =>
        new("tool", "stdio", McpEntryScope.Project, ".cursor/mcp.json", McpServerStatusKind.NeedsConsent, null, null, "node ./tool.js --serve", ["TOOL_TOKEN"], [], true, false, true, false, false);

    private static McpServerEntry Ready() =>
        new("files", "http", McpEntryScope.User, "~/.cursor/mcp.json", McpServerStatusKind.Configured, null, null, "https://files.example.com", [], ["Authorization"], true, true, false, false, false);

    private static (McpPageView View, McpSettingsViewModel Model, Window Window) Open(ScriptedMcpPageSource source)
    {
        var model = new McpSettingsViewModel(source);
        var view = new McpPageView { DataContext = model };
        var window = ViewHost.Show(view);
        return (view, model, window);
    }

    private static List<string> ShownText(Control root) =>
        root.GetVisualDescendants()
            .OfType<TextBlock>()
            .Where(text => text.IsEffectivelyVisible)
            .Select(text => text.Text ?? string.Empty)
            .ToList();

    private static List<Button> VisibleButtons(Control root) =>
        root.GetVisualDescendants().OfType<Button>().Where(button => button.IsEffectivelyVisible).ToList();

    private static Button Button(Control root, string name) =>
        VisibleButtons(root).Single(button => string.Equals(AutomationProperties.GetName(button), name, StringComparison.Ordinal));

    [Fact]
    public async Task ServersAreDrawnByFileWithStatusCommandAndNamesOnly()
    {
        var source = new ScriptedMcpPageSource(NeedsConsent(), Ready());

        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var (view, model, _) = Open(source);

            model.State.ShouldBe(McpViewState.Degraded);
            var text = ShownText(view);
            text.ShouldContain("Project: .cursor/mcp.json", StringComparer.Ordinal);
            text.ShouldContain("User: ~/.cursor/mcp.json", StringComparer.Ordinal);
            text.ShouldContain("tool", StringComparer.Ordinal);
            text.ShouldContain("Needs approval", StringComparer.Ordinal);
            text.ShouldContain("node ./tool.js --serve", StringComparer.Ordinal);
            text.ShouldContain("Environment: TOOL_TOKEN", StringComparer.Ordinal);
            text.ShouldContain("Headers: Authorization", StringComparer.Ordinal);
            text.ShouldContain(McpCopy.DegradedHeading, StringComparer.Ordinal);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    [Fact]
    public async Task EveryVisibleButtonIsNamedAndBigEnoughAndEveryCommandIsBound()
    {
        var source = new ScriptedMcpPageSource(NeedsConsent(), Ready());

        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var (view, _, _) = Open(source);

            var buttons = VisibleButtons(view);
            buttons.Count.ShouldBeGreaterThanOrEqualTo(8);
            buttons.ShouldAllBe(button => !string.IsNullOrWhiteSpace(AutomationProperties.GetName(button)));
            buttons.ShouldAllBe(button => button.Command != null);
            buttons.ShouldAllBe(button => button.Bounds.Height >= 24 && button.Bounds.Width >= 24);
            buttons.Select(button => AutomationProperties.GetName(button)).Distinct(StringComparer.Ordinal).Count().ShouldBe(buttons.Count);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    [Fact]
    public async Task ReviewingAndApprovingAProjectServerGoesThroughThePromptAndWritesOnce()
    {
        var source = new ScriptedMcpPageSource(NeedsConsent());

        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var (view, model, window) = Open(source);
            ShownText(view).ShouldNotContain(McpCopy.ConsentHeading, StringComparer.Ordinal);

            Button(view, "Review approval for server tool").Command!.Execute(null);
            ShellHarness.Settle(window);

            ShownText(view).ShouldContain(McpCopy.ConsentHeading, StringComparer.Ordinal);
            ShownText(view).ShouldContain("tool (stdio server, .cursor/mcp.json)", StringComparer.Ordinal);
            source.Calls.ShouldBeEmpty();

            Button(view, "Approve server tool").Command!.Execute(null);
            ShellHarness.Settle(window);

            source.Calls.ShouldBe(["grant tool"]);
            ShownText(view).ShouldNotContain(McpCopy.ConsentHeading, StringComparer.Ordinal);
            model.State.ShouldBe(McpViewState.Success);
            ShownText(view).ShouldContain("Ready", StringComparer.Ordinal);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    [Fact]
    public async Task TheEmptyStateShowsTheExampleAndTheTemplateButtonsAddAServer()
    {
        var source = new ScriptedMcpPageSource();

        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var (view, model, window) = Open(source);
            ShownText(view).ShouldContain(McpCopy.EmptyHeading, StringComparer.Ordinal);
            view.GetVisualDescendants().OfType<SelectableTextBlock>().Select(block => block.Text).ShouldContain(McpCopy.ExampleJson, StringComparer.Ordinal);

            view.GetVisualDescendants().OfType<TextBox>().Single().Text = "starter";
            ShellHarness.Settle(window);
            Button(view, McpCopy.TemplateStdioLabel).Command!.Execute(null);
            ShellHarness.Settle(window);

            source.Calls.ShouldBe(["template Stdio starter"]);
            model.Rows.Single().Name.ShouldBe("starter");
            ShownText(view).ShouldContain("Added starter to .autonogrammer/mcp.json. It is switched off until you edit it and switch it on.", StringComparer.Ordinal);
            view.GetVisualDescendants().OfType<TextBox>().Single().Text.ShouldBeNullOrEmpty();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    [Fact]
    public async Task ALoadFailureShowsTheReasonAndNoList()
    {
        var source = new ScriptedMcpPageSource(Ready()) { LoadFailure = "The MCP state file is locked." };

        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var (view, _, _) = Open(source);

            var text = ShownText(view);
            text.ShouldContain(McpCopy.ErrorHeading, StringComparer.Ordinal);
            text.ShouldContain("The MCP state file is locked.", StringComparer.Ordinal);
            text.ShouldNotContain("User: ~/.cursor/mcp.json", StringComparer.Ordinal);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    [Fact]
    public async Task InvalidConfigurationListsTheProblemsWithTheirSeverity()
    {
        var source = new ScriptedMcpPageSource();
        source.Notices.Add(new McpNotice(McpNoticeSeverity.Error, ".cursor/mcp.json", null, "The file is not valid JSON (line 2, column 4)."));

        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var (view, model, _) = Open(source);

            model.State.ShouldBe(McpViewState.Invalid);
            var text = ShownText(view);
            text.ShouldContain(McpCopy.InvalidHeading, StringComparer.Ordinal);
            text.ShouldContain(McpCopy.ProblemsHeading, StringComparer.Ordinal);
            text.ShouldContain("Error. .cursor/mcp.json: The file is not valid JSON (line 2, column 4).", StringComparer.Ordinal);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }
}
