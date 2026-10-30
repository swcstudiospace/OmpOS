using Autonogrammer.Presentation.Mentions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Mentions;

/// <summary>Finding mentions in composer text: the send path (complete mentions) and the picker path (the token under the caret).</summary>
public sealed class MentionParserTests
{
    private static string Raw(string text) => string.Join('|', MentionParser.Parse(text).Select(token => text[token.Start..token.End]));

    /// <summary>A mention opens at a word boundary; trailing sentence punctuation is not part of the name; a hash needs digits.</summary>
    [Theory]
    [InlineData("look at @a.cs and @b/", "@a.cs|@b/")]
    [InlineData("mail me at a@b.com", "")]
    [InlineData("fix #123 and (#45)", "#123|#45")]
    [InlineData("# heading", "")]
    [InlineData("#abc", "")]
    [InlineData("see @\"my notes.md\" ok", "@\"my notes.md\"")]
    [InlineData("end of sentence @file.", "@file")]
    [InlineData("@\"unclosed name", "")]
    [InlineData("@\"\"", "")]
    [InlineData("@", "")]
    [InlineData("@a @b", "@a|@b")]
    [InlineData("(@a.cs)", "@a.cs")]
    [InlineData("page#top and @x", "@x")]
    [InlineData("", "")]
    public void ParseFindsTheCompleteMentions(string text, string expected) => Raw(text).ShouldBe(expected);

    /// <summary>A quoted name keeps its spaces and loses its quotes in the query.</summary>
    [Fact]
    public void AQuotedNameKeepsItsSpaces()
    {
        var token = MentionParser.Parse("see @\"my notes.md\" ok").ShouldHaveSingleItem();

        token.Query.ShouldBe("my notes.md");
        token.IsQuoted.ShouldBeTrue();
        token.IsClosed.ShouldBeTrue();
        token.Trigger.ShouldBe(MentionTrigger.At);
    }

    /// <summary>A name that ends with a slash asks for the contents of a folder.</summary>
    [Fact]
    public void ASlashAtTheEndMarksAFolderQuery()
    {
        MentionParser.Parse("@src/").ShouldHaveSingleItem().IsFolderQuery.ShouldBeTrue();
        MentionParser.Parse("@src").ShouldHaveSingleItem().IsFolderQuery.ShouldBeFalse();
    }

    /// <summary>The picker path returns the token under the caret, the whole word, whatever side of the caret the rest is on.</summary>
    [Theory]
    [InlineData("see @src/Ap", 11, "src/Ap")]
    [InlineData("see @src/Ap tail", 7, "src/Ap")]
    [InlineData("see @src/Ap tail", 12, null)]
    [InlineData("see @", 5, "")]
    [InlineData("a@b", 3, null)]
    [InlineData("hello", 5, null)]
    [InlineData("@x", 0, null)]
    [InlineData("@\"my no", 7, "my no")]
    [InlineData("@\"my notes.md\" more", 14, "my notes.md")]
    [InlineData("@\"my notes.md\" more", 15, null)]
    [InlineData("fix #", 5, "")]
    [InlineData("fix #12", 7, "12")]
    [InlineData("fix #pull request", 17, null)]
    public void FindActiveReturnsTheTokenUnderTheCaret(string text, int caret, string? expectedQuery)
    {
        var token = MentionParser.FindActive(text, caret);

        if (expectedQuery is null)
        {
            token.ShouldBeNull();
        }
        else
        {
            token.ShouldNotBeNull().Query.ShouldBe(expectedQuery);
        }
    }

    /// <summary>A hash token is the pull request trigger; a caret beyond the text is clamped.</summary>
    [Fact]
    public void AHashIsThePullRequestTriggerAndTheCaretIsClamped()
    {
        MentionParser.FindActive("see #4", 6)!.Trigger.ShouldBe(MentionTrigger.Hash);
        MentionParser.FindActive("see @a", 999)!.Query.ShouldBe("a");
    }

    /// <summary>Format writes a name as the composer will show it: quoted when it holds a space or ends in punctuation that parsing would trim.</summary>
    [Theory]
    [InlineData(MentionTrigger.At, "src/App.cs", "@src/App.cs")]
    [InlineData(MentionTrigger.At, "my notes.md", "@\"my notes.md\"")]
    [InlineData(MentionTrigger.At, "foo(1)", "@\"foo(1)\"")]
    [InlineData(MentionTrigger.At, "src/", "@src/")]
    [InlineData(MentionTrigger.Hash, "123", "#123")]
    public void FormatQuotesOnlyWhenParsingNeedsIt(MentionTrigger trigger, string name, string expected)
    {
        var text = MentionParser.Format(trigger, name);

        text.ShouldBe(expected);
        // What Format writes parses back to the same name.
        MentionParser.Parse(text).ShouldHaveSingleItem().Query.ShouldBe(name);
    }
}
