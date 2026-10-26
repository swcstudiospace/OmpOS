using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;
using OmpOS.Contracts.Agents;
using OmpOS.Native.Tests.AgentPanel.Views;
using OmpOS.Native.Views.Kanban;
using OmpOS.Presentation.Kanban;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.Kanban;

/// <summary>The Kanban tab states: loading skeleton, empty with a way to start, error with retry, the last-known banner, and the populated columns.</summary>
public sealed class KanbanBoardViewStateTests
{
    private static readonly string[] ColumnKeys = ["backlog", "queued", "running", "awaiting-input", "needs-attention", "paused", "completed", "failed", "unreachable", "archived"];

    /// <summary>Loading shows its words and skeleton bars, never a spinner, and nothing else; the bars pulse only under full motion.</summary>
    [Fact]
    public async Task LoadingShowsSkeletonBarsAndNoSpinnerAndHonoursReducedMotion()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            using var full = KanbanFixture.Open(startLoading: true);
            var texts = ViewHarness.VisibleTexts(full.View);
            texts.ShouldContain(KanbanCopy.LoadingHeading, StringComparer.Ordinal);
            texts.ShouldContain(KanbanCopy.LoadingBody, StringComparer.Ordinal);
            full.View.SkeletonBars.Count().ShouldBe(9);
            full.View.PulsingSkeletons.ShouldBe(9);
            full.View.GetVisualDescendants().OfType<ProgressBar>().ShouldBeEmpty("loading is static text and bars, never a spinner");
            ViewHarness.VisibleTexts(full.View).ShouldNotContain(KanbanCopy.EmptyHeading, StringComparer.Ordinal);
            full.View.ColumnOf("running")?.IsEffectivelyVisible.ShouldNotBe(true);
            AutomationProperties.GetName(full.View).ShouldBe(KanbanCopy.BoardName);

            using var reduced = KanbanFixture.Open(startLoading: true, reducedMotion: true);
            reduced.View.SkeletonBars.Count().ShouldBe(9);
            reduced.View.PulsingSkeletons.ShouldBe(0, "no animation under reduced motion");

            full.Board.CompleteLoading();
            full.Settle();
            ViewHarness.VisibleTexts(full.View).ShouldNotContain(KanbanCopy.LoadingHeading, StringComparer.Ordinal);
            full.View.ColumnOf("running")!.IsEffectivelyVisible.ShouldBeTrue();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Empty says there is no session yet and its button asks the owner to start one.</summary>
    [Fact]
    public async Task EmptyShowsItsCopyAndTheStartButtonRaisesTheRequest()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            using var fixture = KanbanFixture.Open((_, _) => { });
            var texts = ViewHarness.VisibleTexts(fixture.View);
            texts.ShouldContain("No sessions yet", StringComparer.Ordinal);
            texts.ShouldContain(KanbanCopy.EmptyBody, StringComparer.Ordinal);
            fixture.Board.State.ShouldBe(BoardState.Empty);

            var start = fixture.View.StartSession;
            start.IsEffectivelyVisible.ShouldBeTrue();
            start.Classes.Contains("primary").ShouldBeTrue();
            start.MinHeight.ShouldBeGreaterThanOrEqualTo(44);
            AutomationProperties.GetName(start).ShouldBe(KanbanCopy.StartSessionName);
            var raised = 0;
            fixture.Board.StartSessionRequested += (_, _) => raised++;

            ViewHarness.Click(fixture.Window, start);

            raised.ShouldBe(1);
            fixture.View.ColumnOf("running")?.IsEffectivelyVisible.ShouldNotBe(true);
            ViewHarness.Unnamed(fixture.View).ShouldBeEmpty();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Error shows the reason and a Retry button; Retry reads again and the populated board replaces the error.</summary>
    [Fact]
    public async Task ErrorShowsTheReasonAndRetryRecovers()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            using var fixture = KanbanFixture.Open((target, clock) =>
            {
                target.Failure = new InvalidOperationException("The registry is offline");
                target.Sessions.Add(KanbanFixture.Session(clock, "run-1", SessionLifecycle.Running, "Build the board"));
            });
            fixture.Board.State.ShouldBe(BoardState.Error);
            var texts = ViewHarness.VisibleTexts(fixture.View);
            texts.ShouldContain(KanbanCopy.ErrorHeading, StringComparer.Ordinal);
            texts.ShouldContain("The registry is offline", StringComparer.Ordinal);
            var retry = fixture.View.Retry;
            retry.IsEffectivelyVisible.ShouldBeTrue();
            retry.MinHeight.ShouldBeGreaterThanOrEqualTo(44);
            AutomationProperties.GetName(retry).ShouldBe(KanbanCopy.RetryName);
            ViewHarness.Unnamed(fixture.View).ShouldBeEmpty();
            ViewHarness.TabOrder(fixture.View).ShouldBe([KanbanCopy.RetryName]);

