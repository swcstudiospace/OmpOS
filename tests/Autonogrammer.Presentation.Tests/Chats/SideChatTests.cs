using Autonogrammer.Contracts.Streaming;
using Autonogrammer.Presentation.Chats;
using Autonogrammer.Presentation.State;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Chats;

/// <summary>Side chats: opening one never touches the parent transcript, and their messages stay in their own list (ADE-04).</summary>
public sealed class SideChatTests
{
    private const string SessionId = "s-1";
    private const string CorrelationId = "s-1/turn-1";
    private const string SideOnlyMarker = "side-only-marker";
    private static readonly DateTimeOffset Stamp = new(2026, 10, 22, 9, 0, 0, TimeSpan.Zero);

    /// <summary>A session with a completed answer, so the parent transcript has content to protect.</summary>
    private static SessionViewModel SessionWithAnswer()
    {
        var session = new SessionViewModel();
        session.ApplyEvent(Stream(new TextDeltaPayload { Delta = "The main answer." }));
        session.ApplyEvent(Stream(new CompletedPayload { Reason = "end_turn" }));
        return session;
    }

    /// <summary>Opening a side chat leaves the parent transcript the same instance with the same blocks.</summary>
    [Fact]
    public void OpeningASideChatLeavesTheParentTranscriptUnchanged()
    {
        var session = SessionWithAnswer();
        var before = session.Tree;
        var panel = new SideChatPanel(SessionId);

        panel.Open();

        session.Tree.ShouldBeSameAs(before);
        session.Tree.Blocks.Count.ShouldBe(1);
    }

    /// <summary>Messages posted to a side chat do not appear in the parent transcript or its search.</summary>
    [Fact]
    public void SideChatMessagesNeverAppearInTheParentTranscript()
    {
        var session = SessionWithAnswer();
        var before = session.Tree.Blocks.Count;
        var panel = new SideChatPanel(SessionId);
        var chat = panel.Open();

        chat.Post(SideChatAuthor.User, SideOnlyMarker);
        chat.Post(SideChatAuthor.Assistant, $"Reply mentioning {SideOnlyMarker}");

        session.Tree.Blocks.Count.ShouldBe(before);
        TranscriptSearch.Find(session.Tree, SideOnlyMarker).ShouldBeEmpty();
        chat.Messages.Select(static message => message.Text).ShouldBe([SideOnlyMarker, $"Reply mentioning {SideOnlyMarker}"]);
    }

    /// <summary>The side chat keeps its messages in order and names the session it was opened from.</summary>
    [Fact]
    public void SideChatKeepsItsOwnOrderedMessagesAndParent()
    {
        var chat = new SideChatPanel(SessionId).Open();

        chat.Post(SideChatAuthor.User, "first");
        chat.Post(SideChatAuthor.Assistant, "second");

        chat.ParentSessionId.ShouldBe(SessionId);
        chat.Messages.ShouldBe(
        [
            new SideChatMessage { Author = SideChatAuthor.User, Text = "first" },
            new SideChatMessage { Author = SideChatAuthor.Assistant, Text = "second" },
        ]);
    }

    /// <summary>Posting a message announces the Messages change to a bound view.</summary>
    [Fact]
    public void PostingAMessageAnnouncesMessages()
    {
        var chat = new SideChatPanel(SessionId).Open();
        var changed = PropertyChangeRecorder.Record(chat);

        chat.Post(SideChatAuthor.User, "hello");

        changed.ShouldAnnounce(nameof(SideChat.Messages));
    }

    /// <summary>Each open creates a new side chat, selects it, and keeps the earlier ones in order.</summary>
    [Fact]
    public void OpeningAgainCreatesANewSelectedSideChat()
    {
        var panel = new SideChatPanel(SessionId);

        var first = panel.Open();
        var second = panel.Open();

        panel.Chats.ShouldBe([first, second]);
        panel.Selected.ShouldBeSameAs(second);
        second.Title.ShouldBe("Side chat 2");
        second.ParentSessionId.ShouldBe(SessionId);
    }

    /// <summary>Selecting an earlier side chat moves the selection to it.</summary>
    [Fact]
    public void SelectMovesTheSelectionWithinThePanel()
    {
        var panel = new SideChatPanel(SessionId);
        var first = panel.Open();
        panel.Open();

        panel.Select(first);

        panel.Selected.ShouldBeSameAs(first);
    }

    /// <summary>A side chat from another session cannot be selected in this panel.</summary>
    [Fact]
    public void SelectRejectsASideChatFromAnotherPanel()
    {
        var panel = new SideChatPanel(SessionId);
        panel.Open();
        var foreign = new SideChatPanel("s-2").Open();

        Should.Throw<ArgumentException>(() => panel.Select(foreign));
    }

    /// <summary>A blank message is rejected, so a side chat never holds an empty turn.</summary>
    [Fact]
    public void BlankMessageIsRejected()
    {
        var chat = new SideChatPanel(SessionId).Open();

        Should.Throw<ArgumentException>(() => chat.Post(SideChatAuthor.User, "   "));
        chat.Messages.ShouldBeEmpty();
    }

    private static StreamEvent Stream(StreamEventPayload payload) => new()
    {
        Seq = 1,
        SessionId = SessionId,
        CorrelationId = CorrelationId,
        Timestamp = Stamp,
        Payload = payload,
    };
}
