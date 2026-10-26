using OmpOS.Contracts.Streaming;
using OmpOS.Presentation.Chats;
using OmpOS.Presentation.State;
using Shouldly;
using Xunit;

namespace OmpOS.Presentation.Tests.Chats;

/// <summary>The chat scope of the transcript search model: switching scope, selection in a side chat, and live updates (U26-A).</summary>
public sealed class TranscriptSearchScopeTests
{
    private const string SessionId = "s-1";
    private const string CorrelationId = "s-1/turn-1";
    private static readonly DateTimeOffset Stamp = new(2026, 10, 22, 9, 0, 0, TimeSpan.Zero);

    private static SessionViewModel SessionWithAlpha()
    {
        var session = new SessionViewModel();
        session.ApplyEvent(Stream(new TextDeltaPayload { Delta = "Alpha in the transcript" }));
        session.ApplyEvent(Stream(new CompletedPayload { Reason = "end_turn" }));
        return session;
    }

    /// <summary>A search starts on the transcript alone and ignores side chats until the chat scope is chosen.</summary>
    [Fact]
    public void TheDefaultScopeSearchesTheTranscriptOnly()
    {
        var panel = new SideChatPanel(SessionId);
        panel.Open().Post(SideChatAuthor.User, "alpha in a side chat");
        using var search = new TranscriptSearchModel(SessionWithAlpha(), panel);

        search.SetQuery("alpha");

        search.Scope.ShouldBe(SearchScope.Transcript);
        search.HasSideChats.ShouldBeTrue();
        search.MatchCount.ShouldBe(1);
        search.SelectedMatch!.ChatId.ShouldBeNull();
    }

    /// <summary>Choosing the chat scope re-searches, selects the first match and raises MatchSelected once.</summary>
    [Fact]
    public void ChoosingTheChatScopeAddsSideChatMatchesAndSelectsTheFirst()
    {
        var panel = new SideChatPanel(SessionId);
        var chat = panel.Open();
        chat.Post(SideChatAuthor.User, "alpha in a side chat");
        using var search = new TranscriptSearchModel(SessionWithAlpha(), panel);
        search.SetQuery("alpha");
        search.Select(0);
        var raised = new List<TranscriptMatch>();
        search.MatchSelected += (_, args) => raised.Add(args.Match);

        search.SetScope(SearchScope.TranscriptAndSideChats);

        search.MatchCount.ShouldBe(2);
        search.SelectedIndex.ShouldBe(0);
        search.CountLabel.ShouldBe("1 of 2 matches");
        raised.ShouldBe([new TranscriptMatch(0, 0, 5)]);

        search.SetScope(SearchScope.Transcript);
        search.MatchCount.ShouldBe(1);
    }

    /// <summary>Selecting a side chat match exposes its chat and reports no transcript block to scroll to.</summary>
    [Fact]
    public void ASelectedSideChatMatchHasNoTranscriptBlock()
    {
        var panel = new SideChatPanel(SessionId);
        var chat = panel.Open();
        chat.Post(SideChatAuthor.User, "x");
        chat.Post(SideChatAuthor.User, "a alpha");
        using var search = new TranscriptSearchModel(SessionWithAlpha(), panel);
        search.SetScope(SearchScope.TranscriptAndSideChats);
        search.SetQuery("alpha");
        var raised = new List<TranscriptMatch>();
        search.MatchSelected += (_, args) => raised.Add(args.Match);

        search.Next();

        search.SelectedChatId.ShouldBe(chat.Id);
        search.SelectedBlockIndex.ShouldBe(-1);
        search.SelectedMatch.ShouldBe(new TranscriptMatch(1, 2, 5, chat.Id, chat.Title));
        raised.ShouldBe([new TranscriptMatch(1, 2, 5, chat.Id, chat.Title)]);

        search.Next();
        search.SelectedChatId.ShouldBeNull();
        search.SelectedBlockIndex.ShouldBe(0);
    }

    /// <summary>Posting to a side chat and opening a new one refresh the matches without moving the selection or raising MatchSelected.</summary>
    [Fact]
    public void SideChatChangesRefreshTheMatchesWithoutMovingTheSelection()
    {
        var panel = new SideChatPanel(SessionId);
        var first = panel.Open();
        using var search = new TranscriptSearchModel(SessionWithAlpha(), panel);
        search.SetScope(SearchScope.TranscriptAndSideChats);
        search.SetQuery("alpha");
        var raised = 0;
        search.MatchSelected += (_, _) => raised++;

        first.Post(SideChatAuthor.User, "alpha");
        search.MatchCount.ShouldBe(2);

        var second = panel.Open();
        second.Post(SideChatAuthor.Assistant, "alpha again");

        search.MatchCount.ShouldBe(3);
        search.SelectedIndex.ShouldBe(0);
        search.Matches.Select(static match => match.ChatId).ShouldBe([null, first.Id, second.Id]);
        raised.ShouldBe(0);
    }

    /// <summary>In the transcript scope, side chat changes are ignored.</summary>
    [Fact]
    public void SideChatChangesAreIgnoredInTheTranscriptScope()
    {
        var panel = new SideChatPanel(SessionId);
        var chat = panel.Open();
        using var search = new TranscriptSearchModel(SessionWithAlpha(), panel);
        search.SetQuery("alpha");

        chat.Post(SideChatAuthor.User, "alpha");

        search.MatchCount.ShouldBe(1);
    }

    /// <summary>A search without side chats treats the chat scope as the transcript alone.</summary>
    [Fact]
    public void WithoutSideChatsTheChatScopeSearchesTheTranscriptAlone()
    {
        using var search = new TranscriptSearchModel(SessionWithAlpha());

        search.SetScope(SearchScope.TranscriptAndSideChats);
        search.SetQuery("alpha");

        search.HasSideChats.ShouldBeFalse();
        search.MatchCount.ShouldBe(1);
    }

    /// <summary>Once disposed, the search no longer follows side chats, and an undefined scope is rejected.</summary>
    [Fact]
    public void DisposedSearchStopsFollowingSideChatsAndRejectsAnUndefinedScope()
    {
        var panel = new SideChatPanel(SessionId);
        var chat = panel.Open();
        var search = new TranscriptSearchModel(SessionWithAlpha(), panel);
        search.SetScope(SearchScope.TranscriptAndSideChats);
        search.SetQuery("alpha");

        Should.Throw<ArgumentOutOfRangeException>(() => search.SetScope((SearchScope)99));
        search.Dispose();
        chat.Post(SideChatAuthor.User, "alpha");

        search.MatchCount.ShouldBe(1);
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
