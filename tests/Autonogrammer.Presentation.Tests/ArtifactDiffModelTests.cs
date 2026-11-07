using Autonogrammer.Presentation.Cowork.Artifacts;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests;

/// <summary>The diff carries a word and a sign for each change, so it reads without colour.</summary>
public sealed class ArtifactDiffModelTests
{
    /// <summary>Each change has its own sign and word, independent of colour.</summary>
    [Fact]
    public void EveryChangeHasASignAndAWord()
    {
        ArtifactDiffModel.MarkerFor(ArtifactDiffChange.Added).ShouldBe("+");
        ArtifactDiffModel.MarkerFor(ArtifactDiffChange.Removed).ShouldBe("-");
        ArtifactDiffModel.MarkerFor(ArtifactDiffChange.Unchanged).ShouldBe(" ");
        ArtifactDiffModel.LabelFor(ArtifactDiffChange.Added).ShouldBe("Added");
        ArtifactDiffModel.LabelFor(ArtifactDiffChange.Removed).ShouldBe("Removed");
    }

    /// <summary>A text diff keeps its lines and reports its counts.</summary>
    [Fact]
    public void TextDiffKeepsLinesAndCounts()
    {
        var model = new ArtifactDiffModel(
            [new ArtifactDiffLine(ArtifactDiffChange.Removed, "old"), new ArtifactDiffLine(ArtifactDiffChange.Added, "new")],
            addedCount: 1,
            removedCount: 1,
            countsOnly: false,
            metadataOnly: false);

        model.Lines.Count.ShouldBe(2);
        model.Summary.ShouldBe("Added 1 lines, removed 1.");
    }

    /// <summary>Over the cap the diff has counts only; a binary kind has metadata only.</summary>
    [Fact]
    public void CapAndBinaryKindsSayWhatTheyCompare()
    {
        new ArtifactDiffModel([], 4000, 3000, countsOnly: true, metadataOnly: false).Summary
            .ShouldBe("Diff too large to show lines. Added 4000 lines, removed 3000.");
        new ArtifactDiffModel([], 0, 0, countsOnly: false, metadataOnly: true).Summary
            .ShouldBe("Binary version. Only size and hash differ are compared.");
    }
}
