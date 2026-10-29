using Autonogrammer.Native.Views;
using Autonogrammer.Presentation.State;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Blocks;

/// <summary>The empty transcript says "No messages yet." once, not once as the live announcement and once as the heading (Phase 50 foundation finding 6).</summary>
public sealed class SessionViewEmptyStateTests
{
    /// <summary>The announcement that repeats the heading stays a live region but draws nothing, so the sentence is on screen once.</summary>
    [Fact]
    public async Task TheEmptyTranscriptDrawsItsSentenceOnce()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var model = new SessionViewModel();
            model.MarkHistoryLoaded();
            var view = (SessionView)ViewMapping.Create(model);
            var window = new Window { Content = view, Width = 640, Height = 480 };
            window.Show();
            ShellHarness.Settle(window);

            var drawn = view.GetVisualDescendants()
                .OfType<TextBlock>()
                .Where(static block => block.IsEffectivelyVisible && block.Opacity > 0 && block.Bounds.Height > 0)
                .Select(static block => block.Text)
                .Where(static text => string.Equals(text, "No messages yet.", StringComparison.Ordinal))
                .ToList();

            drawn.Count.ShouldBe(1);
            view.FindControl<TextBlock>("AnnouncementText")!.Text.ShouldBe("No messages yet.", "the live region still carries the sentence");
            window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }
}
