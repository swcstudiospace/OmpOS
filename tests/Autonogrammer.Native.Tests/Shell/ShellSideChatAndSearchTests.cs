using System.Text.Json;
using Autonogrammer.Contracts.Streaming;
using Autonogrammer.Presentation.Chats;
using Autonogrammer.Presentation.State;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Shell;

/// <summary>
/// The side-chat region and the transcript search as the shell composes them: a side chat leaves the session's transcript as it
/// was, and a search match scrolls the transcript so the matched block is in view.
/// </summary>
public sealed class ShellSideChatAndSearchTests
{
    private const string SessionId = "shell-session";
    private static readonly DateTimeOffset Stamp = new(2026, 10, 22, 9, 0, 0, TimeSpan.Zero);

    /// <summary>Opening a side chat from the shell adds no block to the main transcript, and the side chat's messages never reach it.</summary>
    [Fact]
    public async Task OpeningASideChatLeavesTheMainTranscriptAlone()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var window = ShellHarness.Show();
            var main = ShellHarness.MainViewOf(window);
            var transcript = ShellHarness.SessionOf(window);
            var feed = new Feed(transcript);
            feed.Turn(1, "Find alpha", "Alpha beta");
            ShellHarness.Settle(window);
            var blocksBefore = transcript.Tree.Blocks.ToArray();
            var drawnBefore = BlockControls(window).Count;

            ShellHarness.ShowToolsView(window);
            main.FindControl<ToggleButton>("SideChatsToggle")!.IsChecked = true;
            ShellHarness.Settle(window);
            SideChatHost(window).IsVisible.ShouldBeTrue();

            OpenSideChatButton(window).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var sideChats = main.SideChats.ShouldNotBeNull();
            sideChats.Chats.Count.ShouldBe(1);
            sideChats.Selected!.Post(SideChatAuthor.User, "a side question");
            ShellHarness.Settle(window);

            transcript.Tree.Blocks.ShouldBe(blocksBefore);
            BlockControls(window).Count.ShouldBe(drawnBefore);
            sideChats.Selected!.Messages.Count.ShouldBe(1);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A match selected in a long transcript scrolls the transcript so the matched block is inside the viewport.</summary>
    [Fact]
    public async Task SelectingAMatchBringsItsBlockIntoTheScrollViewport()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var window = ShellHarness.Show();
            var main = ShellHarness.MainViewOf(window);
            var feed = new Feed(ShellHarness.SessionOf(window));
            for (var turn = 0; turn < 30; turn++)
            {
                feed.Turn(turn, $"Question {turn}", turn == 2 ? "Answer 2 holds the needle" : $"Answer {turn} plain text");
            }
            ShellHarness.Settle(window);

            var scroll = ShellHarness.Scroll(window);
            var target = BlockControls(window)[5];
            scroll.Extent.Height.ShouldBeGreaterThan(scroll.Viewport.Height, "the fixture must overflow the viewport");
            scroll.Offset = new Vector(0, scroll.Extent.Height - scroll.Viewport.Height);
            ShellHarness.Settle(window);
            var offsetAtBottom = scroll.Offset.Y;
            InViewport(target, scroll).ShouldBeFalse("the matched block starts above the visible part");

            OpenSearch(window);
            SearchInput(window).Text = "needle";
            ShellHarness.Settle(window);

            main.Search.ShouldNotBeNull().SelectedBlockIndex.ShouldBe(5);
            scroll.Offset.Y.ShouldBeLessThan(offsetAtBottom, "the transcript scrolled toward the match");
            InViewport(target, scroll).ShouldBeTrue("the matched block is inside the scroll viewport");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A match in the streaming tail names no block control, so selecting it leaves the scroll position alone.</summary>
    [Fact]
    public async Task SelectingAStreamingTailMatchDoesNotScroll()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var window = ShellHarness.Show();
            var main = ShellHarness.MainViewOf(window);
            var transcript = ShellHarness.SessionOf(window);
            var feed = new Feed(transcript);
            for (var turn = 0; turn < 30; turn++)
            {
                feed.Turn(turn, $"Question {turn}", $"Answer {turn} plain text");
            }
            feed.Prompt(30, "Question 30");
            feed.Delta(30, "Streaming tailword continues");
            ShellHarness.Settle(window);

