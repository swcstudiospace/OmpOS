using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.RemoteProvision.Tests;

/// <summary>An excerpt for an error message is at most 200 characters and never carries a line break or a control character.</summary>
public sealed class RemoteOutputExcerptTests
{
    /// <summary>A long text is cut at 200 characters.</summary>
    [Fact]
    public void LongTextIsCutAtTwoHundredCharacters()
    {
        var excerpt = RemoteOutputExcerpt.Of(new string('y', 1000));

        excerpt.Length.ShouldBe(RemoteOutputExcerpt.MaxCharacters);
        RemoteOutputExcerpt.MaxCharacters.ShouldBe(200);
    }

    /// <summary>Line breaks and other control characters become spaces, so one message stays one line.</summary>
    [Fact]
    public void ControlCharactersBecomeSpaces()
    {
        RemoteOutputExcerpt.Of("first line\nsecond\tline\u001b[31m").ShouldBe("first line second line [31m");
    }

    /// <summary>A missing or blank text gives an empty excerpt.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \n  ")]
    public void MissingOrBlankTextGivesEmpty(string? text)
    {
        RemoteOutputExcerpt.Of(text).ShouldBeEmpty();
    }
}
