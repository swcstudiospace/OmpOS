using Avalonia.Automation;
using Avalonia.Controls;
using OmpOS.Native.Views.Settings;
using OmpOS.Presentation.Settings.Worktrees;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.Settings;

/// <summary>The Worktrees page view in a headless window: the facts it draws, the setup states, the error row and that Refresh reads again.</summary>
public sealed class WorktreesPageViewTests
{
    private sealed record Screen(WorktreesSettingsPage Page, FakeWorktreesSource Source, WorktreesPageView View, Window Window) : IDisposable
    {
        public void Dispose() => Window.Close();
    }

    private static async Task<Screen> ShowAsync(FakeWorktreesSource source, CancellationToken cancellationToken)
    {
        var page = new WorktreesSettingsPage(source);
        await page.LoadAsync(cancellationToken).ConfigureAwait(true);
        var view = new WorktreesPageView { DataContext = page };
        var window = new Window { Width = 900, Height = 1400, Content = view };
        window.Show();
        SettingsTree.SettleSafe(window);
        return new Screen(page, source, view, window);
    }

    /// <summary>With no setup file the view shows the fixed facts and the no-setup sentence, and no command, file or script block.</summary>
    [Fact]
    public async Task NoSetupFileShowsTheFixedFactsOnly()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var screen = await ShowAsync(new FakeWorktreesSource(), ct).ConfigureAwait(true);
            var texts = SettingsTree.Texts(screen.View);

            texts.ShouldContain(WorktreesCopy.Heading, StringComparer.Ordinal);
            texts.ShouldContain("/work/app", StringComparer.Ordinal);
            texts.ShouldContain(WorktreesCopy.StorageText, StringComparer.Ordinal);
            texts.ShouldContain(WorktreesCopy.CleanupText, StringComparer.Ordinal);
            texts.ShouldContain(WorktreesCopy.CapText(25), StringComparer.Ordinal);
            texts.ShouldContain(WorktreesCopy.NoSetupFile, StringComparer.Ordinal);
            screen.View.Commands.IsEffectivelyVisible.ShouldBeFalse();
            screen.View.SetupError.IsEffectivelyVisible.ShouldBeFalse();
        }, ct).ConfigureAwait(true);
    }

    /// <summary>An unapproved setup lists each command as a named row and says that nothing runs until it is approved.</summary>
    [Fact]
    public async Task AnUnapprovedSetupListsTheCommands()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var source = new FakeWorktreesSource
            {
                Snapshot = new("/work/app", 25, WorktreeSetupState.NotGranted, "/work/app/.cursor/worktrees.json", ["npm ci", "npm run build"], string.Empty, string.Empty),
            };
            using var screen = await ShowAsync(source, ct).ConfigureAwait(true);
            var texts = SettingsTree.Texts(screen.View);

            texts.ShouldContain(WorktreesCopy.NotGranted, StringComparer.Ordinal);
            texts.ShouldContain("npm ci", StringComparer.Ordinal);
            texts.ShouldContain("npm run build", StringComparer.Ordinal);
            texts.ShouldContain("/work/app/.cursor/worktrees.json", StringComparer.Ordinal);
            screen.View.Commands.IsEffectivelyVisible.ShouldBeTrue();
            SettingsTree.Has<Border>(screen.View, "npm ci").ShouldBeTrue();
        }, ct).ConfigureAwait(true);
    }

    /// <summary>An invalid file shows the error row with the message in the error tone, and no commands.</summary>
    [Fact]
    public async Task AnInvalidFileShowsTheErrorRow()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var source = new FakeWorktreesSource
            {
                Snapshot = new("/work/app", 25, WorktreeSetupState.Invalid, string.Empty, [], string.Empty, "worktrees.json is not valid JSON: x"),
            };
            using var screen = await ShowAsync(source, ct).ConfigureAwait(true);

            screen.View.SetupError.IsEffectivelyVisible.ShouldBeTrue();
            screen.View.SetupError.Text.ShouldBe("worktrees.json is not valid JSON: x", Case.Sensitive);
            screen.View.SetupError.Classes.ShouldContain("tone-error");
            screen.View.SetupState.Text.ShouldBe(WorktreesCopy.Invalid, Case.Sensitive);
            screen.View.Commands.IsEffectivelyVisible.ShouldBeFalse();
        }, ct).ConfigureAwait(true);
    }

    /// <summary>Refresh is the only button, it is named, as large as a target must be, and pressing it reads the source again and draws what changed.</summary>
    [Fact]
    public async Task RefreshReadsAgainAndDrawsTheChange()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var screen = await ShowAsync(new FakeWorktreesSource(), ct).ConfigureAwait(true);
            var buttons = SettingsTree.Visible<Button>(screen.View);

            buttons.ShouldBe([screen.View.Refresher]);
            AutomationProperties.GetName(screen.View.Refresher).ShouldBe(WorktreesCopy.RefreshLabel, Case.Sensitive);
            screen.View.Refresher.MinHeight.ShouldBeGreaterThanOrEqualTo(TargetSize.MinimumTarget);
            screen.View.Refresher.MinWidth.ShouldBeGreaterThanOrEqualTo(TargetSize.MinimumTarget);

            screen.Source.Snapshot = new("/work/app", 25, WorktreeSetupState.Granted, "/work/app/.cursor/worktrees.json", ["make"], string.Empty, string.Empty);
            SettingsTree.Click(screen.Window, screen.View.Refresher);
            SettingsTree.SettleSafe(screen.Window);

            screen.Source.LoadCount.ShouldBe(2);
            SettingsTree.Texts(screen.View).ShouldContain(WorktreesCopy.Granted, StringComparer.Ordinal);
            SettingsTree.Texts(screen.View).ShouldContain("make", StringComparer.Ordinal);
        }, ct).ConfigureAwait(true);
    }

    /// <summary>Every visible string comes from the copy class or the snapshot, and every authored control is named.</summary>
    [Fact]
    public async Task EveryStringComesFromTheCopyOrTheSnapshotAndEveryControlIsNamed()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var source = new FakeWorktreesSource
            {
                Snapshot = new("/work/app", 25, WorktreeSetupState.NotGranted, "/work/app/.cursor/worktrees.json", ["npm ci"], "/work/app/.cursor/s.sh", string.Empty),
            };
            using var screen = await ShowAsync(source, ct).ConfigureAwait(true);
            var allowed = new HashSet<string>(
                [
                    WorktreesCopy.Heading, WorktreesCopy.Description, WorktreesCopy.WorkspaceLabel, WorktreesCopy.StorageLabel, WorktreesCopy.StorageText,
                    WorktreesCopy.CapLabel, WorktreesCopy.CapText(25), WorktreesCopy.CleanupLabel, WorktreesCopy.CleanupText, WorktreesCopy.SetupHeading,
                    WorktreesCopy.SetupDescription, WorktreesCopy.SetupFileLabel, WorktreesCopy.ScriptLabel, WorktreesCopy.CommandsLabel, WorktreesCopy.NotGranted,
                    WorktreesCopy.RefreshLabel, WorktreesCopy.RefreshHelp, "/work/app", "/work/app/.cursor/worktrees.json", "/work/app/.cursor/s.sh", "npm ci",
                ],
                StringComparer.Ordinal);

            SettingsTree.Texts(screen.View).ShouldAllBe(text => allowed.Contains(text), "every visible text comes from the copy or the snapshot");
            SettingsTree.Unnamed(screen.View).ShouldBeEmpty();
        }, ct).ConfigureAwait(true);
    }
}
