using System.Reflection;
using System.Text.RegularExpressions;
using OmpOS.Presentation.Commands;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.SlashPalette;

/// <summary>The markup of the slash palette: copy comes only from <see cref="SlashPaletteCopy"/>, fonts and colours only from the tokens, every copy constant is used, and the row type is declared.</summary>
public sealed class SlashPaletteMarkupTests
{
    private static readonly Regex LiteralFont = new("\\bFontFamily=\"(?<value>[^{\"][^\"]*)\"", RegexOptions.NonBacktracking | RegexOptions.CultureInvariant);

    private static readonly Regex LiteralColour = new("(?:Foreground|Background|Fill|Stroke|BorderBrush)=\"(?<value>#[^\"]*)\"", RegexOptions.NonBacktracking | RegexOptions.CultureInvariant);

    private static readonly Regex LiteralCopy = new("(?:\\bText|AutomationProperties\\.Name|AutomationProperties\\.HelpText|\\bContent|ToolTip\\.Tip|PlaceholderText)=\"(?<value>[^{\"][^\"]*)\"", RegexOptions.NonBacktracking | RegexOptions.CultureInvariant);

    private static string Markup(string file)
    {
        var text = File.ReadAllText(Path.Combine(ShellHarness.RepoRoot(), "src", "OmpOS.Native.Views", "Views", file));
        return Regex.Replace(text, "<!--.*?-->", string.Empty, RegexOptions.Singleline | RegexOptions.NonBacktracking | RegexOptions.CultureInvariant);
    }

    /// <summary>The palette view holds no literal sentence: every text, name and help text is a binding or a constant of the copy class.</summary>
    [Fact]
    public void TheViewHoldsNoLiteralCopy() =>
        LiteralCopy.Matches(Markup("SlashPaletteView.axaml")).Select(static match => match.Groups["value"].Value).ShouldBeEmpty();

    /// <summary>The palette view names no font family and no colour: the mono face is the <c>mono</c> class and every brush comes from the tokens.</summary>
    [Fact]
    public void TheViewHoldsNoLiteralFontOrColour()
    {
        var markup = Markup("SlashPaletteView.axaml");

        LiteralFont.Matches(markup).Select(static match => match.Groups["value"].Value).ShouldBeEmpty();
        LiteralColour.Matches(markup).Select(static match => match.Groups["value"].Value).ShouldBeEmpty();
        markup.ShouldContain("Classes=\"mono\"", Case.Sensitive);
    }

    /// <summary>The palette holds no animation or transition, so reduced motion has nothing to switch off; the row template declares its data type and the live regions are polite.</summary>
    [Fact]
    public void TheViewHoldsNoAnimationAndDeclaresItsRowType()
    {
        var markup = Markup("SlashPaletteView.axaml");

        markup.ShouldNotContain("<Animation", Case.Sensitive);
        markup.ShouldNotContain("Transitions", Case.Sensitive);
        markup.ShouldContain("x:DataType=\"commands:SlashPaletteEntry\"", Case.Sensitive);
        Regex.Count(markup, "AutomationProperties\\.LiveSetting=\"Polite\"", RegexOptions.NonBacktracking | RegexOptions.CultureInvariant).ShouldBe(2);
    }

    /// <summary>Every name and help constant of the copy class is used by the markup, so none is dead and the view says nothing the copy does not.</summary>
    [Fact]
    public void EveryNamingConstantOfTheCopyIsUsedByTheView()
    {
        var markup = Markup("SlashPaletteView.axaml");
        var used = new[] { nameof(SlashPaletteCopy.ListName), nameof(SlashPaletteCopy.ListHelp), nameof(SlashPaletteCopy.CountName), nameof(SlashPaletteCopy.MessageName) };

        foreach (var name in used)
        {
            typeof(SlashPaletteCopy).GetField(name, BindingFlags.Public | BindingFlags.Static).ShouldNotBeNull(name);
            markup.ShouldContain("SlashPaletteCopy." + name, Case.Sensitive);
        }
    }

    /// <summary>The composer view hosts the palette in the row above the card (row 0, the row of the suggestions box) and names no literal font.</summary>
    [Fact]
    public void TheComposerHostsThePaletteAboveTheCard()
    {
        var markup = Markup("ComposerView.axaml");

        Regex.IsMatch(markup, "<StackPanel Grid\\.Row=\"0\">\\s*<local:SlashPaletteView x:Name=\"SlashPalette\"", RegexOptions.NonBacktracking | RegexOptions.CultureInvariant).ShouldBeTrue();
        LiteralFont.Matches(markup).ShouldBeEmpty();
    }
}
