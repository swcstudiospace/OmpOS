using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Autonogrammer.Native.Tests.Settings;
using Autonogrammer.Native.Views.Safety;
using Autonogrammer.Presentation.Safety;
using Autonogrammer.Presentation.Settings;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Safety;

/// <summary>The Run mode page view in a headless window: what it draws for each mode, how entries are added and removed, naming, target sizes and the keyboard.</summary>
public sealed class RunModePageViewTests
{
    private sealed record Screen(RunModeSettingsPage Page, FakeRunModeSource Source, RunModePageView View, Window Window) : IDisposable
    {
        public void Dispose() => Window.Close();
    }

    private static async Task<Screen> ShowAsync(CancellationToken cancellationToken)
    {
        var source = new FakeRunModeSource();
        var page = new RunModeSettingsPage(SettingsSection.Agents, source);
        await page.LoadAsync(cancellationToken).ConfigureAwait(true);
        var view = new RunModePageView { DataContext = page };
        var window = new Window { Width = 900, Height = 2400, Content = view };
        window.Show();
        SettingsTree.SettleSafe(window);
        return new Screen(page, source, view, window);
    }

    [Fact]
    public async Task TheViewDrawsTheModesTheClassifierNoteAndTheListsFromThePage()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var screen = await ShowAsync(ct).ConfigureAwait(true);
            var texts = SettingsTree.Texts(screen.View);

