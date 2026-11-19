using Autonogrammer.Contracts.Catalog;
using Autonogrammer.Presentation.About;
using Autonogrammer.UI.Components.About;
using Autonogrammer.UI.Components.Shell;
using Bunit;
using Shouldly;
using Xunit;

namespace Autonogrammer.UI.Tests.Shell;

/// <summary>The About view: the product and version, the licence, the source offer shared with the desktop window, and the accessibility commitment with the way to report a problem.</summary>
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

    /// <summary>The view has one h1, the version the API reported, the source offer, the source link and two accessibility paragraphs with the issue link.</summary>
    [Fact]
    public void WhenConnectedTheViewShowsTheVersionTheOfferAndTheSourceLink()
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

    /// <summary>Without the API's answer the offer still shows, the source link goes to the repository address, the version is the running assembly's, and the rest still shows.</summary>
    [Fact]
    public void WhenNotConnectedTheOfferShowsWithTheRepositoryAddress()
    {
        var cut = Render<AboutView>();

        cut.Find("a[href='https://github.com/swcstudiospace/OmpOS']").TextContent.ShouldContain(AboutViewCopy.SourceLinkLabel, Case.Sensitive);
        cut.Markup.ShouldContain(AboutCopy.SourceOffer, Case.Sensitive);
        cut.Markup.ShouldContain(ShellInfo.CurrentVersion, Case.Sensitive);
        cut.Find("h1").TextContent.ShouldBe("About Autonogrammer");
    }

    /// <summary>An address from the API that is not http or https never becomes a link.</summary>
    [Fact]
    public void ANonWebSourceAddressIsNotLinked()
    {
        var cut = Render<AboutView>(parameters => parameters.Add(p => p.Source, Source("ftp://example.test/source")));

        cut.Markup.ShouldNotContain("ftp://");
        cut.Markup.ShouldContain(AboutCopy.SourceOffer, Case.Sensitive);
    }
}
