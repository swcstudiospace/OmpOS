using Bunit;
using OmpOS.UI.Components.Shell;
using Shouldly;
using Xunit;

namespace OmpOS.UI.Tests;

/// <summary>The footer carries the AGPL section 13 source offer: the version, a link to the source and the licence (PX-08).</summary>
public sealed class ShellFooterTests : UiTestContext
{
    /// <summary>The footer is the page's contentinfo and links the source repository and the licence.</summary>
    [Fact]
    public void FooterLinksTheSourceRepositoryAndNamesTheLicence()
    {
        var cut = Render<ShellFooter>();

        var footer = cut.Find("footer");
        var source = footer.QuerySelector("a.ompos-footer-source");
        source.ShouldNotBeNull();
        source.GetAttribute("href").ShouldBe("https://github.com/swcstudiospace/OmpOS");
        source.TextContent.ShouldBe("Source code");
        var licence = footer.QuerySelector(".ompos-footer-licence a");
        licence.ShouldNotBeNull();
        licence.TextContent.ShouldBe("AGPL-3.0-or-later");
        licence.GetAttribute("href").ShouldBe(ShellInfo.LicenceUrl);
    }

    /// <summary>The version defaults to the running assembly's version and can be set by the host.</summary>
    [Fact]
    public void FooterShowsTheAssemblyVersionByDefaultAndAGivenOne()
    {
        var byDefault = Render<ShellFooter>();
        byDefault.Find(".ompos-footer-version").TextContent.ShouldBe("OmpOS " + ShellInfo.CurrentVersion);
        ShellInfo.CurrentVersion.ShouldNotBeNullOrWhiteSpace();

        var given = Render<ShellFooter>(parameters => parameters.Add(p => p.Version, "0.15.0+abc1234"));
        given.Find(".ompos-footer-version").TextContent.ShouldBe("OmpOS 0.15.0+abc1234");
    }
}
