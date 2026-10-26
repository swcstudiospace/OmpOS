using OmpOS.Harness.AgentLoop.Substrate;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.AgentLoop.Tests.Substrate;

/// <summary>Plain-text safety of the cleaner every Substrate note, brief source and stage field passes through.</summary>
public sealed class SubstrateTextTests
{
    [Theory]
    [InlineData("a\r\nb\tc", 100, "a b c")]
    [InlineData("  lead   and  trail  ", 100, "lead and trail")]
    [InlineData("a\u0000b\u0007c\u001Bd", 100, "abcd")]
    [InlineData("x\u202Ey\u200Bz\u2066w", 100, "xyzw")]
    [InlineData("line\u2028break\u2029here", 100, "line break here")]
    [InlineData("abcdef", 4, "abcd")]
    [InlineData("ab cd", 3, "ab")]
    [InlineData("ab\U0001F600", 3, "ab")]
    [InlineData("ab\U0001F600", 4, "ab\U0001F600")]
    [InlineData("<script>alert(1)</script>", 100, "<script>alert(1)</script>")]
    [InlineData("", 10, "")]
    [InlineData("abc", 0, "")]
    public void SanitizeProducesOneLineOfCappedPlainText(string input, int max, string expected)
    {
        SubstrateText.Sanitize(input, max).ShouldBe(expected);
    }

    [Fact]
    public void SanitizeOfNullIsEmpty()
    {
        SubstrateText.Sanitize(null, 10).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("plain lesson text", "plain lesson text")]
    [InlineData("[lessonbank]\nmilestone: v1\nphase: p\n\nThe lesson.", "The lesson.")]
    [InlineData("[lessonbank]\nmilestone: v1\n\nLine one\n\nLine two", "Line one\n\nLine two")]
    [InlineData("[lessonbank]\nmilestone: v1", "")]
    public void StripLessonHeaderRemovesOnlyTheMarkerAndHeaderBlock(string body, string expected)
    {
        SubstrateText.StripLessonHeader(body).ShouldBe(expected);
    }

    [Fact]
    public void SanitizeDropsAnUnpairedSurrogateInsteadOfShowingAReplacementCharacter()
    {
        SubstrateText.Sanitize(string.Concat("a", "\uD800", "b"), 100).ShouldBe("ab");
    }
}