            fixture.Fake.Failure = null;
            ViewHarness.Click(fixture.Window, retry);

            fixture.Board.State.ShouldBe(BoardState.Ready);
            ViewHarness.VisibleTexts(fixture.View).ShouldNotContain(KanbanCopy.ErrorHeading, StringComparer.Ordinal);
            fixture.View.CardViewOf("run-1").ShouldNotBeNull().IsEffectivelyVisible.ShouldBeTrue();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A failure after cards exist keeps the cards on screen and adds the banner with the reason and its own Retry.</summary>
    [Fact]
    public async Task AFailureWithCardsShowsABannerAndKeepsTheCards()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            using var fixture = KanbanFixture.Open();
            fixture.View.Banner.IsVisible.ShouldBeFalse();

            fixture.Fake.Failure = new InvalidOperationException("The registry went away");
            fixture.Catalog.Refresh().ShouldBeFalse();
            fixture.Settle();

            fixture.View.Banner.IsEffectivelyVisible.ShouldBeTrue();
            ViewHarness.VisibleTexts(fixture.View).ShouldContain(KanbanCopy.BannerLead + " The registry went away", StringComparer.Ordinal);
            fixture.View.CardViewOf("run-1").ShouldNotBeNull().IsEffectivelyVisible.ShouldBeTrue();
            AutomationProperties.GetName(fixture.View.BannerRetry).ShouldBe(KanbanCopy.BannerRetryName);
            AutomationProperties.GetLiveSetting(fixture.View.Banner).ShouldBe(AutomationLiveSetting.Assertive);
            ViewHarness.Unnamed(fixture.View).ShouldBeEmpty();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The populated board draws every column, named with its title and count, at least 240 wide, in a horizontal scroller.</summary>
    [Fact]
    public async Task ReadyDrawsEveryColumnWithItsNameAndCount()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            using var fixture = KanbanFixture.Open();

            foreach (var key in ColumnKeys)
            {
                var column = fixture.Board.Columns.Single(candidate => string.Equals(candidate.Key, key, StringComparison.Ordinal));
                var frame = fixture.View.ColumnOf(key).ShouldNotBeNull();
                AutomationProperties.GetName(frame).ShouldBe(column.AccessibleName);
                fixture.View.ColumnHeaderOf(key)!.Text.ShouldBe($"{column.Title}  {column.Count}");
                frame.MinWidth.ShouldBeGreaterThanOrEqualTo(240);
            }

            AutomationProperties.GetName(fixture.View.ColumnOf("running")!).ShouldBe("Running, 2 tasks");
            AutomationProperties.GetName(fixture.View.ColumnOf("completed")!).ShouldBe("Completed, 1 task");
            AutomationProperties.GetName(fixture.View.ColumnOf("failed")!).ShouldBe("Failed, 0 tasks");
            ViewHarness.VisibleTexts(fixture.View).ShouldContain(KanbanCopy.EmptyColumn, StringComparer.Ordinal);
            ViewHarness.VisibleTexts(fixture.View).ShouldContain(KanbanCopy.DropHint, StringComparer.Ordinal);
            var scroller = fixture.View.GetVisualDescendants().OfType<ScrollViewer>().First();
            scroller.HorizontalScrollBarVisibility.ShouldBe(Avalonia.Controls.Primitives.ScrollBarVisibility.Auto);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The accessibility walk finds no unnamed tab stop or live region in any state.</summary>
    [Fact]
    public async Task NoStateHasAnUnnamedTabStop()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            using var ready = KanbanFixture.Open();
            ViewHarness.Unnamed(ready.View).ShouldBeEmpty();
            using var loading = KanbanFixture.Open(startLoading: true);
            ViewHarness.Unnamed(loading.View).ShouldBeEmpty();
            using var empty = KanbanFixture.Open((_, _) => { });
            ViewHarness.Unnamed(empty.View).ShouldBeEmpty();

            AutomationProperties.GetLiveSetting(ready.View.Live).ShouldBe(AutomationLiveSetting.Polite);
            AutomationProperties.GetName(ready.View.Live).ShouldBe(KanbanCopy.LiveRegionName);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }
}
