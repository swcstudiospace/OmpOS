using Autonogrammer.UI.Components.Shell;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Autonogrammer.UI.Tests.Shell;

/// <summary>The route announcer: a polite status that says the new page title after a navigation, and nothing on the first load.</summary>
public sealed class RouteAnnouncerTests : UiTestContext
{
    private NavigationManager Navigation => Services.GetRequiredService<NavigationManager>();

    /// <summary>The first title is the page the browser already announced; a navigation arms the announcer and the next title is spoken once.</summary>
    [Fact]
    public void ATitleIsAnnouncedAfterANavigationButNotOnTheFirstLoad()
    {
        using var announcer = new RouteAnnouncer(Navigation);
        var region = Render<RouteAnnouncerRegion>(parameters => parameters.AddCascadingValue(announcer));
        var title = Render<ViewTitle>(parameters => parameters
            .Add(p => p.Text, ViewTitles.Compose("Agents"))
            .AddCascadingValue(announcer));

        announcer.Text.ShouldBeEmpty("nothing is announced on the first load");

        Navigation.NavigateTo("kanban");
        title.Render(parameters => parameters.Add(p => p.Text, ViewTitles.Compose("Kanban")));

        region.WaitForAssertion(() => region.Find("[role='status']").TextContent.ShouldBe("Kanban - Autonogrammer"));
        announcer.Text.ShouldBe("Kanban - Autonogrammer");

        title.Render(parameters => parameters.Add(p => p.Text, ViewTitles.Compose("Later", "Kanban")));

        announcer.Text.ShouldBe("Kanban - Autonogrammer", "one navigation is announced once");
    }

    /// <summary>The app arms the announcer before it changes the view: a title that renders before the browser reports the address is announced, and that address does not arm the announcer again.</summary>
    [Fact]
    public void ATitleThatRendersBeforeTheAddressIsReportedIsAnnounced()
    {
        using var announcer = new RouteAnnouncer(Navigation);

        announcer.Arm("kanban");
        announcer.Report(ViewTitles.Compose("Kanban"));
        Navigation.NavigateTo("kanban");
        announcer.Report(ViewTitles.Compose("Later", "Kanban"));

        announcer.Text.ShouldBe("Kanban - Autonogrammer", "the address of a navigation the app made is not a second navigation");
    }

    /// <summary>An address the app did not arm for (Back, Forward, a link) arms the announcer even after the app armed it for another one.</summary>
    [Fact]
    public void AnotherAddressStillArmsTheAnnouncer()
    {
        using var announcer = new RouteAnnouncer(Navigation);
        announcer.Arm("kanban");
        announcer.Report(ViewTitles.Compose("Kanban"));

        Navigation.NavigateTo("about");
        announcer.Report(ViewTitles.Compose("About"));

        announcer.Text.ShouldBe("About - Autonogrammer");
    }

    /// <summary>The region is a visually hidden polite status that is present before anything is announced, so a screen reader registers it.</summary>
    [Fact]
    public void TheRegionIsAVisuallyHiddenPoliteStatus()
    {
        using var announcer = new RouteAnnouncer(Navigation);

        var region = Render<RouteAnnouncerRegion>(parameters => parameters.AddCascadingValue(announcer));

        var status = region.Find("[role='status']");
        status.GetAttribute("aria-live").ShouldBe("polite");
        status.GetAttribute("aria-atomic").ShouldBe("true");
        status.ClassList.ShouldContain("autonogrammer-sr-only", StringComparer.Ordinal);
        status.TextContent.ShouldBeEmpty();
    }

    /// <summary>After it is disposed the announcer ignores navigations and titles.</summary>
    [Fact]
    public void ADisposedAnnouncerStaysSilent()
    {
        var announcer = new RouteAnnouncer(Navigation);
        announcer.Dispose();

        Navigation.NavigateTo("about");
        announcer.Report("About - Autonogrammer");

        announcer.Text.ShouldBeEmpty();
    }
}
