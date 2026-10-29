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
