using Bunit;
using Shouldly;
using Xunit;

namespace OmpOS.Web.Tests;

/// <summary>The New control's accessible name contains its visible label (WCAG 2.5.3 Label in Name), on the page that mounts the workbench.</summary>
public sealed class NewConversationButtonTests : BunitContext
{
    /// <summary>The visible "New" label is part of the button's accessible name.</summary>
    [Fact]
    public void NewConversationButtonNameContainsVisibleLabel()
    {
        using var host = new DemoTestHost();
        DemoPageSetup.Register(Services, host);
        JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = Render<OmpOS.Web.Pages.Index>();
        cut.WaitForAssertion(() => cut.Find("button.ompos-btn").ShouldNotBeNull(), TimeSpan.FromSeconds(10));

        var button = cut.FindAll("button.ompos-btn").First(b => b.TextContent.Contains("New", StringComparison.Ordinal));
        var visibleLabel = button.TextContent.Trim();
        visibleLabel.ShouldContain("New");
        var accessibleName = button.GetAttribute("aria-label");
        accessibleName.ShouldNotBeNullOrWhiteSpace();
        accessibleName.ShouldContain("New", customMessage: "the accessible name must contain the visible label");
    }
}
