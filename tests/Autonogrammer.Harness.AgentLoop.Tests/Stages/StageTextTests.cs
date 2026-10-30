using Autonogrammer.Harness.AgentLoop.Stages;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.AgentLoop.Tests.Stages;

/// <summary>Verifies that text from a vendor file is made plain and bounded before it reaches an event.</summary>
public sealed class StageTextTests
{
    [Fact]
    public void ControlAndFormatCharactersAreReplacedBySpaces()
    {
        StageText.Clean("a\u001b[31mred\u0007\nline\u202Eend\u2028x").ShouldBe("a [31mred line end x");
    }

    [Fact]
    public void ResultIsTruncatedToTheLimitAndTrimmed()
    {
        var result = StageText.Clean(new string('x', 500) + "tail");

        result.Length.ShouldBe(StageText.MaxDetailLength);
        result.ShouldNotContain("tail");
        StageText.Clean("  padded  ").ShouldBe("padded");
    }

    [Fact]
    public void TruncationNeverLeavesALoneHighSurrogate()
    {
        var text = new string('x', 9) + "\U0001F600";

        var result = StageText.Clean(text, 10);

        result.ShouldBe(new string('x', 9));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void NullAndEmptyBecomeEmpty(string? value)
    {
        StageText.Clean(value).ShouldBe(string.Empty);
    }
}
