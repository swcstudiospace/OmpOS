using Autonogrammer.Native.Tests.Settings;
using Autonogrammer.Native.Views.Hooks;
using Autonogrammer.Presentation.Hooks;
using Autonogrammer.Presentation.Tests.Hooks;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Threading;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Hooks;

/// <summary>The run log view and the drawer around it, and the notice rows a thread shows.</summary>
public sealed class HookLogAndNoticeViewTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static Window Show(Control view)
    {
        var window = new Window { Width = 800, Height = 900, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    /// <summary>The log lists the newest run first with its outcome, shows redacted output only, filters by outcome, and every control is named.</summary>
    [Fact]
    public async Task TheLogShowsRedactedRowsNewestFirstAndFilters()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(() =>
        {
            var model = new HookRunLogModel(TimeZoneInfo.Utc);
            model.Append(HookItems.Run("first", stdout: "ok"));
            model.Append(HookItems.Run("second", HookRunOutcome.TimedOut, 30000, "token=abcdefghijklmnop leaked"));
            var view = new HookRunLogView { DataContext = model };
            var window = Show(view);

            var texts = SettingsTree.Texts(view);
            texts.ToList().IndexOf("second").ShouldBeLessThan(texts.ToList().IndexOf("first"));
            texts.ShouldContain(HooksCopy.OutcomeText(HookRunOutcome.TimedOut), StringComparer.Ordinal);
            SettingsTree.Everything(view).Any(static s => s.Contains("abcdefghijklmnop", StringComparison.Ordinal)).ShouldBeFalse();
            AutomationProperties.GetName(view.Copy).ShouldBe(HooksCopy.CopyLogLabel);
            AutomationProperties.GetName(view.Filter).ShouldBe(HooksCopy.RunLogFilterLabel);
            SettingsTree.Unnamed(view).ShouldBeEmpty();

            model.SetFilter(HookRunOutcome.Succeeded);
            Dispatcher.UIThread.RunJobs();
            view.Rows.ItemCount.ShouldBe(1);
            window.Close();
            return Task.CompletedTask;
        }, Token).ConfigureAwait(true);
    }

    /// <summary>An empty log says so, and the drawer raises its close request from a named button.</summary>
    [Fact]
    public async Task TheDrawerClosesFromItsButtonAndAnEmptyLogSaysSo()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(() =>
        {
            var model = new HookRunLogModel(TimeZoneInfo.Utc);
            var drawer = new HookRunLogDrawerView { DataContext = model };
            var window = Show(drawer);
            var closed = 0;
            drawer.CloseRequested += (_, _) => closed++;

            drawer.LogView.Empty.IsVisible.ShouldBeTrue();
            drawer.LogView.Empty.Text.ShouldBe(HooksCopy.RunLogEmpty);
            AutomationProperties.GetName(drawer.Close).ShouldBe(HooksCopy.CloseLogLabel);
            SettingsTree.Click(window, drawer.Close);

            closed.ShouldBe(1);
            window.Close();
            return Task.CompletedTask;
        }, Token).ConfigureAwait(true);
    }

    /// <summary>A denial is one line; a context row is collapsed to its summary and expands to the injected text; a consent notice offers a named Review button that raises its request.</summary>
    [Fact]
    public async Task NoticeRowsShowTheirTextAndOfferReviewOnlyWhenNeeded()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(() =>
        {
            var denied = new HookNoticeRowView { DataContext = HookNoticeRow.From(new HookNoticeEntry(HookNoticeType.Denied, "guard", "c1", "not today")) };
            var deniedWindow = Show(denied);
            denied.Line.IsVisible.ShouldBeTrue();
            denied.Line.Text.ShouldBe("denied by hook guard: not today");
            denied.Context.IsVisible.ShouldBeFalse();
            denied.Review.IsVisible.ShouldBeFalse();
            deniedWindow.Close();

            var context = new HookNoticeRowView { DataContext = HookNoticeRow.From(new HookNoticeEntry(HookNoticeType.Context, "rules", null, "Use tabs.")) };
            var contextWindow = Show(context);
            context.Context.IsVisible.ShouldBeTrue();
            context.Context.IsExpanded.ShouldBeFalse();
            context.Line.IsVisible.ShouldBeFalse();
            SettingsTree.Everything(context).ShouldNotContain("Use tabs.", StringComparer.Ordinal);
            context.Context.IsExpanded = true;
            Dispatcher.UIThread.RunJobs();
            SettingsTree.Everything(context).ShouldContain("Use tabs.", StringComparer.Ordinal);
            contextWindow.Close();

            var consent = new HookNoticeRowView { DataContext = HookNoticeRow.From(new HookNoticeEntry(HookNoticeType.ConsentRequired, "guard", "c2", string.Empty)) };
            var consentWindow = Show(consent);
            var reviews = 0;
            consent.ReviewRequested += (_, _) => reviews++;
            consent.Review.IsVisible.ShouldBeTrue();
            AutomationProperties.GetName(consent.Review).ShouldBe(HooksCopy.ReviewLabel);
            SettingsTree.Click(consentWindow, consent.Review);
            reviews.ShouldBe(1);
            consentWindow.Close();
            return Task.CompletedTask;
        }, Token).ConfigureAwait(true);
    }
}
