using Autonogrammer.Contracts.Streaming;
using Autonogrammer.Presentation.Chats;
using Autonogrammer.Presentation.State;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Chats;

/// <summary>The selection state over a session's transcript: wrapping next and previous, direct select, and live updates (ADE-04).</summary>
public sealed class TranscriptSearchModelTests
{
    private const string SessionId = "s-1";
    private const string CorrelationId = "s-1/turn-1";
    private static readonly DateTimeOffset Stamp = new(2026, 10, 22, 9, 0, 0, TimeSpan.Zero);

    /// <summary>Two blocks that contain "alpha": a user turn, then a completed markdown answer.</summary>
    private static SessionViewModel SessionWithTwoAlphas()
    {
        var session = new SessionViewModel();
        session.ApplyEvent(Stream(PromptAccepted("Find alpha")));
        session.ApplyEvent(Stream(new TextDeltaPayload { Delta = "Alpha beta" }));
        session.ApplyEvent(Stream(new CompletedPayload { Reason = "end_turn" }));
        return session;
    }

    /// <summary>A search reports the count, selects the first match, and exposes its block index.</summary>
    [Fact]
    public void SetQuerySelectsTheFirstMatchAndReportsTheCount()
    {
        var session = SessionWithTwoAlphas();
        using var search = new TranscriptSearchModel(session);

        search.SetQuery("alpha");

        search.MatchCount.ShouldBe(2);
        search.Matches.ShouldBe([new TranscriptMatch(0, 5, 5), new TranscriptMatch(1, 0, 5)]);
        search.SelectedIndex.ShouldBe(0);
        search.SelectedBlockIndex.ShouldBe(0);
        search.CountLabel.ShouldBe("1 of 2 matches");
    }

    /// <summary>Next moves forward and wraps from the last match to the first.</summary>
    [Fact]
    public void NextWrapsFromTheLastMatchToTheFirst()
    {
        using var search = new TranscriptSearchModel(SessionWithTwoAlphas());
        search.SetQuery("alpha");

        search.Next();
        search.SelectedIndex.ShouldBe(1);
        search.SelectedBlockIndex.ShouldBe(1);

        search.Next();
        search.SelectedIndex.ShouldBe(0);
        search.SelectedBlockIndex.ShouldBe(0);
    }

    /// <summary>Previous moves back and wraps from the first match to the last.</summary>
    [Fact]
    public void PreviousWrapsFromTheFirstMatchToTheLast()
    {
        using var search = new TranscriptSearchModel(SessionWithTwoAlphas());
        search.SetQuery("alpha");

        search.Previous();

        search.SelectedIndex.ShouldBe(1);
        search.SelectedBlockIndex.ShouldBe(1);
        search.CountLabel.ShouldBe("2 of 2 matches");
    }

    /// <summary>Select jumps to the given match and rejects an index outside the matches.</summary>
    [Fact]
    public void SelectJumpsToAMatchAndRejectsAnOutOfRangeIndex()
    {
        using var search = new TranscriptSearchModel(SessionWithTwoAlphas());
        search.SetQuery("alpha");

        search.Select(1);

        search.SelectedIndex.ShouldBe(1);
        search.SelectedBlockIndex.ShouldBe(1);
        Should.Throw<ArgumentOutOfRangeException>(() => search.Select(2));
        Should.Throw<ArgumentOutOfRangeException>(() => search.Select(-1));
    }

    /// <summary>Every explicit selection raises MatchSelected with the selected match, even when the index does not change.</summary>
    [Fact]
    public void EverySelectionRaisesMatchSelectedWithTheMatch()
    {
        using var search = new TranscriptSearchModel(SessionWithTwoAlphas());
        var raised = new List<TranscriptMatch>();
        search.MatchSelected += (_, args) => raised.Add(args.Match);

        search.SetQuery("alpha");
        search.Next();
        search.Select(1);
        search.Select(1);

        raised.ShouldBe(
        [
            new TranscriptMatch(0, 5, 5),
            new TranscriptMatch(1, 0, 5),
            new TranscriptMatch(1, 0, 5),
            new TranscriptMatch(1, 0, 5),
        ]);
    }

    /// <summary>An empty query clears the matches; selection stays at -1 and navigation does nothing.</summary>
    [Fact]
    public void EmptyQueryHasNoSelectionAndNavigationDoesNothing()
    {
        using var search = new TranscriptSearchModel(SessionWithTwoAlphas());

        search.SetQuery(string.Empty);
        search.Next();
        search.Previous();

        search.MatchCount.ShouldBe(0);
        search.SelectedIndex.ShouldBe(-1);
        search.SelectedBlockIndex.ShouldBe(-1);
        search.CountLabel.ShouldBe(string.Empty);
    }

    /// <summary>A query with no match reports "No matches" and no selection.</summary>
    [Fact]
    public void NoMatchReportsNoMatches()
    {
        using var search = new TranscriptSearchModel(SessionWithTwoAlphas());

        search.SetQuery("gamma");

        search.MatchCount.ShouldBe(0);
        search.SelectedIndex.ShouldBe(-1);
        search.CountLabel.ShouldBe("No matches");
    }

    /// <summary>A streaming update under an active query re-searches without moving the selection or raising MatchSelected.</summary>
    [Fact]
    public void StreamingUpdateRefreshesMatchesWithoutMovingTheSelection()
    {
        var session = SessionWithTwoAlphas();
        using var search = new TranscriptSearchModel(session);
        search.SetQuery("alpha");
        search.Select(1);
        var raised = 0;
        search.MatchSelected += (_, _) => raised++;

        session.ApplyEvent(Stream(new TextDeltaPayload { Delta = " and alpha" }));

        search.MatchCount.ShouldBe(3);
        search.SelectedIndex.ShouldBe(1);
        raised.ShouldBe(0);
    }

    /// <summary>Once disposed, the search no longer follows the transcript.</summary>
    [Fact]
    public void DisposedSearchStopsFollowingTheTranscript()
    {
        var session = SessionWithTwoAlphas();
        var search = new TranscriptSearchModel(session);
        search.SetQuery("alpha");

        search.Dispose();
        session.ApplyEvent(Stream(new TextDeltaPayload { Delta = " alpha" }));

        search.MatchCount.ShouldBe(2);
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
        using var doc = System.Text.Json.JsonDocument.Parse($$"""{"kind":"prompt_accepted","prompt":"{{prompt}}"}""");
        return new UnknownPayload { OriginalKind = "prompt_accepted", Raw = doc.RootElement.Clone() };
    }
}
