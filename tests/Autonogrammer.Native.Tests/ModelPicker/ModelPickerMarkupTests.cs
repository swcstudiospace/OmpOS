using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.ModelPicker;

/// <summary>The markup of the model chip and the composer footer: fonts come from the <c>Autonogrammer.Font.*</c> tokens, there is no animation to gate, and the model type is declared.</summary>
public sealed class ModelPickerMarkupTests
{
    private static readonly Regex LiteralFont = new("\\bFontFamily=\"(?<value>[^{\"][^\"]*)\"", RegexOptions.NonBacktracking | RegexOptions.CultureInvariant);

    private static readonly Regex LiteralColour = new("(?:Foreground|Background|Fill|Stroke|BorderBrush)=\"(?<value>#[^\"]*)\"", RegexOptions.NonBacktracking | RegexOptions.CultureInvariant);

    private static string Markup(string file)
    {
        var text = File.ReadAllText(Path.Combine(ShellHarness.RepoRoot(), "src", "Autonogrammer.Native.Views", "Views", file));
        return Regex.Replace(text, "<!--.*?-->", string.Empty, RegexOptions.Singleline | RegexOptions.NonBacktracking | RegexOptions.CultureInvariant);
    }

    /// <summary>Neither view names a font family: families come only from the tokens, and the chip uses the mono token.</summary>
    [Fact]
    public void NoViewHoldsALiteralFontFamily()
    {
        foreach (var file in new[] { "ModelPickerView.axaml", "ComposerView.axaml" })
        {
            LiteralFont.Matches(Markup(file)).Select(static match => match.Groups["value"].Value).ShouldBeEmpty(file);
        }

        Markup("ModelPickerView.axaml").ShouldContain("FontFamily=\"{DynamicResource Autonogrammer.Font.Mono}\"", Case.Sensitive);
    }

    /// <summary>The views hold no colour literal: brushes come from the tokens.</summary>
    [Fact]
    public void NoViewHoldsALiteralColour()
    {
        foreach (var file in new[] { "ModelPickerView.axaml", "ComposerView.axaml" })
        {
            LiteralColour.Matches(Markup(file)).Select(static match => match.Groups["value"].Value).ShouldBeEmpty(file);
        }
    }

    /// <summary>The chip and its flyout hold no animation or transition, so reduced motion has nothing to switch off; the row template declares its data type.</summary>
    [Fact]
    public void TheChipHoldsNoAnimationAndDeclaresItsRowType()
    {
        var markup = Markup("ModelPickerView.axaml");

        markup.ShouldNotContain("<Animation", Case.Sensitive);
        markup.ShouldNotContain("Transitions", Case.Sensitive);
        markup.ShouldContain("x:DataType=\"composer:ModelPickerRow\"", Case.Sensitive);
        markup.ShouldContain("AutomationProperties.LiveSetting=\"Polite\"", Case.Sensitive);
    }
}
