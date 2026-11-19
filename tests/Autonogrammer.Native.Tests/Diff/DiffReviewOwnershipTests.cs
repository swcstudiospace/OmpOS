using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Diff;

/// <summary>
/// The Diff review tool holds the review of one session's changes. Once another session is shown, or that session's pane goes, the review
/// is stale: Apply must not write its hunks while another session is on screen.
/// </summary>
public sealed class DiffReviewOwnershipTests
{
    /// <summary>With no review held there is nothing to clear, whatever pane is shown.</summary>
    [Fact]
    public void NoHeldReviewIsNeverStale() => MainView.IsStaleDiffReview(null, new object()).ShouldBeFalse();

    /// <summary>A review of the pane that is shown stays, so Apply writes the changes of the session on screen.</summary>
    [Fact]
    public void AReviewOfTheShownPaneIsKept()
    {
        var pane = new object();

        MainView.IsStaleDiffReview(pane, pane).ShouldBeFalse();
    }

    /// <summary>A review of another session is stale once that session is no longer shown.</summary>
    [Fact]
    public void AReviewOfAnotherPaneIsStale() =>
        MainView.IsStaleDiffReview(new object(), new object()).ShouldBeTrue("switching to another session empties the Diff review");

    /// <summary>With no pane shown, a held review is stale.</summary>
    [Fact]
    public void AReviewWithNoShownPaneIsStale() => MainView.IsStaleDiffReview(new object(), null).ShouldBeTrue();
}
