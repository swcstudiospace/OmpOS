using Autonogrammer.UI.Components.Shell;
using Bunit;
using Shouldly;
using Xunit;

namespace Autonogrammer.UI.Tests.Shell;

/// <summary>The not-found view: one heading that can take focus, a sentence and a link home.</summary>
public sealed class NotFoundViewTests : UiTestContext
{
    /// <summary>The view has one focusable h1, a sentence and a link to the given address.</summary>
    [Fact]
    public void TheViewHasOneHeadingASentenceAndALinkHome()
    {
        var cut = Render<NotFoundView>(parameters => parameters
            .Add(p => p.Heading, "Page not found")
            .Add(p => p.Body, "Nothing lives here.")
            .Add(p => p.LinkText, "Go home")
            .Add(p => p.Href, "/"));

        var heading = cut.Find("h1");
        heading.TextContent.ShouldBe("Page not found");
        heading.GetAttribute("tabindex").ShouldBe("-1");
        cut.Find("p").TextContent.ShouldBe("Nothing lives here.");
        var link = cut.Find("a");
        link.TextContent.ShouldBe("Go home");
        link.GetAttribute("href").ShouldBe("/");
    }

    /// <summary>The heading takes focus on the first render only when the page asks for it.</summary>
    [Fact]
    public void TheHeadingTakesFocusOnlyOnRequest()
    {
        var quiet = Render<NotFoundView>(parameters => parameters
            .Add(p => p.Heading, "Quiet")
            .Add(p => p.Body, "No focus move."));
        quiet.Find("h1").TextContent.ShouldBe("Quiet");
        JSInterop.FocusCalls().ShouldBeEmpty();

        var focused = Render<NotFoundView>(parameters => parameters
            .Add(p => p.Heading, "Focused")
            .Add(p => p.Body, "Focus moves.")
            .Add(p => p.FocusOnRender, true));

        var id = focused.Find("h1").InitialReferenceId();
        JSInterop.FocusCalls().Count.ShouldBe(1);
        JSInterop.FocusCalls()[0].ReferenceIdOf().ShouldBe(id);
    }
}
