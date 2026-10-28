using Autonogrammer.Contracts.Streaming;
using Autonogrammer.Presentation.Chats;
using Autonogrammer.Presentation.State;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Chats;

/// <summary>Search across the parent transcript and every side chat: order, naming of the chat, and what it never finds (U26-A).</summary>
public sealed class ChatSearchTests
{
    private const string SessionId = "s-1";
    private const string CorrelationId = "s-1/turn-1";
    private static readonly DateTimeOffset Stamp = new(2026, 10, 22, 9, 0, 0, TimeSpan.Zero);

    /// <summary>A session whose completed answer is block 0 and says "Alpha" once.</summary>
    private static SessionViewModel SessionWithAnswer(string answer)
    {
        var session = new SessionViewModel();
        session.ApplyEvent(Stream(new TextDeltaPayload { Delta = answer }));
        session.ApplyEvent(Stream(new CompletedPayload { Reason = "end_turn" }));
        return session;
    }

    /// <summary>The parent transcript's matches come first, then each side chat's, in the order the chats were opened.</summary>
    [Fact]
    public void SearchSpansTheParentTranscriptAndEverySideChat()
    {
        var session = SessionWithAnswer("Alpha in the main answer");
        var panel = new SideChatPanel(SessionId);
        var first = panel.Open();
        first.Post(SideChatAuthor.User, "no hit here");
        first.Post(SideChatAuthor.Assistant, "second message, ALPHA");
        var second = panel.Open();
        second.Post(SideChatAuthor.User, "alpha alpha");

        var matches = ChatSearch.Find(session.Tree, panel.Chats, "alpha");

        matches.ShouldBe(
        [
            new TranscriptMatch(0, 0, 5),
            new TranscriptMatch(1, 16, 5, first.Id, first.Title),
            new TranscriptMatch(0, 0, 5, second.Id, second.Title),
            new TranscriptMatch(0, 6, 5, second.Id, second.Title),
        ]);
    }

    /// <summary>A side chat's match carries that chat's identifier and title, and a transcript match carries neither.</summary>
    [Fact]
    public void ASideChatMatchNamesItsChat()
    {
        var session = SessionWithAnswer("Alpha in the main answer");
        var panel = new SideChatPanel(SessionId);
        var chat = panel.Open();
        chat.Post(SideChatAuthor.User, "a side-only-term");

        var sideMatch = ChatSearch.Find(session.Tree, panel.Chats, "side-only-term").ShouldHaveSingleItem();
        var transcriptMatch = ChatSearch.Find(session.Tree, panel.Chats, "alpha").ShouldHaveSingleItem();

        sideMatch.ChatId.ShouldBe(chat.Id);
        sideMatch.ChatTitle.ShouldBe("Side chat 1");
        sideMatch.LocationName.ShouldBe("Side chat 1");
        sideMatch.IsInSideChat.ShouldBeTrue();
        sideMatch.BlockIndex.ShouldBe(0);
        sideMatch.Offset.ShouldBe(2);
        sideMatch.Length.ShouldBe("side-only-term".Length);
        transcriptMatch.ChatId.ShouldBeNull();
        transcriptMatch.LocationName.ShouldBe("Transcript");
        transcriptMatch.IsInSideChat.ShouldBeFalse();
    }

    /// <summary>With two chats and a term that both hold, each chat contributes its own match and the transcript none.</summary>
    [Fact]
    public void TwoChatsAndAKnownTermReturnMatchesFromBoth()
    {
        var session = SessionWithAnswer("Nothing relevant");
        var panel = new SideChatPanel(SessionId);
        var one = panel.Open();
        one.Post(SideChatAuthor.User, "the quokka question");
        var two = panel.Open();
        two.Post(SideChatAuthor.Assistant, "about a Quokka");

        var matches = ChatSearch.Find(session.Tree, panel.Chats, "quokka");

        matches.Select(static match => match.ChatId).ShouldBe([one.Id, two.Id]);
        matches.Select(static match => match.LocationName).ShouldBe(["Side chat 1", "Side chat 2"]);
    }

    /// <summary>An empty query matches nothing in either place, and a chat with no messages contributes nothing.</summary>
    [Fact]
    public void EmptyQueryAndEmptyChatsMatchNothing()
    {
        var session = SessionWithAnswer("Alpha");
        var panel = new SideChatPanel(SessionId);
        panel.Open().Post(SideChatAuthor.User, "alpha");
        panel.Open();

        ChatSearch.Find(session.Tree, panel.Chats, string.Empty).ShouldBeEmpty();
        ChatSearch.Find(session.Tree, panel.Chats, "alpha").Count.ShouldBe(2);
        ChatSearch.FindInChat(panel.Chats[1], "alpha").ShouldBeEmpty();
    }

    /// <summary>Matches inside one message do not overlap, as in the transcript search.</summary>
    [Fact]
    public void MatchesInOneSideChatMessageDoNotOverlap()
    {
        var chat = new SideChatPanel(SessionId).Open();
        chat.Post(SideChatAuthor.User, "aaaa");

        ChatSearch.FindInChat(chat, "aa").Select(static match => match.Offset).ShouldBe([0, 2]);
    }

    /// <summary>Null arguments are rejected.</summary>
    [Fact]
    public void NullArgumentsAreRejected()
    {
        var session = SessionWithAnswer("Alpha");
        var chat = new SideChatPanel(SessionId).Open();

        Should.Throw<ArgumentNullException>(() => ChatSearch.Find(null!, [chat], "a"));
        Should.Throw<ArgumentNullException>(() => ChatSearch.Find(session.Tree, null!, "a"));
        Should.Throw<ArgumentNullException>(() => ChatSearch.Find(session.Tree, [chat], null!));
        Should.Throw<ArgumentNullException>(() => ChatSearch.FindInChat(null!, "a"));
        Should.Throw<ArgumentNullException>(() => ChatSearch.FindInChat(chat, null!));
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
