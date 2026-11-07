using Autonogrammer.UI.Components.Cowork.Artifacts;
using Bunit;
using Shouldly;
using Xunit;

namespace Autonogrammer.UI.Tests.Cowork.Artifacts;

/// <summary>
/// The Markdown artifact view under the artifact link policy (71-02 amendment 2): a relative, <c>artifact:</c> or <c>javascript:</c>
/// target is text, never an anchor; an https target keeps its text and shows the full URL, with no anchor either.
/// </summary>
public sealed class ArtifactMarkdownHostileTests : UiTestContext
{
    /// <summary>The hostile link fixture renders with no anchor and every target visible only as text.</summary>
    [Fact]
    public void HostileLinkTargetsAreTextAndNoAnchorNavigates()
    {
        var markdown = File.ReadAllText(Path.Combine(CssSources.RepoRoot, "tests", "Autonogrammer.Cowork.Artifacts.Tests", "Fixtures", "hostile", "md", "script-link.md"));

        var cut = Render<ArtifactMarkdownView>(parameters => parameters.Add(p => p.Markdown, markdown));

        cut.FindAll("a").ShouldBeEmpty("no link of an artifact is an anchor");
        cut.FindAll("[href]").ShouldBeEmpty();
        cut.Markup.ShouldContain("run this", Case.Sensitive, "the script link keeps its text");
        cut.Markup.ShouldContain("the next file", Case.Sensitive, "the relative link keeps its text");
        cut.Markup.ShouldContain("the artifact", Case.Sensitive, "the artifact link keeps its text");
    }

    /// <summary>An https target keeps its text and shows the full URL beside it, so the reader sees where it goes.</summary>
    [Fact]
    public void AnHttpsTargetShowsItsFullUrlAsText()
    {
        var html = ArtifactMarkdownLinks.Apply("<p><a href=\"https://example.com/page\">the site</a></p>");

        html.ShouldBe("<p>the site (https://example.com/page)</p>");
    }

    /// <summary>A javascript target, in any letter case, is refused as an anchor and shows only its text.</summary>
    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("JavaScript:alert(1)")]
    [InlineData("artifact:abc")]
    [InlineData("../other.md")]
    public void NoOtherTargetBecomesAnAnchor(string target)
    {
        var html = ArtifactMarkdownLinks.Apply($"<p><a href=\"{target}\">text</a></p>");

        html.ShouldBe("<p>text</p>");
    }
}
