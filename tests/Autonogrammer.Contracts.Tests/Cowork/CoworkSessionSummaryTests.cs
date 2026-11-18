using Autonogrammer.Contracts.Cowork;
using Shouldly;
using Xunit;

namespace Autonogrammer.Contracts.Tests.Cowork;

/// <summary>The stored title is capped at the limit and never ends in half of a surrogate pair.</summary>
public sealed class CoworkSessionSummaryTests
{
    private static readonly DateTimeOffset At = new(2026, 11, 18, 9, 0, 0, TimeSpan.Zero);

    /// <summary>A cut that falls between the halves of an emoji drops the whole emoji, so no lone surrogate is stored.</summary>
    [Fact]
    public void ACutBetweenSurrogateHalvesDropsTheWholeCharacter()
    {
        var title = new string('a', CoworkSessionSummary.MaxTitleLength - 1) + "\U0001F600" + "tail";

        var summary = Summary(title);

        summary.Title.ShouldBe(new string('a', CoworkSessionSummary.MaxTitleLength - 1));
    }

    /// <summary>A cut that falls after a whole emoji keeps the emoji, and the title stays at the limit.</summary>
    [Fact]
    public void ACutAfterWholeSurrogatePairKeepsThePair()
    {
        var title = new string('a', CoworkSessionSummary.MaxTitleLength - 2) + "\U0001F600" + "tail";

        var summary = Summary(title);

        summary.Title.ShouldBe(new string('a', CoworkSessionSummary.MaxTitleLength - 2) + "\U0001F600");
    }

    private static CoworkSessionSummary Summary(string title) => new()
    {
        Id = "session-1",
        Title = title,
        Status = CoworkSessionStatus.Created,
        CreatedAt = At,
        UpdatedAt = At,
    };
}
