using OmpOS.Presentation.Commands;
using Shouldly;
using Xunit;

namespace OmpOS.Presentation.Tests.Commands;

/// <summary>The shipped catalogue records the Cursor references its names were checked against (VX-02).</summary>
public sealed class SlashCommandReferenceTests
{
    /// <summary>The shipped catalogue records the research record that holds the verified rows and the date it was read.</summary>
    [Fact]
    public void ShippedCatalogueRecordsTheCursorReferenceAndItsReadDate()
    {
        SlashCommandCatalogue.Reference.Source.ShouldBe("docs/plans/vision-research/cursor-slash-commands.md");
        SlashCommandCatalogue.Reference.ReadOn.ShouldBe(new DateOnly(2026, 10, 22));
    }

    /// <summary>The six tokens added after the first record cite the later research record and its read date.</summary>
    [Fact]
    public void TheAdditionsRecordTheirOwnReference()
    {
        SlashCommandCatalogue.AdditionsReference.Source.ShouldBe("docs/plans/v0.15.0-research/03-cursor-settings-slash.md");
        SlashCommandCatalogue.AdditionsReference.ReadOn.ShouldBe(new DateOnly(2026, 10, 27));
    }
}
