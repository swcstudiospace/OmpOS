using System.Text;
using Autonogrammer.Contracts.Artifacts;
using Autonogrammer.UI.Components.Cowork.Artifacts;
using Bunit;
using Shouldly;
using Xunit;

namespace Autonogrammer.UI.Tests.Cowork.Artifacts;

/// <summary>A rejected SVG shows its source in a labelled, focusable region (SCR-ArtifactReview, error.validation).</summary>
public sealed class ArtifactBodySvgFallbackTests : UiTestContext
{
    /// <summary>The source fallback is reachable by keyboard and carries an accessible name.</summary>
    [Fact]
    public void ARejectedSvgShowsItsSourceInALabelledFocusableRegion()
    {
        byte[] content = Encoding.UTF8.GetBytes("not an svg document");

        var cut = Render<ArtifactBody>(parameters => parameters
            .Add(p => p.Kind, ArtifactKind.Svg)
            .Add(p => p.Title, "diagram.svg")
            .Add(p => p.MediaType, "image/svg+xml")
            .Add(p => p.Content, content)
            .Add(p => p.SyncOff, false));

        var pre = cut.Find("pre[tabindex='0']");
        pre.GetAttribute("role").ShouldBe("group");
        pre.GetAttribute("aria-label").ShouldBe("SVG source");
        pre.TextContent.ShouldContain("not an svg document");
        cut.FindAll("iframe").ShouldBeEmpty("a rejected SVG never renders in a frame");
    }
}
