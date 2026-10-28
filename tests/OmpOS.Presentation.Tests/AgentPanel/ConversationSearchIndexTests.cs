using OmpOS.Presentation.AgentPanel;
using Shouldly;
using Xunit;

namespace OmpOS.Presentation.Tests.AgentPanel;

/// <summary>The bounded in-memory conversation index: matching, ranking, snippets and every bound.</summary>
public sealed class ConversationSearchIndexTests
{
    /// <summary>A title match ranks before a transcript match, a session with neither is absent, and the result names the session.</summary>
    [Fact]
    public void ATitleMatchRanksBeforeATranscriptMatch()
    {
        var index = new ConversationSearchIndex();
        index.Upsert("s1", "Alpha", "nothing to see");
        index.Upsert("s2", "Fix login", "the login bug");
        index.Upsert("s3", "Notes", "we discussed the login flow");

        var results = index.Search("login", 10);

        results.Select(static result => result.SessionId).ShouldBe(["s2", "s3"]);
        results[0].IsTitleMatch.ShouldBeTrue();
        results[1].IsTitleMatch.ShouldBeFalse();
        results[1].Snippet.ShouldContain("login flow", Case.Sensitive);
    }

    /// <summary>Every word of the query must be in the title or the transcript, compared without case.</summary>
    [Fact]
    public void EveryWordMustMatch()
    {
        var index = new ConversationSearchIndex();
        index.Upsert("s2", "Fix login", "the login bug");
        index.Upsert("s3", "Notes", "we discussed the LOGIN flow");

        index.Search("login flow", 10).Select(static result => result.SessionId).ShouldBe(["s3"]);
        index.Search("flow login", 10).Select(static result => result.SessionId).ShouldBe(["s3"]);
        index.Search("login zebra", 10).ShouldBeEmpty();
    }

    /// <summary>The snippet is one line around the first match, with ellipses where it was cut, and the match offsets point at the matched text.</summary>
    [Fact]
    public void TheSnippetIsOneLineAroundTheMatch()
    {
        var index = new ConversationSearchIndex(new ConversationSearchOptions { SnippetRadius = 10 });
        index.Upsert("s1", "Title", new string('x', 50) + "\n\n  Needle  " + new string('y', 50));

        var result = index.Search("needle", 10).Single();

        result.Snippet.ShouldStartWith("\u2026", Case.Sensitive);
        result.Snippet.ShouldEndWith("\u2026", Case.Sensitive);
        result.Snippet.ShouldNotContain('\n');
        result.Snippet.Substring(result.MatchStart, result.MatchLength).ShouldBe("Needle");
    }

    /// <summary>A match in the title alone has no snippet.</summary>
    [Fact]
    public void ATitleOnlyMatchHasNoSnippet()
    {
        var index = new ConversationSearchIndex();
        index.Upsert("s1", "Release plan", null);

        var result = index.Search("release", 10).Single();

        result.HasSnippet.ShouldBeFalse();
        result.MatchStart.ShouldBe(-1);
        result.AutomationName.ShouldBe("Release plan");
    }

    /// <summary>A session keeps only the end of a long transcript, the most recent part.</summary>
    [Fact]
    public void ALongTranscriptKeepsItsEnd()
    {
        var index = new ConversationSearchIndex(new ConversationSearchOptions { MaxCharsPerSession = 10 });

        index.Upsert("a", "t", "0123456789ABCDEFGHIJ");

        index.TotalChars.ShouldBe(10);
        index.Search("ABC", 10).ShouldHaveSingleItem();
        index.Search("012", 10).ShouldBeEmpty();
    }

    /// <summary>Past the session bound the least recently updated session goes.</summary>
    [Fact]
    public void PastTheSessionBoundTheOldestSessionGoes()
    {
        var index = new ConversationSearchIndex(new ConversationSearchOptions { MaxSessions = 2 });
        index.Upsert("a", "one", "x");
        index.Upsert("b", "two", "x");
        index.Upsert("c", "three", "x");

        index.SessionCount.ShouldBe(2);
        index.Search("one", 10).ShouldBeEmpty();
        index.Search("three", 10).ShouldHaveSingleItem();
    }

    /// <summary>Past the character bound the text of the oldest session goes first and its title stays searchable.</summary>
    [Fact]
    public void PastTheCharacterBoundTheOldestTextGoesAndTheTitleStays()
    {
        var index = new ConversationSearchIndex(new ConversationSearchOptions { MaxTotalChars = 20 });
        index.Upsert("a", "Alpha", "alphatext12");
        index.Upsert("b", "Beta", "betatext1234");

        index.TotalChars.ShouldBe(12);
        index.Search("alphatext", 10).ShouldBeEmpty();
        index.Search("alpha", 10).Single().SessionId.ShouldBe("a");
        index.Search("betatext", 10).Single().SessionId.ShouldBe("b");
        index.SessionCount.ShouldBe(2);
    }

    /// <summary>Updating only the title keeps the transcript and the session's place in the order of recency.</summary>
    [Fact]
    public void ATitleOnlyUpdateKeepsTheTextAndTheOrder()
    {
        var index = new ConversationSearchIndex();
        index.Upsert("a", "Old title", "shared words");
        index.Upsert("b", "Other", "shared words");

        index.Upsert("a", "New title", null);

        index.Search("shared", 10).Select(static result => result.SessionId).ShouldBe(["b", "a"]);
        index.Search("new", 10).Single().SessionId.ShouldBe("a");
        index.Search("old", 10).ShouldBeEmpty();
    }

    /// <summary>Equal matches list the most recently updated session first.</summary>
    [Fact]
    public void EqualMatchesListTheMostRecentFirst()
    {
        var index = new ConversationSearchIndex();
        index.Upsert("a", "Design review", null);
        index.Upsert("b", "Design review", null);

        index.Search("design", 10).Select(static result => result.SessionId).ShouldBe(["b", "a"]);
    }

    /// <summary>RetainOnly and Remove drop sessions and their characters from the count.</summary>
    [Fact]
    public void RemovingSessionsFreesTheirText()
    {
        var index = new ConversationSearchIndex();
        index.Upsert("a", "A", "12345");
        index.Upsert("b", "B", "123");
        index.Upsert("c", "C", "12");

        index.Remove("a").ShouldBeTrue();
        index.Remove("a").ShouldBeFalse();
        index.RetainOnly(new HashSet<string>(["b"], StringComparer.Ordinal));

        index.SessionCount.ShouldBe(1);
        index.TotalChars.ShouldBe(3);
    }

    /// <summary>Only the first words of a query count, a blank query matches nothing and a zero limit returns nothing.</summary>
    [Fact]
    public void QueryBounds()
    {
        var index = new ConversationSearchIndex();
        index.Upsert("a", "t", "a b c d e f g h");

        index.Search("a b c d e f g h zzzzzz", 10).ShouldHaveSingleItem();
        index.Search("   ", 10).ShouldBeEmpty();
        index.Search(string.Empty, 10).ShouldBeEmpty();
        index.Search("a", 0).ShouldBeEmpty();
        index.Search("a", 1).ShouldHaveSingleItem();
    }

    /// <summary>A blank session identifier is refused.</summary>
    [Fact]
    public void ABlankIdentifierIsRefused()
    {
        var index = new ConversationSearchIndex();

        Should.Throw<ArgumentException>(() => index.Upsert(" ", "t", "x"));
        Should.Throw<ArgumentNullException>(() => index.Upsert("a", null!, "x"));
    }
}
