using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;
using OmpOS.Harness.Mcp.Catalog;
using OmpOS.Harness.Mcp.Config;
using OmpOS.Native.Tests.Workspace;
using OmpOS.Native.Views.Mcp;
using OmpOS.Presentation.Mcp;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.Mcp;

/// <summary>The MCP page in a headless window over the real catalog: the add-from-preset list shows the thirteen presets with their sign-in notes and vendor links, and its buttons write the owned file.</summary>
public sealed class McpPresetPageViewTests
{
    private static readonly string Root = Path.Combine("/work", "app");
    private static readonly string OwnedFile = Path.Combine(Root, ".ompos", "mcp.json");

    private static McpLocatorContext Context() => new([Root], Path.Combine("/home", "ada"), new Dictionary<string, string?>(StringComparer.Ordinal));

    private static (McpPageView View, McpSettingsViewModel Model, Window Window) Open(MemoryMcpFiles files)
    {
        var source = new McpCatalogSettingsSource(new McpServerCatalog(files, files), Context, null);
        var model = new McpSettingsViewModel(source);
        var view = new McpPageView { DataContext = model };
        var window = ViewHost.Show(view);
        return (view, model, window);
    }

    private static List<string> ShownText(Control root) =>
        root.GetVisualDescendants()
            .OfType<TextBlock>()
            .Where(static text => text.IsEffectivelyVisible)
            .Select(static text => text.Text ?? string.Empty)
            .ToList();

    private static List<Button> VisibleButtons(Control root) =>
        root.GetVisualDescendants().OfType<Button>().Where(static button => button is not RepeatButton && button.IsEffectivelyVisible).ToList();

    /// <summary>The list holds one named button per preset, and its text names the sign-in route and the vendor page; every button is named and big enough.</summary>
    [Fact]
    public async Task ThePageListsThirteenPresetsWithNotesAndVendorLinks()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var (view, _, window) = Open(new MemoryMcpFiles());
            ShellHarness.Settle(window);

            var text = ShownText(view);
            text.ShouldContain(McpCopy.PresetHeading, StringComparer.Ordinal);
            text.ShouldContain("Linear", StringComparer.Ordinal);
            text.ShouldContain(McpCopy.PresetRemote, StringComparer.Ordinal);
            text.ShouldContain(McpCopy.PresetLocal, StringComparer.Ordinal);
            text.ShouldContain(McpCopy.AuthOAuth, StringComparer.Ordinal);
            text.ShouldContain(McpCopy.AuthBearerToken("GITHUB_PERSONAL_ACCESS_TOKEN"), StringComparer.Ordinal);
            view.GetVisualDescendants().OfType<SelectableTextBlock>().Select(static block => block.Text).ShouldContain("Vendor docs: https://linear.app/docs/mcp", StringComparer.Ordinal);

            var presetButtons = VisibleButtons(view).Where(static button => AutomationProperties.GetName(button)?.StartsWith("Add ", StringComparison.Ordinal) == true).ToList();
            presetButtons.Count(static button => button.Command is McpActionCommand).ShouldBeGreaterThanOrEqualTo(13);
            presetButtons.Select(static button => AutomationProperties.GetName(button)).ShouldContain("Add Linear", StringComparer.Ordinal);
            VisibleButtons(view).ShouldAllBe(static button => !string.IsNullOrWhiteSpace(AutomationProperties.GetName(button)) && button.Bounds.Height >= 24 && button.Bounds.Width >= 24);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Pressing Add writes the owned file, says the server is off and how it signs in, and turns the button into a disabled Added.</summary>
    [Fact]
    public async Task PressingAddWritesTheFileAndLocksTheButton()
    {
        var files = new MemoryMcpFiles();

        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var (view, model, window) = Open(files);
            ShellHarness.Settle(window);

            var add = VisibleButtons(view).Single(static button => string.Equals(AutomationProperties.GetName(button), "Add Linear", StringComparison.Ordinal));
            add.Command!.Execute(null);
            ShellHarness.Settle(window);

            files.Text(OwnedFile).ShouldNotBeNull().ShouldContain("https://mcp.linear.app/mcp", Case.Sensitive);
            ShownText(view).ShouldContain("Added linear to .ompos/mcp.json. It is switched off until you switch it on. " + McpCopy.AuthOAuth, StringComparer.Ordinal);
            var added = VisibleButtons(view).Single(static button => string.Equals(AutomationProperties.GetName(button), "Added Linear", StringComparison.Ordinal));
            added.IsEnabled.ShouldBeFalse();
            model.Rows.Single().Name.ShouldBe("linear");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }
}
