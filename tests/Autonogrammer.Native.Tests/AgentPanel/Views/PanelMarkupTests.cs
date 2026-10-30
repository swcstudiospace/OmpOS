using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.AgentPanel.Views;

/// <summary>Rules on the Agent Panel markup that no headless render shows: new size tokens, compiled bindings and copy that lives in one place.</summary>
public sealed class PanelMarkupTests
{
    private const string XamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";
    private const string PanelDirectory = "src/Autonogrammer.Native.Views/Views/AgentPanel";
    private const string TokenPath = "src/Autonogrammer.Native.Views/Themes/AutonogrammerTokens.axaml";

    private static readonly Regex LiteralCopy = new(
        "(?:\\bText|\\bContent|\\bHeader|\\bPlaceholderText|AutomationProperties\\.Name|ToolTip\\.Tip)=\"(?<value>[^{\"][^\"]*)\"",
        RegexOptions.NonBacktracking | RegexOptions.CultureInvariant);

    private static readonly string[] ViewsWithAModel =
    [
        "AgentRailView",
        "PanelStateView",
        "ThreadHeaderView",
        "PanelNoticeView",
        "ConfirmBarView",
        "PipelineStripView",
        "PipelineTabView",
    ];

    private static string Root => ShellHarness.RepoRoot();

    /// <summary>Each new size token of 46-UI-SPEC exists with its value, as a plain double outside the theme variants.</summary>
    [Theory]
    [InlineData("Autonogrammer.Size.AgentRailWidth", 280)]
    [InlineData("Autonogrammer.Size.StageFlyoutWidth", 360)]
    [InlineData("Autonogrammer.Size.StageFlyoutMaxHeight", 360)]
    [InlineData("Autonogrammer.Size.ThreadMinWidth", 400)]
    [InlineData("Autonogrammer.Size.ThreadContentMaxWidth", 760)]
    [InlineData("Autonogrammer.Size.SessionRowMinHeight", 48)]
    [InlineData("Autonogrammer.Size.StageRowMinHeight", 28)]
    [InlineData("Autonogrammer.Size.ChipHeight", 20)]
    [InlineData("Autonogrammer.Size.IconSize", 16)]
    [InlineData("Autonogrammer.Size.IconSizeSm", 14)]
    [InlineData("Autonogrammer.Size.ControlHeight", 32)]
    [InlineData("Autonogrammer.Size.ControlHeightSm", 28)]
    [InlineData("Autonogrammer.Size.IconButton", 28)]
    [InlineData("Autonogrammer.Size.IconButtonSm", 24)]
    [InlineData("Autonogrammer.Size.TitleBarHeight", 40)]
    [InlineData("Autonogrammer.Size.PipelineStripHeight", 28)]
    [InlineData("Autonogrammer.Size.ComposerMinHeight", 72)]
    [InlineData("Autonogrammer.Size.ComposerMaxHeight", 220)]
    [InlineData("Autonogrammer.Size.ScrollBarThumb", 6)]
    [InlineData("Autonogrammer.Size.SettingsNavWidth", 220)]
    [InlineData("Autonogrammer.Size.SettingsContentMaxWidth", 720)]
    [InlineData("Autonogrammer.Size.KanbanColumnWidth", 280)]
    public void TheNewSizeTokensExistOutsideTheThemeVariants(string key, int value)
    {
        var document = XDocument.Load(Path.Combine(Root, TokenPath));
        var name = XName.Get("Key", XamlNamespace);

        var element = document.Descendants().Single(candidate => string.Equals((string?)candidate.Attribute(name), key, StringComparison.Ordinal));

        element.Name.LocalName.ShouldBe("Double");
        ((int)double.Parse(element.Value, CultureInfo.InvariantCulture)).ShouldBe(value);
        element.Ancestors().Any(static ancestor => string.Equals((string?)ancestor.Attribute(XName.Get("Key", XamlNamespace)), "Light", StringComparison.Ordinal)
            || string.Equals((string?)ancestor.Attribute(XName.Get("Key", XamlNamespace)), "Dark", StringComparison.Ordinal)).ShouldBeFalse("a size is not a colour and does not change with the theme");
    }

    /// <summary>Every panel view that draws a model declares its type, so its bindings compile.</summary>
    [Fact]
    public void EveryPanelViewDeclaresItsDataType()
    {
        foreach (var view in ViewsWithAModel)
        {
            var markup = File.ReadAllText(Path.Combine(Root, PanelDirectory, view + ".axaml"));
            markup.ShouldContain("x:DataType=\"copy:", Case.Sensitive, $"{view} must declare the model it draws");
        }
    }

    /// <summary>No panel view writes a sentence or a name as a literal: every one is bound or read from the copy constants.</summary>
    [Fact]
    public void NoPanelViewHoldsLiteralCopy()
    {
        var literals = new List<string>();
        foreach (var file in Directory.EnumerateFiles(Path.Combine(Root, PanelDirectory), "*.axaml"))
        {
            var markup = Regex.Replace(File.ReadAllText(file), "<!--.*?-->", string.Empty, RegexOptions.Singleline | RegexOptions.NonBacktracking | RegexOptions.CultureInvariant);
            literals.AddRange(LiteralCopy.Matches(markup).Select(match => $"{Path.GetFileName(file)}: {match.Groups["value"].Value}"));
        }

        literals.ShouldBeEmpty();
    }
}