            texts.ShouldContain(SafetyCopy.Heading, StringComparer.Ordinal);
            texts.ShouldContain(SafetyCopy.AutoReviewTitle, StringComparer.Ordinal);
            texts.ShouldContain(SafetyCopy.AllowlistTitle, StringComparer.Ordinal);
            texts.ShouldContain(SafetyCopy.RunEverythingTitle, StringComparer.Ordinal);
            texts.ShouldContain(SafetyCopy.ClassifierHeading, StringComparer.Ordinal);
            texts.ShouldContain(SafetyCopy.NoSandboxNote, StringComparer.Ordinal);
            texts.ShouldContain("git status", StringComparer.Ordinal);
            texts.ShouldContain("npm", StringComparer.Ordinal);
            texts.ShouldContain("Shell(rm)", StringComparer.Ordinal);
            texts.ShouldContain("It is not valid JSON. It is ignored.", StringComparer.Ordinal);
            screen.View.Classifier.IsEffectivelyVisible.ShouldBeTrue();
            screen.View.RunEverythingNote.IsEffectivelyVisible.ShouldBeFalse();
            screen.View.Modes.Items.Count.ShouldBe(3);
            ((ListBoxItem)screen.View.Modes.SelectedItem!).Tag.ShouldBeOfType<RunModeOption>().Choice.ShouldBe(RunModeChoice.AutoReview);
            screen.View.Boundaries.Items.Count.ShouldBe(2);
        }, ct).ConfigureAwait(true);
    }

    [Fact]
    public async Task ChoosingRunEverythingShowsTheWarningAndHidesTheClassifierNoteAndTheReadAccess()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var screen = await ShowAsync(ct).ConfigureAwait(true);

            screen.View.Modes.SelectedItem = screen.View.Modes.Items.OfType<ListBoxItem>().Last();
            SettingsTree.SettleSafe(screen.Window);

            screen.Page.Mode.ShouldBe(RunModeChoice.RunEverything);
            screen.View.RunEverythingNote.IsEffectivelyVisible.ShouldBeTrue();
            screen.View.Classifier.IsEffectivelyVisible.ShouldBeFalse();
            SettingsTree.Texts(screen.View).ShouldContain(SafetyCopy.ReadHiddenNote, StringComparer.Ordinal);
            screen.Source.Saved.ShouldBeEmpty("a choice is a draft until Save");
        }, ct).ConfigureAwait(true);
    }

    [Fact]
    public async Task AnEntryIsAddedFromTheBoxByTheButtonAndByEnterAndARejectedOneKeepsItsTextAndSaysWhy()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var screen = await ShowAsync(ct).ConfigureAwait(true);

            screen.View.TerminalEntryBox.Text = "make";
            SettingsTree.Click(screen.Window, screen.View.AddTerminal);

            screen.Page.Draft.TerminalAllowlist.ShouldBe(["git status", "make"]);
            screen.View.TerminalEntryBox.Text.ShouldBeNullOrEmpty();
            SettingsTree.Texts(screen.View).ShouldContain("make", StringComparer.Ordinal);

            screen.View.TerminalEntryBox.Text = "cargo build";
            _ = screen.View.TerminalEntryBox.Focus();
            SettingsTree.Press(screen.Window, Key.Enter, PhysicalKey.Enter);

            screen.Page.Draft.TerminalAllowlist.ShouldBe(["git status", "make", "cargo build"]);

            screen.View.McpEntryBox.Text = "not-a-tool";
            SettingsTree.Click(screen.Window, screen.View.AddMcp);

            screen.View.McpEntryBox.Text.ShouldBe("not-a-tool");
            screen.View.EntryError.IsEffectivelyVisible.ShouldBeTrue();
            screen.View.EntryError.Text.ShouldBe(SafetyCopy.McpEntryInvalid);
        }, ct).ConfigureAwait(true);
    }

    [Fact]
    public async Task AnEntryOfTheEditedFileHasANamedRemoveButtonAndAnEntryOfAnotherFileHasNone()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var screen = await ShowAsync(ct).ConfigureAwait(true);

            SettingsTree.Has<Button>(screen.View, "Remove git status").ShouldBeTrue();
            SettingsTree.Has<Button>(screen.View, "Remove npm").ShouldBeFalse();

            SettingsTree.Click(screen.Window, SettingsTree.Named<Button>(screen.View, "Remove git status"));

            screen.Page.Draft.TerminalAllowlist.ShouldBeEmpty();
            SettingsTree.Has<Button>(screen.View, "Remove git status").ShouldBeFalse();
            screen.Page.TerminalRows.Select(row => row.Text).ToArray().ShouldBe(["npm"]);
        }, ct).ConfigureAwait(true);
    }

    [Fact]
    public async Task EveryFocusableControlAndLiveRegionIsNamedAndEveryTargetIsBigEnough()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var screen = await ShowAsync(ct).ConfigureAwait(true);
            screen.View.TerminalEntryBox.Text = "make";
            SettingsTree.Click(screen.Window, screen.View.AddTerminal);
            screen.View.McpEntryBox.Text = "bad";
            SettingsTree.Click(screen.Window, screen.View.AddMcp);

            SettingsTree.Unnamed(screen.View).ShouldBeEmpty();
            var minimum = (double)Application.Current!.FindResource("Autonogrammer.Size.TargetMin")!;
            foreach (var button in SettingsTree.Visible<Button>(screen.View))
            {
                button.MinHeight.ShouldBeGreaterThanOrEqualTo(minimum, AutomationProperties.GetName(button));
            }

            screen.View.TerminalEntryBox.MinHeight.ShouldBeGreaterThanOrEqualTo(minimum);
            SettingsTree.Descriptions(screen.View).ShouldContain(SafetyCopy.TerminalAddLabel, StringComparer.Ordinal);
        }, ct).ConfigureAwait(true);
    }

    [Fact]
    public async Task TheKeyboardReachesTheEntryBoxesInPageOrder()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var screen = await ShowAsync(ct).ConfigureAwait(true);

            var stops = SettingsTree.TabStops(screen.View).Select(control => AutomationProperties.GetName(control)).ToList();

            stops.IndexOf(SafetyCopy.TerminalAddLabel).ShouldBeGreaterThanOrEqualTo(0);
            stops.IndexOf(SafetyCopy.TerminalAddLabel).ShouldBeLessThan(stops.IndexOf(SafetyCopy.McpAddLabel));
            stops.IndexOf(SafetyCopy.McpAddLabel).ShouldBeLessThan(stops.IndexOf(SafetyCopy.ReadPathAddLabel));
        }, ct).ConfigureAwait(true);
    }

    [Fact]
    public async Task WithNoFileToWriteTheBoxesAndTheBoundaryAreDisabledAndTheReasonIsShown()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var source = new FakeRunModeSource();
            source.Snapshot = source.Snapshot with { EditPermissionsPath = string.Empty, EditSandboxPath = string.Empty };
            var page = new RunModeSettingsPage(SettingsSection.Agents, source);
            await page.LoadAsync(ct).ConfigureAwait(true);
            var view = new RunModePageView { DataContext = page };
            var window = new Window { Width = 900, Height = 2400, Content = view };
            window.Show();
            SettingsTree.SettleSafe(window);

            view.TerminalEntryBox.IsEnabled.ShouldBeFalse();
            view.AddTerminal.IsEnabled.ShouldBeFalse();
            view.Boundaries.IsEnabled.ShouldBeFalse();
            SettingsTree.Texts(view).ShouldContain(SafetyCopy.SavedTo(string.Empty), StringComparer.Ordinal);
            window.Close();
        }, ct).ConfigureAwait(true);
    }

    [Fact]
    public async Task APageThatIsStillLoadingShowsTheLoadingStateNotTheControls()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(() =>
        {
            var page = new RunModeSettingsPage(SettingsSection.Agents, new FakeRunModeSource());
            var view = new RunModePageView { DataContext = page };
            var window = new Window { Width = 900, Height = 1200, Content = view };
            window.Show();
            SettingsTree.SettleSafe(window);

            view.Status.IsLoadingShown.ShouldBeTrue();
            // The ready panel is hidden, so its content is never attached to the visual tree: ask the panel, not a control inside it.
            view.FindControl<ScrollViewer>("ReadyPanel").ShouldNotBeNull().IsVisible.ShouldBeFalse();
            SettingsTree.Unnamed(view).ShouldBeEmpty();
            window.Close();
            return Task.CompletedTask;
        }, ct).ConfigureAwait(true);
    }
}
