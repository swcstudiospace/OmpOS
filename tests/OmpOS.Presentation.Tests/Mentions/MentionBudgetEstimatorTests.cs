using OmpOS.Presentation.Mentions;
using Shouldly;
using Xunit;

namespace OmpOS.Presentation.Tests.Mentions;

/// <summary>The context estimate of attached mentions, and the size text of a chip.</summary>
public sealed class MentionBudgetEstimatorTests
{
    private static ResolvedMention Mention(long size) => new(
        MentionKind.File,
        "f" + size,
        "f" + size,
        new Uri("file:///f"),
        "text/plain",
        size,
        false,
        (_, _) => Task.FromResult(new MentionContent(string.Empty, 0, false)));

    /// <summary>A token is four bytes, rounded up; zero and negative sizes count as nothing.</summary>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(-5, 0)]
    [InlineData(1, 1)]
    [InlineData(4, 1)]
    [InlineData(5, 2)]
    [InlineData(4000, 1000)]
    public void TokensRoundUp(long bytes, long expected) => MentionBudgetEstimator.TokensForBytes(bytes).ShouldBe(expected);

    /// <summary>Each mention counts up to the send cap; a mention of unknown size counts at the assumed size, capped as well.</summary>
    [Fact]
    public void EachMentionIsCappedAndUnknownSizesAreAssumed()
    {
        var budget = MentionBudgetEstimator.Estimate([Mention(400), Mention(10_000_000), Mention(-1)], maxContentBytes: 1000);

        budget.EstimatedBytes.ShouldBe(400 + 1000 + 1000);
        budget.EstimatedTokens.ShouldBe(600);
        budget.UnknownSizeCount.ShouldBe(1);
    }

    /// <summary>No mentions is no budget.</summary>
    [Fact]
    public void NoMentionsIsNoBudget() => MentionBudgetEstimator.Estimate([], 1000).ShouldBe(MentionBudget.None);

    /// <summary>The unknown size is the assumed size when the cap allows it.</summary>
    [Fact]
    public void AnUnknownSizeUsesTheAssumedSize() =>
        MentionBudgetEstimator.Estimate([Mention(-1)], MentionLimits.DefaultContentBytes).EstimatedBytes.ShouldBe(MentionBudgetEstimator.AssumedUnknownBytes);

    /// <summary>Sizes are written in bytes, KB and MB with one decimal at most, with no culture dependence.</summary>
    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(1023, "1023 B")]
    [InlineData(1024, "1 KB")]
    [InlineData(1536, "1.5 KB")]
    [InlineData(1048576, "1 MB")]
    [InlineData(1572864, "1.5 MB")]
    public void SizesAreWrittenCompactly(long bytes, string expected) => MentionCopy.FormatSize(bytes).ShouldBe(expected);
}