            var scroll = ShellHarness.Scroll(window);
            scroll.Offset = new Vector(0, 0);
            ShellHarness.Settle(window);

            OpenSearch(window);
            SearchInput(window).Text = "tailword";
            ShellHarness.Settle(window);

            main.Search.ShouldNotBeNull().SelectedBlockIndex.ShouldBe(transcript.Tree.Blocks.Count);
            scroll.Offset.Y.ShouldBe(0);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Closing the window disposes its transcript search: it stops following the session's transcript.</summary>
    [Fact]
    public async Task ClosingTheWindowStopsTheTranscriptSearch()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var window = ShellHarness.Show();
            var main = ShellHarness.MainViewOf(window);
            var feed = new Feed(ShellHarness.SessionOf(window));
            feed.Turn(0, "Question 0", "Answer with needle");
            ShellHarness.Settle(window);
            OpenSearch(window);
            SearchInput(window).Text = "needle";
            var search = main.Search.ShouldNotBeNull();
            search.MatchCount.ShouldBe(1);

            window.Close();
            feed.Turn(1, "Question 1", "Another needle");

            search.MatchCount.ShouldBe(1, "a disposed search does not follow the transcript");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Opens the transcript search the way the keyboard does: Ctrl+F. The search is hidden until then.</summary>
    private static void OpenSearch(MainWindow window)
    {
        ShellHarness.Press(window, Key.F, PhysicalKey.F, control: true);
        ShellHarness.MainViewOf(window).FindControl<ContentControl>("SearchHost")!.IsVisible.ShouldBeTrue("Ctrl+F opens the transcript search");
    }

    private static TextBox SearchInput(MainWindow window) => Host(window, "SearchHost").FindControl<TextBox>("SearchInput")!;

    private static Control Host(MainWindow window, string name) => (Control)ShellHarness.MainViewOf(window).FindControl<ContentControl>(name)!.Content!;

    private static ContentControl SideChatHost(MainWindow window) => ShellHarness.MainViewOf(window).FindControl<ContentControl>("SideChatHost")!;

    private static Button OpenSideChatButton(MainWindow window) => Host(window, "SideChatHost").FindControl<Button>("OpenSideChatButton")!;

    /// <summary>The transcript's drawn block controls, in transcript order.</summary>
    private static List<Control> BlockControls(MainWindow window) =>
        ShellHarness.Transcript(window).FindControl<StackPanel>("BlockHost")!.Children.OfType<Control>().ToList();

    /// <summary>Whether a block lies entirely inside the visible part of the scroll viewport.</summary>
    private static bool InViewport(Control block, ScrollViewer scroll)
    {
        var top = block.TranslatePoint(new Point(0, 0), scroll) ?? throw new InvalidOperationException("The block is not in the scroll viewer.");
        return top.Y >= 0 && top.Y + block.Bounds.Height <= scroll.Bounds.Height;
    }

    /// <summary>Feeds stream events into the session the way the host does, with monotonic sequence numbers.</summary>
    private sealed class Feed(SessionViewModel session)
    {
        private long _seq;

        /// <summary>Adds a prompt and a completed answer.</summary>
        public void Turn(int turn, string prompt, string answer)
        {
            Prompt(turn, prompt);
            Delta(turn, answer);
            Apply(turn, new CompletedPayload { Reason = "end_turn" });
        }

        /// <summary>Adds the user's prompt for one turn.</summary>
        public void Prompt(int turn, string prompt)
        {
            using var doc = JsonDocument.Parse($$"""{"kind":"prompt_accepted","prompt":"{{prompt}}"}""");
            Apply(turn, new UnknownPayload { OriginalKind = "prompt_accepted", Raw = doc.RootElement.Clone() });
        }

        /// <summary>Streams text into one turn's answer without completing it.</summary>
        public void Delta(int turn, string text) => Apply(turn, new TextDeltaPayload { Delta = text });

        private void Apply(int turn, StreamEventPayload payload) => session.ApplyEvent(new StreamEvent
        {
            Seq = ++_seq,
            SessionId = SessionId,
            CorrelationId = $"{SessionId}/turn-{turn}",
            Timestamp = Stamp,
            Payload = payload,
        });
    }
}
