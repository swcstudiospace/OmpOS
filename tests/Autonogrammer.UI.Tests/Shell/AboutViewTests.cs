using Autonogrammer.Contracts.Catalog;
using Autonogrammer.UI.Components.About;
using Autonogrammer.UI.Components.Shell;
using Bunit;
using Shouldly;
using Xunit;

namespace Autonogrammer.UI.Tests.Shell;

/// <summary>The About view: the product and version, the AGPL notice, the source offer when the API reported it, and the accessibility commitment with the way to report a problem.</summary>
public sealed class AboutViewTests : UiTestContext
{
    private static SourceInfo Source(string source = "https://github.com/swcstudiospace/OmpOS") => new()
    {
        Licence = "AGPL-3.0-or-later",
        LicenceUrl = new Uri("https://www.gnu.org/licenses/agpl-3.0.html"),
        SourceUrl = new Uri(source),
        Revision = "abc1234",
        Version = "0.15.0+0123456789abcdef",
    };

    /// <summary>The view has one h1, the version the API reported, the licence notice, the source link and two accessibility paragraphs with the issue link.</summary>
    [Fact]
    public void WhenConnectedTheViewShowsTheVersionTheNoticeAndTheSourceLink()
    {
        var cut = Render<AboutView>(parameters => parameters.Add(p => p.Source, Source()));

        cut.FindAll("h1").Select(static heading => heading.TextContent).ShouldBe(["About Autonogrammer"]);
        cut.Find("h1").GetAttribute("tabindex").ShouldBe("-1");
        cut.Markup.ShouldContain("0.15.0+0123456", Case.Sensitive);
        cut.Markup.ShouldContain("abc1234", Case.Sensitive);
        cut.Markup.ShouldContain("AGPL-3.0-or-later", Case.Sensitive);
        cut.Find("a[href='https://github.com/swcstudiospace/OmpOS']").TextContent.ShouldContain(AboutViewCopy.SourceLinkLabel, Case.Sensitive);
        cut.Find("a[href='" + ShellInfo.LicenceUrl + "']").ShouldNotBeNull();
        cut.FindAll("h2").Select(static heading => heading.TextContent).ShouldBe([AboutViewCopy.LicenceHeading, AboutViewCopy.AccessibilityHeading]);
        cut.Find("section").TextContent.ShouldContain("WCAG", Case.Sensitive);
        cut.Find("section").TextContent.ShouldContain("2.2", Case.Sensitive);
        var issue = cut.Find("a[href$='/issues/new']");
        issue.GetAttribute("rel").ShouldBe("noopener noreferrer");
        issue.TextContent.ShouldContain(AboutViewCopy.NewTab, Case.Sensitive);
    }

    /// <summary>Without the API's answer the source offer is hidden, the version is the running assembly's, and the rest still shows.</summary>
    [Fact]
    public void WhenNotConnectedTheSourceOfferIsHiddenGracefully()
    {
        var cut = Render<AboutView>();

        cut.FindAll("a").Select(static link => link.GetAttribute("href")).ShouldNotContain("https://github.com/swcstudiospace/OmpOS", StringComparer.Ordinal);
        cut.Markup.ShouldNotContain(AboutViewCopy.SourceOffer);
        cut.Markup.ShouldContain(ShellInfo.CurrentVersion, Case.Sensitive);
        cut.Markup.ShouldContain(AboutViewCopy.LicenceNotice, Case.Sensitive);
        cut.Find("h1").TextContent.ShouldBe("About Autonogrammer");
    }

    /// <summary>An address from the API that is not http or https never becomes a link.</summary>
    [Fact]
    public void ANonWebSourceAddressIsNotLinked()
    {
        var cut = Render<AboutView>(parameters => parameters.Add(p => p.Source, Source("ftp://example.test/source")));

        cut.Markup.ShouldNotContain("ftp://");
        cut.Markup.ShouldNotContain(AboutViewCopy.SourceOffer);
    }
}
