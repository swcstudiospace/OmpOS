using Autonogrammer.Contracts.Catalog;
using Autonogrammer.Presentation.About;
using Autonogrammer.UI.Components.About;
using Bunit;
using Shouldly;
using Xunit;

namespace Autonogrammer.UI.Tests.Shell;

/// <summary>The web About view states the AGPL source offer in the desktop window's exact words, as one paragraph, connected or not.</summary>
public sealed class AboutOfferRegressionTests : UiTestContext
{
    private static SourceInfo Source() => new()
    {
        Licence = "AGPL-3.0-or-later",
        LicenceUrl = new Uri("https://www.gnu.org/licenses/agpl-3.0.html"),
        SourceUrl = new Uri("https://github.com/swcstudiospace/OmpOS"),
        Revision = "abc1234",
        Version = "0.15.0+0123456789abcdef",
    };

    /// <summary>The paragraphs whose whole text is the desktop source offer.</summary>
    private static string[] OfferParagraphs(IRenderedComponent<AboutView> cut) =>
        [.. cut.FindAll("p").Select(static paragraph => paragraph.TextContent).Where(static text => string.Equals(text, AboutCopy.SourceOffer, StringComparison.Ordinal))];

    /// <summary>Connected, the web offer is the desktop offer verbatim and appears exactly once.</summary>
    [Fact]
    public void WhenConnectedTheWebOfferIsTheDesktopOfferOnce()
    {
        var cut = Render<AboutView>(parameters => parameters.Add(p => p.Source, Source()));

        OfferParagraphs(cut).ShouldBe([AboutCopy.SourceOffer]);
    }

    /// <summary>Not connected, the offer still shows verbatim: the section 13 offer does not depend on the API answering.</summary>
    [Fact]
    public void WhenNotConnectedTheWebOfferIsTheDesktopOfferOnce()
    {
        var cut = Render<AboutView>();

        OfferParagraphs(cut).ShouldBe([AboutCopy.SourceOffer]);
    }
}
