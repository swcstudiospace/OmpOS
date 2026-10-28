using OmpOS.Presentation.Composer;
using Shouldly;
using Xunit;

namespace OmpOS.Presentation.Tests.Composer;

/// <summary>The context ring: percentage, the 70 and 90 percent thresholds, unknown window sizes, the estimate of attached items, and the words that carry every state.</summary>
public sealed class ContextRingModelTests
{
    /// <summary>The state changes exactly at 70 and 90 percent, the percentage rounds down and is capped at 100.</summary>
    [Theory]
    [InlineData(0, 1000, 0, ContextRingState.Normal)]
    [InlineData(699, 1000, 69, ContextRingState.Normal)]
    [InlineData(700, 1000, 70, ContextRingState.Warning)]
    [InlineData(899, 1000, 89, ContextRingState.Warning)]
    [InlineData(900, 1000, 90, ContextRingState.Critical)]
    [InlineData(1000, 1000, 100, ContextRingState.Critical)]
    [InlineData(2000, 1000, 100, ContextRingState.Critical)]
    public void ThresholdsAndPercentage(long used, long window, int percent, ContextRingState state)
    {
        var ring = new ContextRingModel();

        ring.ReportUsage(used, window);

        ring.Percent.ShouldBe(percent);
        ring.State.ShouldBe(state);
        ring.Text.ShouldBe(percent + "%");
    }

    /// <summary>Every state has words in the tooltip, so colour is never the only carrier.</summary>
    [Theory]
    [InlineData(100, "Plenty of room")]
    [InlineData(750, "Getting full")]
    [InlineData(950, "Almost full")]
    public void EveryStateHasWords(long used, string words)
    {
        var ring = new ContextRingModel();
        ring.ReportUsage(used, 1000);

        ring.Tooltip.ShouldContain(words, Case.Sensitive);
        ring.AutomationName.ShouldStartWith("Context window.", Case.Sensitive);
        ring.AutomationName.ShouldContain(words, Case.Sensitive);
    }

    /// <summary>Without a window size the ring says so and gives no percentage; the tokens used are still shown.</summary>
    [Fact]
    public void AnUnknownWindowGivesNoPercentage()
    {
        var ring = new ContextRingModel();

        ring.ReportUsage(12_345);

        ring.HasWindow.ShouldBeFalse();
        ring.Percent.ShouldBeNull();
        ring.State.ShouldBe(ContextRingState.Unknown);
        ring.Text.ShouldBe("?");
        ring.Tooltip.ShouldBe("Context: 12.3k tokens used. Window size unknown.");
        ring.SweepDegrees.ShouldBe(0);
    }

    /// <summary>Before any usage the ring has no text and the tooltip says when usage appears.</summary>
    [Fact]
    public void BeforeAnyUsageTheRingIsQuiet()
    {
        var ring = new ContextRingModel();

        ring.Text.ShouldBeEmpty();
        ring.HasUsage.ShouldBeFalse();
        ring.Tooltip.ShouldBe(ContextRingCopy.NoUsageYet);
    }

    /// <summary>A usage report without a window keeps the known window; the model's window can be set and cleared; a negative usage is zero.</summary>
    [Fact]
    public void TheWindowSurvivesReportsAndCanBeReplaced()
    {
        var ring = new ContextRingModel();
        ring.ReportUsage(100, 1000);

        ring.ReportUsage(500);
        ring.Percent.ShouldBe(50);

        ring.SetWindow(2000);
        ring.Percent.ShouldBe(25);

        ring.SetWindow(null);
        ring.Percent.ShouldBeNull();

        ring.SetWindow(1000);
        ring.ReportUsage(-5);
        ring.UsedTokens.ShouldBe(0);
        ring.Percent.ShouldBe(0);
    }

    /// <summary>The attached items add to the ring as an estimate: the projected percentage and state include it, the plain percentage does not.</summary>
    [Fact]
    public void TheEstimateOfAttachedItemsIsAddedToTheProjection()
    {
        var ring = new ContextRingModel();
        ring.ReportUsage(500, 1000);

        ring.SetPending(250);

        ring.Percent.ShouldBe(50);
        ring.ProjectedPercent.ShouldBe(75);
        ring.ProjectedState.ShouldBe(ContextRingState.Warning);
        ring.State.ShouldBe(ContextRingState.Normal);
        ring.Text.ShouldBe("75%");
        ring.Tooltip.ShouldContain("plus about 250 from attached items", Case.Sensitive);
        ring.SweepDegrees.ShouldBe(270, 0.001);
    }

    /// <summary>Reset forgets the usage and the estimate and keeps the window; every property change is announced.</summary>
    [Fact]
    public void ResetKeepsTheWindowAndChangesAreAnnounced()
    {
        var ring = new ContextRingModel();
        ring.ReportUsage(800, 1000);
        var changed = new List<string?>();
        ring.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        ring.Reset();

        ring.HasUsage.ShouldBeFalse();
        ring.WindowTokens.ShouldBe(1000);
        ring.Text.ShouldBeEmpty();
        changed.ShouldContain(nameof(ContextRingModel.Percent), StringComparer.Ordinal);
        changed.ShouldContain(nameof(ContextRingModel.Tooltip), StringComparer.Ordinal);
        changed.ShouldContain(nameof(ContextRingModel.State), StringComparer.Ordinal);
    }

    /// <summary>Token counts are written compactly and without culture dependence.</summary>
    [Theory]
    [InlineData(0, "0")]
    [InlineData(999, "999")]
    [InlineData(1000, "1k")]
    [InlineData(12_345, "12.3k")]
    [InlineData(200_000, "200k")]
    [InlineData(1_500_000, "1.5M")]
    public void TokensAreWrittenCompactly(long tokens, string expected) => ContextRingCopy.FormatTokens(tokens).ShouldBe(expected);
}
