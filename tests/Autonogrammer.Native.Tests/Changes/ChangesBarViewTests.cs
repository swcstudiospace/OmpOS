using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Autonogrammer.Native.Tests.Diff;
using Autonogrammer.Native.Views.Diff;
using Autonogrammer.Presentation.Changes;
using Autonogrammer.Presentation.Editor;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Changes;

/// <summary>
/// The Changes bar under the transcript of a chat session, and the Diff review tool it feeds: a scripted run with file-edit blocks lists the
/// changed files, Undo restores them on disk, a file that changed since is refused, and Review opens the tool with that file's diff.
/// </summary>
public sealed class ChangesBarViewTests
{
    private const string Old = "alpha\nbeta\ngamma\n";
    private const string New = "alpha\nBETA\ngamma\n";

    private static readonly string[] ChangedPaths = ["src/a.txt", "made.txt"];

    /// <summary>A session that changed no file shows no bar.</summary>
    [Fact]
    public async Task TheBarIsHiddenWhileTheSessionChangedNoFile()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.Dispatch(() =>
        {
            using var fixture = new ChangesFixture();

            fixture.Bar.IsEffectivelyVisible.ShouldBeFalse();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>After a scripted run with file edits the bar lists each file with its counts, and every control carries a name from the copy constants.</summary>
    [Fact]
    public async Task TheBarListsTheChangedFilesWithNamedControls()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.Dispatch(() =>
        {
            using var fixture = new ChangesFixture();
            fixture.Write("src/a.txt", New);
            fixture.Edit("c1", Path.Combine(fixture.Root, "src", "a.txt"), Old, New);
            fixture.Edit("c2", "made.txt", null, "x\ny\n");

            fixture.Bar.IsEffectivelyVisible.ShouldBeTrue();
            var texts = ChangesFixture.Texts(fixture.Bar);
            texts.ShouldContain("src/a.txt", StringComparer.Ordinal);
            texts.ShouldContain("made.txt", StringComparer.Ordinal);
            texts.ShouldContain("+1", StringComparer.Ordinal);
            texts.ShouldContain("-1", StringComparer.Ordinal);
            texts.ShouldContain("+2", StringComparer.Ordinal);
            texts.ShouldContain("2 files changed +3 -1", StringComparer.Ordinal);
            foreach (var path in ChangedPaths)
            {
                ChangesFixture.Named(fixture.Bar, ChangeCopy.ReviewName(path)).IsEffectivelyEnabled.ShouldBeTrue();
                ChangesFixture.Named(fixture.Bar, ChangeCopy.KeepName(path)).IsEffectivelyEnabled.ShouldBeTrue();
                ChangesFixture.Named(fixture.Bar, ChangeCopy.UndoName(path)).IsEffectivelyEnabled.ShouldBeTrue();
            }

            ChangesFixture.Named(fixture.Bar, ChangeCopy.KeepAllName).ShouldNotBeNull();
            ChangesFixture.Named(fixture.Bar, ChangeCopy.UndoAllName).ShouldNotBeNull();
            AutomationProperties.GetName(fixture.Bar).ShouldBe(ChangeCopy.BarName);
            var result = fixture.Bar.FindControl<TextBlock>("ResultText")!;
            AutomationProperties.GetLiveSetting(result).ShouldBe(AutomationLiveSetting.Polite);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The edits of a failed tool call never reach the bar.</summary>
    [Fact]
    public async Task AFailedCallIsNotListed()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.Dispatch(() =>
        {
            using var fixture = new ChangesFixture();

            fixture.Edit("c1", "a.txt", "old\n", "new\n", status: "failed");

            fixture.Bar.IsEffectivelyVisible.ShouldBeFalse();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Undo on a file restores the text from before the agent on disk, says so in the live region, and disables the file's controls.</summary>
    [Fact]
    public async Task UndoRestoresTheFileOnDisk()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.Dispatch(() =>
        {
            using var fixture = new ChangesFixture();
            fixture.Write("a.txt", New);
            fixture.Write("made.txt", "x\n");
            fixture.Edit("c1", "a.txt", Old, New);
            fixture.Edit("c2", "made.txt", null, "x\n");

            DiffHarness.Click(fixture.Window, ChangesFixture.Named(fixture.Bar, ChangeCopy.UndoName("a.txt")));
            DiffHarness.Click(fixture.Window, ChangesFixture.Named(fixture.Bar, ChangeCopy.UndoName("made.txt")));

            fixture.Read("a.txt").ShouldBe(Old);
            fixture.Exists("made.txt").ShouldBeFalse("a file the agent created is removed by its undo");
            var result = fixture.Bar.FindControl<TextBlock>("ResultText")!;
            result.IsVisible.ShouldBeTrue();
            result.Text.ShouldBe(ChangeCopy.RemovedMessage("made.txt"));
            ChangesFixture.Named(fixture.Bar, ChangeCopy.UndoName("a.txt")).IsEffectivelyEnabled.ShouldBeFalse();
            ChangesFixture.Texts(fixture.Bar).ShouldContain("Undone", StringComparer.Ordinal);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A file that changed since the agent edited it is refused: nothing is written and the live region says to review it by hand.</summary>
    [Fact]
    public async Task UndoOfAFileChangedSinceIsRefused()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.Dispatch(() =>
        {
            using var fixture = new ChangesFixture();
            fixture.Write("a.txt", "the user's text\n");
            fixture.Edit("c1", "a.txt", Old, New);

            DiffHarness.Click(fixture.Window, ChangesFixture.Named(fixture.Bar, ChangeCopy.UndoName("a.txt")));

            fixture.Read("a.txt").ShouldBe("the user's text\n");
            var result = fixture.Bar.FindControl<TextBlock>("ResultText")!;
            result.Text.ShouldNotBeNull().ShouldContain(ChangeCopy.StaleMessage, Case.Sensitive);
            ChangesFixture.Texts(fixture.Bar).ShouldContain("Changed since", StringComparer.Ordinal);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Keep on one file writes nothing, marks only that file kept and leaves the other file's controls enabled.</summary>
    [Fact]
    public async Task KeepMarksOneFileAndLeavesTheOtherPending()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.Dispatch(() =>
        {
            using var fixture = new ChangesFixture();
            fixture.Write("a.txt", New);
            fixture.Write("b.txt", "B\n");
            fixture.Edit("c1", "a.txt", Old, New);
            fixture.Edit("c2", "b.txt", "b\n", "B\n");

            DiffHarness.Click(fixture.Window, ChangesFixture.Named(fixture.Bar, ChangeCopy.KeepName("a.txt")));

            fixture.Read("a.txt").ShouldBe(New);
            fixture.Read("b.txt").ShouldBe("B\n");
            fixture.Bar.FindControl<TextBlock>("ResultText")!.Text.ShouldBe(ChangeCopy.KeptMessage("a.txt"));
            ChangesFixture.Named(fixture.Bar, ChangeCopy.KeepName("a.txt")).IsEffectivelyEnabled.ShouldBeFalse();
            ChangesFixture.Named(fixture.Bar, ChangeCopy.KeepName("b.txt")).IsEffectivelyEnabled.ShouldBeTrue();
            ChangesFixture.Named(fixture.Bar, ChangeCopy.UndoName("b.txt")).IsEffectivelyEnabled.ShouldBeTrue();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A CRLF file whose transcript text uses line feeds is not reported stale: its undo restores the text with the file's CRLF endings.</summary>
    [Fact]
    public async Task UndoOfACrlfFileRestoresItWithItsEndings()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.Dispatch(() =>
        {
            using var fixture = new ChangesFixture();
            fixture.Write("a.txt", "alpha\r\nBETA\r\ngamma\r\n");
            fixture.Edit("c1", "a.txt", Old, New);

            DiffHarness.Click(fixture.Window, ChangesFixture.Named(fixture.Bar, ChangeCopy.UndoName("a.txt")));

            fixture.Read("a.txt").ShouldBe("alpha\r\nbeta\r\ngamma\r\n");
            fixture.Bar.FindControl<TextBlock>("ResultText")!.Text.ShouldBe(ChangeCopy.UndoneMessage("a.txt"));
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Keep all keeps every file and writes nothing; Undo all restores the files that can be restored.</summary>
    [Fact]
    public async Task KeepAllAndUndoAllActOnEveryFile()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.Dispatch(() =>
        {
            using var kept = new ChangesFixture();
            kept.Write("a.txt", New);
            kept.Edit("c1", "a.txt", Old, New);
            DiffHarness.Click(kept.Window, ChangesFixture.Named(kept.Bar, ChangeCopy.KeepAllName));
            kept.Read("a.txt").ShouldBe(New);
            ChangesFixture.Texts(kept.Bar).ShouldContain("Kept", StringComparer.Ordinal);
            ChangesFixture.Named(kept.Bar, ChangeCopy.KeepAllName).IsEffectivelyEnabled.ShouldBeFalse();

            using var undone = new ChangesFixture();
            undone.Write("a.txt", New);
            undone.Write("b.txt", "B\n");
            undone.Edit("c1", "a.txt", Old, New);
            undone.Edit("c2", "b.txt", "b\n", "B\n");
            DiffHarness.Click(undone.Window, ChangesFixture.Named(undone.Bar, ChangeCopy.UndoAllName));
            undone.Read("a.txt").ShouldBe(Old);
            undone.Read("b.txt").ShouldBe("b\n");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Review opens the Diff review tool with that file's diff: the text before the agent as the original, every hunk accepted.</summary>
    [Fact]
    public async Task ReviewOpensTheDiffReviewToolWithThatFilesDiff()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.Dispatch(() =>
        {
            using var fixture = new ChangesFixture();
            fixture.Write("a.txt", New);
            fixture.Edit("c1", "a.txt", Old, New);

            DiffHarness.Click(fixture.Window, ChangesFixture.Named(fixture.Bar, ChangeCopy.ReviewName("a.txt")));

            fixture.ToolHost.IsVisible.ShouldBeTrue();
            var view = fixture.ToolHost.Content.ShouldBeOfType<DiffReviewView>();
            var review = view.DataContext.ShouldBeOfType<DiffReview>();
            review.Original.ShouldBe(Old);
            review.Hunks.Count.ShouldBe(1);
            review.Hunks[0].Decision.ShouldBe(HunkDecision.Accept);
            view.FindControl<TextBox>("ResultBox")!.Text.ShouldBe(New);
            ChangesFixture.Texts(view).ShouldContain(ChangeCopy.ReviewTitle("a.txt"), StringComparer.Ordinal);
            view.FindControl<Control>("EmptyText")!.IsVisible.ShouldBeFalse();
            fixture.Bar.IsEffectivelyVisible.ShouldBeFalse("the bar hides with the transcript while a tool is open");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Rejecting the hunk in the tool and applying the review reverts the file on disk, and the tool goes back to its empty sentence.</summary>
    [Fact]
    public async Task RejectingTheHunkAndApplyingTheReviewRevertsTheFile()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.Dispatch(() =>
        {
            using var fixture = new ChangesFixture();
            fixture.Write("a.txt", New);
            fixture.Edit("c1", "a.txt", Old, New);
            DiffHarness.Click(fixture.Window, ChangesFixture.Named(fixture.Bar, ChangeCopy.ReviewName("a.txt")));
            var view = fixture.ToolHost.Content.ShouldBeOfType<DiffReviewView>();

            DiffHarness.Click(fixture.Window, ChangesFixture.Named(view, "Reject hunk 1"));
            view.FindControl<TextBox>("ResultBox")!.Text.ShouldBe(Old);
            fixture.Read("a.txt").ShouldBe(New);
            DiffHarness.Click(fixture.Window, ChangesFixture.Named(view, ChangeCopy.ApplyReviewName));

            fixture.Read("a.txt").ShouldBe(Old);
            view.DataContext.ShouldBeNull();
            view.FindControl<TextBlock>("EmptyText")!.Text.ShouldBe(ChangeCopy.NothingToReview);
            fixture.Main.FindControl<TextBlock>("StatusLine")!.Text.ShouldBe(ChangeCopy.UndoneMessage("a.txt"));
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Applying a review of a file that changed since the agent edited it writes nothing and says why in the tool.</summary>
    [Fact]
    public async Task ApplyingAReviewOfAChangedFileWritesNothing()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.Dispatch(() =>
        {
            using var fixture = new ChangesFixture();
            fixture.Write("a.txt", New);
            fixture.Edit("c1", "a.txt", Old, New);
            DiffHarness.Click(fixture.Window, ChangesFixture.Named(fixture.Bar, ChangeCopy.ReviewName("a.txt")));
            var view = fixture.ToolHost.Content.ShouldBeOfType<DiffReviewView>();
            DiffHarness.Click(fixture.Window, ChangesFixture.Named(view, "Reject hunk 1"));
            fixture.Write("a.txt", "the user's text\n");

            DiffHarness.Click(fixture.Window, ChangesFixture.Named(view, ChangeCopy.ApplyReviewName));

            fixture.Read("a.txt").ShouldBe("the user's text\n");
            view.FindControl<TextBlock>("ResultText")!.Text.ShouldBe(ChangeCopy.StaleMessage);
            view.DataContext.ShouldBeOfType<DiffReview>();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The Diff review tool opened from its toggle with nothing fed says so, instead of standing empty.</summary>
    [Fact]
    public async Task TheDiffReviewToolSaysThereIsNothingToReview()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.Dispatch(() =>
        {
            using var fixture = new ChangesFixture();

            fixture.Main.FindControl<ToggleButton>("DiffToggle")!.IsChecked = true;
            ShellHarness.Settle(fixture.Window);

            var view = fixture.ToolHost.Content.ShouldBeOfType<DiffReviewView>();
            var empty = view.FindControl<TextBlock>("EmptyText")!;
            empty.IsVisible.ShouldBeTrue();
            empty.Text.ShouldBe("No changes to review.");
            view.FindControl<Control>("ReviewContent")!.IsVisible.ShouldBeFalse();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The path of a changed file is drawn in the mono token face, not a literal font, and the markup holds no literal font or colour.</summary>
    [Fact]
    public async Task TheBarUsesTheFontTokensAndNoLiterals()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.Dispatch(() =>
        {
            using var fixture = new ChangesFixture();
            fixture.Write("a.txt", New);
            fixture.Edit("c1", "a.txt", Old, New);

            var path = ChangesFixture.TextBlockWith(fixture.Bar, "a.txt");
            var token = (FontFamily)fixture.Bar.FindResource("Autonogrammer.Font.Mono")!;
            path.FontFamily.ShouldBe(token);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);

        foreach (var file in new[] { "Changes/ChangesBarView.axaml", "Diff/DiffReviewView.axaml" })
        {
            var markup = await File.ReadAllTextAsync(Path.Combine(ShellHarness.RepoRoot(), "src", "Autonogrammer.Native.Views", "Views", file), TestContext.Current.CancellationToken).ConfigureAwait(true);
            markup.ShouldNotContain("FontFamily=\"", Case.Sensitive);
            markup.ShouldNotContain("=\"#", Case.Sensitive);
        }
    }
}
