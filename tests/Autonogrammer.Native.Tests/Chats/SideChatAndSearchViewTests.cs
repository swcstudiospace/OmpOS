using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Autonogrammer.Contracts.Streaming;
using Autonogrammer.Native.Views;
using Autonogrammer.Native.Views.Chats;
using Autonogrammer.Presentation.Chats;
using Autonogrammer.Presentation.State;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Chats;

/// <summary>
/// Headless checks of the side-chat region and the transcript search bar (ADE-04): opening a side chat leaves the
/// session's transcript as it was, and the search reports the right match count and moves the selection.
/// </summary>
public sealed class SideChatAndSearchViewTests
{
    private const string SessionId = "s-1";
    private const string CorrelationId = "s-1/turn-1";
    private static readonly DateTimeOffset Stamp = new(2026, 10, 22, 9, 0, 0, TimeSpan.Zero);

    /// <summary>A session with a prompt and a completed answer: block 0 is the prompt, block 1 is the answer.</summary>
    private static SessionViewModel SessionWithPromptAndAnswer()
    {
        var session = new SessionViewModel();
        session.ApplyEvent(Stream(PromptAccepted("Find alpha")));
        session.ApplyEvent(Stream(new TextDeltaPayload { Delta = "Alpha beta ALPHA" }));
        session.ApplyEvent(Stream(new CompletedPayload { Reason = "end_turn" }));
        return session;
    }

    /// <summary>Opening a side chat from the session leaves the main transcript count, and what the session view draws, unchanged.</summary>
    [Fact]
    public async Task OpeningASideChatLeavesTheMainTranscriptCountUnchanged()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.Dispatch(() =>
        {
            var session = SessionWithPromptAndAnswer();
            var transcript = new SessionView { DataContext = session };
            var blocksBefore = session.Tree.Blocks.Count;
            var drawnBefore = transcript.FindControl<StackPanel>("BlockHost")!.Children.Count;

            var panel = new SideChatPanel(SessionId);
            var sideChats = new SideChatListView { DataContext = panel };
            sideChats.FindControl<Button>("OpenSideChatButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            panel.Chats.Count.ShouldBe(1);
            sideChats.FindControl<ListBox>("SideChatRows")!.ItemCount.ShouldBe(1);
            session.Tree.Blocks.Count.ShouldBe(blocksBefore);
            transcript.FindControl<StackPanel>("BlockHost")!.Children.Count.ShouldBe(drawnBefore);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Messages posted to the open side chat are drawn in its region and never in the session view.</summary>
    [Fact]
    public async Task SideChatMessagesAreDrawnOnlyInTheSideChatRegion()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.Dispatch(() =>
        {
            var session = SessionWithPromptAndAnswer();
            var transcript = new SessionView { DataContext = session };
            var blocksBefore = session.Tree.Blocks.Count;
            var drawnBefore = transcript.FindControl<StackPanel>("BlockHost")!.Children.Count;
            var panel = new SideChatPanel(SessionId);
            var sideChats = new SideChatListView { DataContext = panel };
            sideChats.FindControl<Button>("OpenSideChatButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            panel.Selected!.Post(SideChatAuthor.User, "a side question");

            sideChats.FindControl<StackPanel>("MessageHost")!.Children.Count.ShouldBe(1);
            session.Tree.Blocks.Count.ShouldBe(blocksBefore);
            transcript.FindControl<StackPanel>("BlockHost")!.Children.Count.ShouldBe(drawnBefore);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Typing a query shows the match count across the transcript, including the prompt and the answer.</summary>
    [Fact]
    public async Task SearchShowsTheCorrectMatchCount()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.Dispatch(() =>
        {
            var session = SessionWithPromptAndAnswer();
            var search = new TranscriptSearchModel(session);
            var view = new TranscriptSearchView { DataContext = search };

            view.FindControl<TextBox>("SearchInput")!.Text = "alpha";

            search.MatchCount.ShouldBe(3);
            view.FindControl<TextBlock>("SearchCount")!.Text.ShouldBe("1 of 3 matches");
            view.FindControl<Button>("NextButton")!.IsEnabled.ShouldBeTrue();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Next and Previous move the selected block index, wrapping at both ends.</summary>
    [Fact]
    public async Task NextAndPreviousExposeTheSelectedBlockIndex()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.Dispatch(() =>
        {
            var session = SessionWithPromptAndAnswer();
            var search = new TranscriptSearchModel(session);
            var view = new TranscriptSearchView { DataContext = search };
            view.FindControl<TextBox>("SearchInput")!.Text = "alpha";
            var next = view.FindControl<Button>("NextButton")!;
            var previous = view.FindControl<Button>("PreviousButton")!;

            search.SelectedBlockIndex.ShouldBe(0);

            next.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            search.SelectedBlockIndex.ShouldBe(1);
            view.FindControl<TextBlock>("SearchCount")!.Text.ShouldBe("2 of 3 matches");

            next.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            next.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            search.SelectedBlockIndex.ShouldBe(0);

            previous.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            search.SelectedBlockIndex.ShouldBe(1);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A query with no match reports that and disables Next and Previous.</summary>
    [Fact]
    public async Task NoMatchReportsNoMatchesAndDisablesNavigation()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.Dispatch(() =>
        {
            var session = SessionWithPromptAndAnswer();
            var search = new TranscriptSearchModel(session);
            var view = new TranscriptSearchView { DataContext = search };

            view.FindControl<TextBox>("SearchInput")!.Text = "gamma";

            view.FindControl<TextBlock>("SearchCount")!.Text.ShouldBe("No matches");
            view.FindControl<Button>("NextButton")!.IsEnabled.ShouldBeFalse();
            view.FindControl<Button>("PreviousButton")!.IsEnabled.ShouldBeFalse();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    private static StreamEvent Stream(StreamEventPayload payload) => new()
    {
        Seq = 1,
        SessionId = SessionId,
        CorrelationId = CorrelationId,
        Timestamp = Stamp,
        Payload = payload,
    };

    private static UnknownPayload PromptAccepted(string prompt)
    {
        using var doc = JsonDocument.Parse($$"""{"kind":"prompt_accepted","prompt":"{{prompt}}"}""");
        return new UnknownPayload { OriginalKind = "prompt_accepted", Raw = doc.RootElement.Clone() };
    }
}
