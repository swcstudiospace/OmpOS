using OmpOS.Presentation.Mentions;
using Shouldly;
using Xunit;

namespace OmpOS.Presentation.Tests.Mentions;

/// <summary>Fuzzy scoring and ranking of mention names.</summary>
public sealed class MentionFuzzyTests
{
    /// <summary>A match in the file name outranks the same text in a folder name.</summary>
    [Fact]
    public void TheFileNameOutranksAFolderName() =>
        MentionFuzzy.Score("app", "src/App.cs").ShouldBeGreaterThan(MentionFuzzy.Score("app", "src/apple/readme.md"));

    /// <summary>A name that does not hold the query as a subsequence does not match, and an empty query scores zero.</summary>
    [Fact]
    public void NonMatchesAndEmptyQueries()
    {
        MentionFuzzy.Score("xyz", "abc").ShouldBe(-1);
        MentionFuzzy.Score(string.Empty, "anything").ShouldBe(0);
        MentionFuzzy.Score("zz", string.Empty).ShouldBe(-1);
    }

    /// <summary>The initials of a camel-case name match it, without regard to case.</summary>
    [Fact]
    public void InitialsMatchACamelCaseName() =>
        MentionFuzzy.Score("mcm", "MentionComposerModel.cs").ShouldBeGreaterThan(0);

    /// <summary>The ranking puts the exact name first, shorter before longer, and leaves out names that do not match.</summary>
    [Fact]
    public void RankingPutsTheExactNameFirst()
    {
        var ranked = MentionFuzzy.Rank(["a/readme.md", "readme.md", "docs/readme.txt"], "readme.md", static name => name, 10);

        ranked.Select(static pair => pair.Item).ShouldBe(["readme.md", "a/readme.md"]);
    }

    /// <summary>With an empty query the first items are kept in their given order, up to the limit.</summary>
    [Fact]
    public void AnEmptyQueryKeepsTheGivenOrderUpToTheLimit()
    {
        var ranked = MentionFuzzy.Rank(["c", "a", "b", "d"], string.Empty, static name => name, 3);

        ranked.Select(static pair => pair.Item).ShouldBe(["c", "a", "b"]);
    }

    /// <summary>Equal scores fall back to the ordinal name order, so the list is the same on every run.</summary>
    [Fact]
    public void EqualScoresFallBackToOrdinalOrder()
    {
        var ranked = MentionFuzzy.Rank(["b.cs", "a.cs"], "cs", static name => name, 10);

        ranked.Select(static pair => pair.Item).ShouldBe(["a.cs", "b.cs"]);
    }

    /// <summary>A scan stops at the cap, so a huge tree cannot stall the picker.</summary>
    [Fact]
    public void AScanStopsAtTheCap()
    {
        var scanned = 0;
        IEnumerable<string> Names()
        {
            for (var index = 0; index < MentionFuzzy.MaxScanned + 1000; index++)
            {
                scanned++;
                yield return "file" + index;
            }
        }

        MentionFuzzy.Rank(Names(), "zzz", static name => name, 5).ShouldBeEmpty();

        scanned.ShouldBeLessThanOrEqualTo(MentionFuzzy.MaxScanned + 1);
    }
}
