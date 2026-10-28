using System.Reflection;
using System.Text.RegularExpressions;
using OmpOS.Presentation.AgentPanel;
using OmpOS.Presentation.Mentions;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.Mentions;

/// <summary>The markup of the new composer views: sentences come only from the copy classes, fonts and colours only from the tokens, the row types are declared, nothing animates, and every copy constant is used.</summary>
public sealed class MentionMarkupTests
{
    private static readonly Regex LiteralFont = new("\\bFontFamily=\"(?<value>[^{\"][^\"]*)\"", RegexOptions.NonBacktracking | RegexOptions.CultureInvariant);

    private static readonly Regex LiteralColour = new("(?:Foreground|Background|Fill|Stroke|BorderBrush)=\"(?<value>#[^\"]*)\"", RegexOptions.NonBacktracking | RegexOptions.CultureInvariant);

    private static readonly Regex LiteralCopy = new("(?:\\bText|AutomationProperties\\.Name|AutomationProperties\\.HelpText|\\bContent|ToolTip\\.Tip|PlaceholderText)=\"(?<value>[^{\"][^\"]*)\"", RegexOptions.NonBacktracking | RegexOptions.CultureInvariant);

    private static string Markup(string file)
    {
        var text = File.ReadAllText(Path.Combine(ShellHarness.RepoRoot(), "src", "OmpOS.Native.Views", "Views", file));
        return Regex.Replace(text, "<!--.*?-->", string.Empty, RegexOptions.Singleline | RegexOptions.NonBacktracking | RegexOptions.CultureInvariant);
    }

    /// <summary>The three views hold no literal sentence: every text, name, help text and placeholder is a binding or a constant of a copy class.</summary>
    [Theory]
    [InlineData("MentionPickerView.axaml")]
    [InlineData("ConversationSearchView.axaml")]
    [InlineData("ContextRingView.axaml")]
    public void TheViewsHoldNoLiteralCopy(string file) =>
        LiteralCopy.Matches(Markup(file)).Select(static match => match.Groups["value"].Value).ShouldBeEmpty();

    /// <summary>The views name no font family and no colour: the mono face is the <c>mono</c> class and every brush comes from the tokens.</summary>
    [Theory]
    [InlineData("MentionPickerView.axaml")]
    [InlineData("ConversationSearchView.axaml")]
    [InlineData("ContextRingView.axaml")]
    [InlineData("ComposerView.axaml")]
    [InlineData("ComposerStyles.axaml")]
    public void TheViewsHoldNoLiteralFontOrColour(string file)
    {
        var markup = Markup(file);

        LiteralFont.Matches(markup).Select(static match => match.Groups["value"].Value).ShouldBeEmpty();
        LiteralColour.Matches(markup).Select(static match => match.Groups["value"].Value).ShouldBeEmpty();
    }

    /// <summary>Nothing animates, so reduced motion has nothing to switch off, and the row templates declare their data types.</summary>
    [Fact]
    public void NothingAnimatesAndTheRowTypesAreDeclared()
    {
        foreach (var file in new[] { "MentionPickerView.axaml", "ConversationSearchView.axaml", "ContextRingView.axaml" })
        {
            var markup = Markup(file);
            markup.ShouldNotContain("<Animation", Case.Sensitive);
            markup.ShouldNotContain("Transitions", Case.Sensitive);
        }

        Markup("MentionPickerView.axaml").ShouldContain("x:DataType=\"mentions:MentionPickerRow\"", Case.Sensitive);
        Markup("ConversationSearchView.axaml").ShouldContain("x:DataType=\"panel:ConversationSearchResult\"", Case.Sensitive);
        Markup("ComposerView.axaml").ShouldContain("x:DataType=\"mentions:MentionChip\"", Case.Sensitive);
    }

    /// <summary>Every name and help constant of the picker and search copy is used by the markup, so none is dead.</summary>
    [Fact]
    public void EveryNamingConstantIsUsed()
    {
        var picker = Markup("MentionPickerView.axaml");
        foreach (var name in new[] { nameof(MentionCopy.ListName), nameof(MentionCopy.ListHelp), nameof(MentionCopy.CountName), nameof(MentionCopy.MessageName) })
        {
            typeof(MentionCopy).GetField(name, BindingFlags.Public | BindingFlags.Static).ShouldNotBeNull(name);
            picker.ShouldContain("mentions:MentionCopy." + name, Case.Sensitive);
        }

        Markup("ComposerView.axaml").ShouldContain("mentions:MentionCopy." + nameof(MentionCopy.ChipsName), Case.Sensitive);
        var search = Markup("ConversationSearchView.axaml");
        foreach (var name in new[] { nameof(ConversationSearchCopy.DialogName), nameof(ConversationSearchCopy.QueryName), nameof(ConversationSearchCopy.QueryHelp), nameof(ConversationSearchCopy.ListName), nameof(ConversationSearchCopy.CountName) })
        {
            typeof(ConversationSearchCopy).GetField(name, BindingFlags.Public | BindingFlags.Static).ShouldNotBeNull(name);
            search.ShouldContain("panel:ConversationSearchCopy." + name, Case.Sensitive);
        }
    }

    /// <summary>The live regions are polite, and every interactive control of the chips is a target of the minimum size.</summary>
    [Fact]
    public void LiveRegionsArePoliteAndTargetsAreTheMinimumSize()
    {
        Regex.Count(Markup("MentionPickerView.axaml"), "AutomationProperties\\.LiveSetting=\"Polite\"", RegexOptions.NonBacktracking | RegexOptions.CultureInvariant).ShouldBe(2);
        Regex.Count(Markup("ConversationSearchView.axaml"), "AutomationProperties\\.LiveSetting=\"Polite\"", RegexOptions.NonBacktracking | RegexOptions.CultureInvariant).ShouldBe(1);
        var composer = Markup("ComposerView.axaml");
        composer.ShouldContain("MinHeight=\"{DynamicResource OmpOS.Size.TargetMin}\"", Case.Sensitive);
        composer.ShouldContain("MinWidth=\"{DynamicResource OmpOS.Size.TargetMin}\"", Case.Sensitive);
    }
}
