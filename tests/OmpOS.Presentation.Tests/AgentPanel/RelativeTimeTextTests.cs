using OmpOS.Presentation.AgentPanel;
using Shouldly;
using Xunit;

namespace OmpOS.Presentation.Tests.AgentPanel;

/// <summary>The boundaries of the relative time text: just now, seconds, minutes, hours and days (46-UI-SPEC, Row layout).</summary>
public sealed class RelativeTimeTextTests
{
    /// <summary>Each boundary reads as the spec words it, including the last moment before and the first moment after.</summary>
    [Theory]
    [InlineData(0, "just now")]
    [InlineData(9, "just now")]
    [InlineData(10, "10 s ago")]
    [InlineData(59, "59 s ago")]
    [InlineData(60, "1 min ago")]
    [InlineData(5 * 60, "5 min ago")]
    [InlineData((59 * 60) + 59, "59 min ago")]
    [InlineData(60 * 60, "1 h ago")]
    [InlineData(2 * 60 * 60, "2 h ago")]
    [InlineData((23 * 3600) + 3599, "23 h ago")]
    [InlineData(24 * 3600, "1 d ago")]
    [InlineData(3 * 24 * 3600, "3 d ago")]
    public void BoundariesReadAsTheSpecWordsThem(int seconds, string expected)
    {
        RelativeTimeText.Format(TimeSpan.FromSeconds(seconds)).ShouldBe(expected);
    }

    /// <summary>A time in the future, from clock skew, reads as just now and never as a negative age.</summary>
    [Fact]
    public void AFutureTimeReadsAsJustNow()
    {
        var now = new DateTimeOffset(2026, 10, 26, 9, 0, 0, TimeSpan.Zero);

        RelativeTimeText.Format(now.AddMinutes(5), now).ShouldBe("just now");
    }

    /// <summary>A fractional age is cut down, so 9.9 seconds is still just now and 59.9 seconds is still seconds.</summary>
    [Fact]
    public void FractionsAreCutDown()
    {
        RelativeTimeText.Format(TimeSpan.FromMilliseconds(9_900)).ShouldBe("just now");
        RelativeTimeText.Format(TimeSpan.FromMilliseconds(59_900)).ShouldBe("59 s ago");
        RelativeTimeText.Format(TimeSpan.FromMinutes(59.99)).ShouldBe("59 min ago");
    }
}
