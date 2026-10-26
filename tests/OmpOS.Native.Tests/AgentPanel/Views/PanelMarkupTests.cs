using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.AgentPanel.Views;

/// <summary>Rules on the Agent Panel markup that no headless render shows: new size tokens, compiled bindings and copy that lives in one place.</summary>
public sealed class PanelMarkupTests
{
    private const string XamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";
    private const string PanelDirectory = "src/OmpOS.Native.Views/Views/AgentPanel";
    private const string TokenPath = "src/OmpOS.Native.Views/Themes/OmpOSTokens.axaml";

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
    [InlineData("OmpOS.Size.AgentRailWidth", 280)]
    [InlineData("OmpOS.Size.StageFlyoutWidth", 360)]
    [InlineData("OmpOS.Size.StageFlyoutMaxHeight", 360)]
    [InlineData("OmpOS.Size.ThreadMinWidth", 400)]
    [InlineData("OmpOS.Size.ThreadContentMaxWidth", 760)]
    [InlineData("OmpOS.Size.SessionRowMinHeight", 48)]
    [InlineData("OmpOS.Size.StageRowMinHeight", 28)]
    [InlineData("OmpOS.Size.ChipHeight", 20)]
    [InlineData("OmpOS.Size.IconSize", 16)]
    [InlineData("OmpOS.Size.IconSizeSm", 14)]
    [InlineData("OmpOS.Size.ControlHeight", 32)]
    [InlineData("OmpOS.Size.ControlHeightSm", 28)]
    [InlineData("OmpOS.Size.IconButton", 28)]
    [InlineData("OmpOS.Size.IconButtonSm", 24)]
    [InlineData("OmpOS.Size.TitleBarHeight", 40)]
    [InlineData("OmpOS.Size.PipelineStripHeight", 28)]
    [InlineData("OmpOS.Size.ComposerMinHeight", 72)]
    [InlineData("OmpOS.Size.ComposerMaxHeight", 220)]
    [InlineData("OmpOS.Size.ScrollBarThumb", 6)]
    [InlineData("OmpOS.Size.SettingsNavWidth", 220)]
    [InlineData("OmpOS.Size.SettingsContentMaxWidth", 720)]
    [InlineData("OmpOS.Size.KanbanColumnWidth", 280)]
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
