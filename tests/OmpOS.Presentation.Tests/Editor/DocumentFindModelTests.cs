using OmpOS.Presentation.Editor;
using Shouldly;
using Xunit;

namespace OmpOS.Presentation.Tests;

/// <summary>Find over a document: the match count, moving between matches with wrap, case, and following edits (ADE-09).</summary>
public sealed class DocumentFindModelTests
{
    private const string Fixture = "cat dog\nCat cat\ndog";

    /// <summary>Typing a query reports the match count and selects the first match in document order.</summary>
    [Fact]
    public void Query_ReportsTheMatchCountAndSelectsTheFirstMatch()
    {
        using var find = new DocumentFindModel(new TextDocument(Fixture)) { Query = "cat" };

        find.MatchCount.ShouldBe(3);
        find.Matches.ShouldBe([new TextMatch(0, 0, 3), new TextMatch(1, 0, 3), new TextMatch(1, 4, 3)]);
        find.SelectedIndex.ShouldBe(0);
        find.SelectedMatch.ShouldBe(new TextMatch(0, 0, 3));
    }

    /// <summary>Next moves through the matches in order and wraps from the last to the first.</summary>
    [Fact]
    public void Next_MovesForwardAndWrapsFromTheLastToTheFirst()
    {
        using var find = new DocumentFindModel(new TextDocument(Fixture)) { Query = "cat" };

        find.Next();
        find.SelectedMatch.ShouldBe(new TextMatch(1, 0, 3));
        find.Next();
        find.SelectedMatch.ShouldBe(new TextMatch(1, 4, 3));
        find.Next();
        find.SelectedIndex.ShouldBe(0);
    }

    /// <summary>Previous moves backward and wraps from the first to the last.</summary>
    [Fact]
    public void Previous_MovesBackwardAndWrapsFromTheFirstToTheLast()
    {
        using var find = new DocumentFindModel(new TextDocument(Fixture)) { Query = "cat" };

        find.Previous();
        find.SelectedMatch.ShouldBe(new TextMatch(1, 4, 3));
        find.Previous();
        find.SelectedMatch.ShouldBe(new TextMatch(1, 0, 3));
    }

    /// <summary>The case option decides whether "Cat" matches "cat", and changing it selects the first match again.</summary>
    [Fact]
    public void CaseSensitive_RestrictsTheMatchesAndSelectsTheFirst()
    {
        using var find = new DocumentFindModel(new TextDocument(Fixture)) { Query = "cat" };
        find.Next();

        find.CaseSensitive = true;

        find.Matches.ShouldBe([new TextMatch(0, 0, 3), new TextMatch(1, 4, 3)]);
        find.SelectedIndex.ShouldBe(0);
    }

    /// <summary>A query with no matches leaves nothing selected, and moving is a no-op.</summary>
    [Fact]
    public void NoMatches_SelectsNothingAndMovingDoesNothing()
    {
        using var find = new DocumentFindModel(new TextDocument(Fixture)) { Query = "zebra" };

        find.MatchCount.ShouldBe(0);
        find.SelectedIndex.ShouldBe(-1);
        find.SelectedMatch.ShouldBeNull();

        find.Next();
        find.Previous();

        find.SelectedIndex.ShouldBe(-1);
    }

    /// <summary>An empty query has no matches, so an empty search never selects anything.</summary>
    [Fact]
    public void EmptyQuery_HasNoMatches()
    {
        using var find = new DocumentFindModel(new TextDocument(Fixture));

        find.MatchCount.ShouldBe(0);
        find.SelectedIndex.ShouldBe(-1);
    }

    /// <summary>An edit that removes a match recomputes the matches and clamps the selection to the new count.</summary>
    [Fact]
    public void DocumentEdit_RecomputesMatchesAndClampsTheSelection()
    {
        var document = new TextDocument(Fixture);
        using var find = new DocumentFindModel(document) { Query = "cat" };
        find.Previous();
        find.SelectedIndex.ShouldBe(2);

        document.Replace(12, 3, string.Empty);

        find.MatchCount.ShouldBe(2);
        find.SelectedIndex.ShouldBe(1);
        find.SelectedMatch.ShouldBe(new TextMatch(1, 0, 3));
    }

    /// <summary>After disposal the find model no longer follows its document.</summary>
    [Fact]
    public void Dispose_StopsFollowingTheDocument()
    {
        var document = new TextDocument("cat");
        var find = new DocumentFindModel(document) { Query = "cat" };

        find.Dispose();
        document.Replace(0, 3, "dog");

        find.MatchCount.ShouldBe(1);
    }
}
