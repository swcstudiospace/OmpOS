using System.Reflection;
using OmpOS.Presentation.Commands;
using Shouldly;
using Xunit;

namespace OmpOS.Presentation.Tests.Commands;

/// <summary>The copy of the slash palette is complete: no constant is blank, and the built sentences say what the views need (SLASH-1).</summary>
public sealed class SlashPaletteCopyTests
{
    /// <summary>Every constant of the copy class is a non-blank sentence, so a view never shows or announces an empty string.</summary>
    [Fact]
    public void EveryConstantIsNonBlank()
    {
        var constants = typeof(SlashPaletteCopy)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(static field => field.IsLiteral && field.FieldType == typeof(string))
            .ToArray();

        constants.ShouldNotBeEmpty();
        foreach (var constant in constants)
        {
            ((string?)constant.GetRawConstantValue()).ShouldNotBeNullOrWhiteSpace(constant.Name);
        }
    }

    /// <summary>The result count says none, one, or the number of commands.</summary>
    [Theory]
    [InlineData(-1, "No commands")]
    [InlineData(0, "No commands")]
    [InlineData(1, "1 command")]
    [InlineData(2, "2 commands")]
    [InlineData(12, "12 commands")]
    public void TheResultCountReadsNaturally(int count, string expected) =>
        SlashPaletteCopy.ResultCount(count).ShouldBe(expected);

    /// <summary>The accessible name of a row names the command, then its input hint, its description and its source; absent parts are left out.</summary>
    [Fact]
    public void TheRowNameOmitsWhatIsAbsent()
    {
        SlashPaletteCopy.EntryName(new SlashPaletteEntry("fast", "Toggle fast mode", "[on|off]", SlashPaletteSource.Engine))
            .ShouldBe("/fast, [on|off], Toggle fast mode, Engine");
        SlashPaletteCopy.EntryName(new SlashPaletteEntry("side", string.Empty, null, SlashPaletteSource.OmpOs))
            .ShouldBe("/side, OmpOS");
        Should.Throw<ArgumentNullException>(() => SlashPaletteCopy.EntryName(null!));
    }
}
