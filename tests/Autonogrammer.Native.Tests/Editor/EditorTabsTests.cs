using Avalonia.Input;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Editor;

/// <summary>
/// The tab strip over the open files: open, select and close, with the editor showing the selected file and its edits
/// reaching the tab's buffer (ADE-09).
/// </summary>
public sealed class EditorTabsTests
{
    private const string ProgramPath = "src/Program.cs";
    private const string ProgramText = "class Program { }";
    private const string SettingsPath = "config/settings.json";
    private static readonly string[] RemainingNames = ["a.cs", "c.cs"];
    private const string SettingsText = "{ \"enabled\": true }";

    /// <summary>Two opened files are two tabs; the second one is selected and its text is the text the editor shows.</summary>
    [Fact]
    public async Task TwoFilesOpenInTwoTabsWithTheSecondSelected()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var model = EditorHarness.NewModel();
            model.Open(ProgramPath, ProgramText);
            model.Open(SettingsPath, SettingsText);
            using var window = EditorHarness.Show(model);

            model.Items.Count.ShouldBe(2);
            EditorHarness.TabToggle(window, "Program.cs").IsChecked.ShouldBe(false);
            EditorHarness.TabToggle(window, "settings.json").IsChecked.ShouldBe(true);
            EditorHarness.Code(window).Text.ShouldBe(SettingsText);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A tab opened after the view is shown appears in the strip and is shown in the editor.</summary>
    [Fact]
    public async Task TabOpenedWhileShownAppearsAndIsShown()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var model = EditorHarness.NewModel();
            using var window = EditorHarness.Show(model);

            model.Open(ProgramPath, ProgramText);
            ShellHarness.Settle(window);

            EditorHarness.TabStrip(window).ItemCount.ShouldBe(1);
            EditorHarness.TabToggle(window, "Program.cs").IsChecked.ShouldBe(true);
            EditorHarness.Code(window).Text.ShouldBe(ProgramText);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Clicking an unselected tab selects its file and shows its text.</summary>
    [Fact]
    public async Task ClickingATabSelectsItsFile()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var model = EditorHarness.NewModel();
            model.Open(ProgramPath, ProgramText);
            model.Open(SettingsPath, SettingsText);
            using var window = EditorHarness.Show(model);

            EditorHarness.Click(window, EditorHarness.TabToggle(window, "Program.cs"));

            model.SelectedTab!.RelativePath.ShouldBe(ProgramPath);
            EditorHarness.TabToggle(window, "Program.cs").IsChecked.ShouldBe(true);
            EditorHarness.TabToggle(window, "settings.json").IsChecked.ShouldBe(false);
            EditorHarness.Code(window).Text.ShouldBe(ProgramText);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Clicking the selected tab leaves it selected, so the toggle never shows a false state for the open file.</summary>
    [Fact]
    public async Task ClickingTheSelectedTabKeepsItChecked()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var model = EditorHarness.NewModel();
            model.Open(ProgramPath, ProgramText);
            using var window = EditorHarness.Show(model);

            EditorHarness.Click(window, EditorHarness.TabToggle(window, "Program.cs"));

            model.SelectedTab!.RelativePath.ShouldBe(ProgramPath);
            EditorHarness.TabToggle(window, "Program.cs").IsChecked.ShouldBe(true);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Clicking a tab's close button removes that tab and selects the tab that takes its place.</summary>
    [Fact]
    public async Task ClosingTheSelectedTabSelectsItsSuccessor()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var model = EditorHarness.NewModel();
            model.Open("a.cs", "A");
            model.Open("b.cs", "B");
            model.Open("c.cs", "C");
            model.Select("b.cs");
            using var window = EditorHarness.Show(model);

            EditorHarness.Click(window, EditorHarness.TabClose(window, "b.cs"));

            model.Items.Select(item => item.Name).ToArray().ShouldBe(RemainingNames);
            model.SelectedTab!.RelativePath.ShouldBe("c.cs");
            EditorHarness.Code(window).Text.ShouldBe("C");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Closing the last open tab leaves no selected tab and an empty editor.</summary>
    [Fact]
    public async Task ClosingTheLastTabLeavesAnEmptyEditor()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var model = EditorHarness.NewModel();
            model.Open(ProgramPath, ProgramText);
            using var window = EditorHarness.Show(model);

            EditorHarness.Click(window, EditorHarness.TabClose(window, "Program.cs"));

            model.Items.ShouldBeEmpty();
            model.SelectedTab.ShouldBeNull();
            EditorHarness.Code(window).Text.ShouldBe(string.Empty);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Keyboard Enter on a focused tab selects it, so the tab strip works without a pointer.</summary>
    [Fact]
    public async Task EnterOnAFocusedTabSelectsIt()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var model = EditorHarness.NewModel();
            model.Open(ProgramPath, ProgramText);
            model.Open(SettingsPath, SettingsText);
            using var window = EditorHarness.Show(model);

            EditorHarness.TabToggle(window, "Program.cs").Focus();
            EditorHarness.Press(window, Key.Enter, PhysicalKey.Enter);

            model.SelectedTab!.RelativePath.ShouldBe(ProgramPath);
            EditorHarness.Code(window).Text.ShouldBe(ProgramText);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Typing in the editor changes the selected tab's buffer, and the edit survives switching away and back.</summary>
    [Fact]
    public async Task EditsReachTheTabBufferAndSurviveATabSwitch()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var model = EditorHarness.NewModel();
            model.Open(ProgramPath, ProgramText);
            model.Open(SettingsPath, SettingsText);
            using var window = EditorHarness.Show(model);

            EditorHarness.Code(window).Document.Insert(0, "// edited\n");
            ShellHarness.Settle(window);

            model.Items.Count.ShouldBe(2);
            model.SelectedTab!.Document.Text.ShouldBe("// edited\n" + SettingsText);

            EditorHarness.Click(window, EditorHarness.TabToggle(window, "Program.cs"));
            EditorHarness.Click(window, EditorHarness.TabToggle(window, "settings.json"));

            EditorHarness.Code(window).Text.ShouldBe("// edited\n" + SettingsText);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }
}
