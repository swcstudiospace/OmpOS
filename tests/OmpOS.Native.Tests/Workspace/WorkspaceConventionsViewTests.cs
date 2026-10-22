using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using OmpOS.Native.Views.Workspace;
using OmpOS.Workspace;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.Workspace;

/// <summary>
/// The workspace conventions panel: the rules with their diagnostics, the skills, and the modes with the mode picker. Each test
/// writes a real workspace folder, loads it through <see cref="WorkspaceCatalog"/>, and checks what the panel draws.
/// </summary>
public sealed class WorkspaceConventionsViewTests
{
    /// <summary>A malformed rule file is listed as a diagnostic, and the valid rules still load beside it.</summary>
    [Fact]
    public async Task AMalformedRuleShowsItsDiagnostic()
    {
        using var workspace = new TempWorkspace();
        workspace.Write(".ompos/rules/style.md", "Use the house style.");
        workspace.Write(".ompos/rules/Bad Name.md", "The file name is not a valid rule id.");
        using var catalog = new WorkspaceCatalog(new WorkspaceRoot(workspace.Root));

        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            using var model = new WorkspaceConventionsModel(catalog);
            var view = new WorkspaceConventionsView { DataContext = model };
            ViewHost.Show(view);

            var diagnostic = catalog.Current.Diagnostics.ShouldHaveSingleItem();
            var diagnostics = ShownText(view.DiagnosticRows);
            diagnostics.ShouldShowText(diagnostic.RelativePath);
            diagnostics.ShouldShowText(diagnostic.Reason);

            var rules = ShownText(view.RuleRows);
            rules.ShouldShowText("style");
            rules.ShouldNotShowText(diagnostic.RelativePath);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The mode picker and the mode list show one choice, and the chosen mode's rules and skills are shown.</summary>
    [Fact]
    public async Task ThePickerAndTheModeListShowTheSameChosenMode()
    {
        using var workspace = new TempWorkspace();
        workspace.Write(".ompos/rules/style.md", "Use the house style.");
        workspace.Write(".ompos/skills/review.md", "Review the diff.");
        workspace.Write(".ompos/modes/reviewer.json", """{"id":"reviewer","displayName":"Reviewer","rules":["style"],"skills":["review"]}""");
        using var catalog = new WorkspaceCatalog(new WorkspaceRoot(workspace.Root));

        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            using var model = new WorkspaceConventionsModel(catalog);
            var view = new WorkspaceConventionsView { DataContext = model };
            ViewHost.Show(view);
            var reviewer = catalog.Current.Modes.ShouldHaveSingleItem();

            view.ModePicker.SelectedItem = reviewer;
            model.SelectedMode.ShouldBe(reviewer);
            view.ModeRows.SelectedItem.ShouldBe(reviewer);
            (view.SelectedModeDetail.Text ?? string.Empty).ShouldContain("style");
            (view.SelectedModeDetail.Text ?? string.Empty).ShouldContain("review");

            view.ModeRows.SelectedItem = null;
            model.SelectedMode.ShouldBeNull();
            view.ModePicker.SelectedItem.ShouldBeNull();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A mode that names a rule which does not exist is a diagnostic and is not offered in the mode list.</summary>
    [Fact]
    public async Task AModeNamingAnUnknownRuleIsADiagnosticAndNotOffered()
    {
        using var workspace = new TempWorkspace();
        workspace.Write(".ompos/rules/style.md", "Use the house style.");
        workspace.Write(".ompos/modes/broken.json", """{"id":"broken","displayName":"Broken","rules":["ghost"],"skills":[]}""");
        using var catalog = new WorkspaceCatalog(new WorkspaceRoot(workspace.Root));

        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            using var model = new WorkspaceConventionsModel(catalog);
            var view = new WorkspaceConventionsView { DataContext = model };
            ViewHost.Show(view);

            var diagnostic = catalog.Current.Diagnostics.ShouldHaveSingleItem();
            ShownText(view.DiagnosticRows).ShouldShowText(diagnostic.RelativePath);
            ShownText(view.ModeRows).ShouldBeEmpty();
            view.ModePicker.ItemCount.ShouldBe(0);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A rule added to the workspace appears in the rules list after the catalog reloads.</summary>
    [Fact]
    public async Task ARuleAddedAndReloadedAppearsInTheList()
    {
        using var workspace = new TempWorkspace();
        workspace.Write(".ompos/rules/style.md", "Use the house style.");
        using var catalog = new WorkspaceCatalog(new WorkspaceRoot(workspace.Root));

        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            using var model = new WorkspaceConventionsModel(catalog);
            var view = new WorkspaceConventionsView { DataContext = model };
            var window = ViewHost.Show(view);
            ShownText(view.RuleRows).ShouldNotShowText("review");

            workspace.Write(".ompos/rules/review.md", "Review every change.");
            catalog.Reload();
            ShellHarness.Settle(window);

            ShownText(view.RuleRows).ShouldShowText("review");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Every keyboard stop of the panel is named, and Tab moves from the mode picker to the mode list.</summary>
    [Fact]
    public async Task EveryKeyboardStopIsNamedAndTabReachesTheModeList()
    {
        using var workspace = new TempWorkspace();
        workspace.Write(".ompos/rules/style.md", "Use the house style.");
        workspace.Write(".ompos/modes/reviewer.json", """{"id":"reviewer","displayName":"Reviewer","rules":["style"],"skills":[]}""");
        using var catalog = new WorkspaceCatalog(new WorkspaceRoot(workspace.Root));

        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            using var model = new WorkspaceConventionsModel(catalog);
            var view = new WorkspaceConventionsView { DataContext = model };
            var window = ViewHost.Show(view);

            ViewHost.UnnamedTabStops(view).ShouldBeEmpty();

            view.ModePicker.Focus();
            ViewHost.Press(window, Key.Tab, PhysicalKey.Tab);
            var modeListHasFocus = view.ModeRows.IsFocused
                || view.ModeRows.GetVisualDescendants().OfType<ListBoxItem>().Any(item => item.IsFocused);
            modeListHasFocus.ShouldBeTrue("the mode list follows the mode picker");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    private static List<string> ShownText(Control root) =>
        root.GetVisualDescendants()
            .OfType<TextBlock>()
            .Select(text => text.Text ?? string.Empty)
            .ToList();
}
