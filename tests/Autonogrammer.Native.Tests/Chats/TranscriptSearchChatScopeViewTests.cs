using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Autonogrammer.Contracts.Streaming;
using Autonogrammer.Native.Views.Chats;
using Autonogrammer.Presentation.Chats;
using Autonogrammer.Presentation.State;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Chats;

/// <summary>
/// Headless checks of the chat scope of the transcript search bar (U26-A2): the scope check box appears only with side chats,
/// every match shows the name of its chat, and picking a row selects that match on the model.
/// </summary>
public sealed class TranscriptSearchChatScopeViewTests
{
    private const string SessionId = "s-1";
    private const string CorrelationId = "s-1/turn-1";
    private static readonly DateTimeOffset Stamp = new(2026, 10, 22, 9, 0, 0, TimeSpan.Zero);

    /// <summary>A session with a prompt and a completed answer, each holding "alpha"; block 0 is the prompt.</summary>
    private static SessionViewModel SessionWithAlphas()
    {
        var session = new SessionViewModel();
        session.ApplyEvent(Stream(PromptAccepted("Find alpha")));
        session.ApplyEvent(Stream(new TextDeltaPayload { Delta = "Alpha beta" }));
        session.ApplyEvent(Stream(new CompletedPayload { Reason = "end_turn" }));
        return session;
    }

    private static SideChatPanel PanelWithOneAlphaInEachOfTwoChats()
    {
        var panel = new SideChatPanel(SessionId);
        panel.Open().Post(SideChatAuthor.User, "side alpha one");
        panel.Open().Post(SideChatAuthor.Assistant, "side ALPHA two");
        return panel;
    }

    /// <summary>A search without side chats offers no chat scope and shows no match list.</summary>
    [Fact]
    public async Task WithoutSideChatsTheScopeToggleAndRowsAreHidden()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.Dispatch(() =>
        {
            using var search = new TranscriptSearchModel(SessionWithAlphas());
            var view = new TranscriptSearchView { DataContext = search };

            view.FindControl<TextBox>("SearchInput")!.Text = "alpha";

            view.FindControl<CheckBox>("ChatScopeToggle")!.IsVisible.ShouldBeFalse();
            view.FindControl<ListBox>("MatchRows")!.IsVisible.ShouldBeFalse();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>With side chats the toggle is offered, and in the transcript scope the count and rows ignore the chats.</summary>
    [Fact]
    public async Task SideChatsOfferTheScopeToggleButStartInTheTranscriptScope()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.Dispatch(() =>
        {
            using var search = new TranscriptSearchModel(SessionWithAlphas(), PanelWithOneAlphaInEachOfTwoChats());
            var view = new TranscriptSearchView { DataContext = search };

            view.FindControl<TextBox>("SearchInput")!.Text = "alpha";

            var toggle = view.FindControl<CheckBox>("ChatScopeToggle")!;
            toggle.IsVisible.ShouldBeTrue();
            toggle.IsChecked.ShouldBe(false);
            search.MatchCount.ShouldBe(2);
            view.FindControl<ListBox>("MatchRows")!.IsVisible.ShouldBeFalse();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Checking the toggle searches the chats too and lists each match under the name of its chat.</summary>
    [Fact]
    public async Task CheckingTheToggleListsEveryMatchWithItsChatName()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.Dispatch(() =>
        {
            using var search = new TranscriptSearchModel(SessionWithAlphas(), PanelWithOneAlphaInEachOfTwoChats());
            var view = new TranscriptSearchView { DataContext = search };
            view.FindControl<TextBox>("SearchInput")!.Text = "alpha";

            view.FindControl<CheckBox>("ChatScopeToggle")!.IsChecked = true;

            search.Scope.ShouldBe(SearchScope.TranscriptAndSideChats);
            search.MatchCount.ShouldBe(4);
            var rows = view.FindControl<ListBox>("MatchRows")!;
            rows.IsVisible.ShouldBeTrue();
            rows.ItemCount.ShouldBe(4);
            rows.Items.Cast<string>().ShouldBe(
            [
                "Transcript - block 1",
                "Transcript - block 2",
                "Side chat 1 - message 1",
                "Side chat 2 - message 1",
            ]);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The selected match's chat is named, follows Next, and unchecking the toggle returns to the transcript scope.</summary>
    [Fact]
    public async Task TheSelectedMatchNamesItsChatAndFollowsNext()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.Dispatch(() =>
        {
            using var search = new TranscriptSearchModel(SessionWithAlphas(), PanelWithOneAlphaInEachOfTwoChats());
            var view = new TranscriptSearchView { DataContext = search };
            view.FindControl<TextBox>("SearchInput")!.Text = "alpha";
            var toggle = view.FindControl<CheckBox>("ChatScopeToggle")!;
            toggle.IsChecked = true;
            var location = view.FindControl<TextBlock>("SelectedLocation")!;
            var rows = view.FindControl<ListBox>("MatchRows")!;
            location.Text.ShouldBe("Transcript - block 1");

            var next = view.FindControl<Button>("NextButton")!;
            next.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            next.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            location.Text.ShouldBe("Side chat 1 - message 1");
            rows.SelectedIndex.ShouldBe(2);
            view.FindControl<TextBlock>("SearchCount")!.Text.ShouldBe("3 of 4 matches");

            toggle.IsChecked = false;

            search.Scope.ShouldBe(SearchScope.Transcript);
            rows.IsVisible.ShouldBeFalse();
            location.IsVisible.ShouldBeFalse();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Picking a row selects that match on the model, so the shell can open its side chat.</summary>
    [Fact]
    public async Task PickingARowSelectsThatMatchOnTheModel()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.Dispatch(() =>
        {
            var panel = PanelWithOneAlphaInEachOfTwoChats();
            using var search = new TranscriptSearchModel(SessionWithAlphas(), panel);
            var view = new TranscriptSearchView { DataContext = search };
            view.FindControl<TextBox>("SearchInput")!.Text = "alpha";
            view.FindControl<CheckBox>("ChatScopeToggle")!.IsChecked = true;
            var raised = new List<TranscriptMatch>();
            search.MatchSelected += (_, args) => raised.Add(args.Match);

            view.FindControl<ListBox>("MatchRows")!.SelectedIndex = 3;

            search.SelectedIndex.ShouldBe(3);
            search.SelectedChatId.ShouldBe(panel.Chats[1].Id);
            raised.ShouldHaveSingleItem().LocationName.ShouldBe("Side chat 2");
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
