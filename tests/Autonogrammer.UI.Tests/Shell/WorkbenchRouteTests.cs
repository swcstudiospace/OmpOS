using Autonogrammer.UI.Components.Shell;
using Shouldly;
using Xunit;

namespace Autonogrammer.UI.Tests.Shell;

/// <summary>The addresses of the workbench shell: which paths it shows, how it reads them, and how it writes them back.</summary>
public sealed class WorkbenchRouteTests
{
    /// <summary>Every address the shell shows parses to its route, ignoring a query, a fragment, a trailing slash and letter case of the fixed words.</summary>
    [Theory]
    [InlineData("", WorkbenchViewKind.Agents, null, null)]
    [InlineData("/", WorkbenchViewKind.Agents, null, null)]
    [InlineData("kanban", WorkbenchViewKind.Kanban, null, null)]
    [InlineData("Kanban/", WorkbenchViewKind.Kanban, null, null)]
    [InlineData("about?x=1", WorkbenchViewKind.About, null, null)]
    [InlineData("sessions/abc", WorkbenchViewKind.Session, "abc", null)]
    [InlineData("sessions/a%20b#top", WorkbenchViewKind.Session, "a b", null)]
    [InlineData("settings", WorkbenchViewKind.Settings, null, null)]
    [InlineData("settings/models", WorkbenchViewKind.Settings, null, "models")]
    public void KnownPathsParse(string path, WorkbenchViewKind kind, string? sessionId, string? settingsPage)
    {
        WorkbenchRoute.TryParse(path, out var route).ShouldBeTrue();

        route.ShouldNotBeNull();
        route.Kind.ShouldBe(kind);
        route.SessionId.ShouldBe(sessionId);
        if (kind == WorkbenchViewKind.Settings)
        {
            route.SettingsPage.ShouldBe(settingsPage);
        }
    }

    /// <summary>A path that is not a workbench address (the connect page, an unknown page, a session without an id) does not parse.</summary>
    [Theory]
    [InlineData("connect")]
    [InlineData("sessions")]
    [InlineData("sessions/a/b")]
    [InlineData("kanban/extra")]
    [InlineData("nothing-here")]
    public void OtherPathsDoNotParse(string path)
    {
        WorkbenchRoute.TryParse(path, out var route).ShouldBeFalse();

        route.ShouldBeNull();
    }

    /// <summary>A route writes back to the path that parses to it, with a session id escaped.</summary>
    [Fact]
    public void RoutesWriteBackToTheirPath()
    {
        WorkbenchRoute.Agents.RelativePath.ShouldBeEmpty();
        WorkbenchRoute.Kanban.RelativePath.ShouldBe("kanban");
        WorkbenchRoute.About.RelativePath.ShouldBe("about");
        WorkbenchRoute.ForSettings(null).RelativePath.ShouldBe("settings");
        WorkbenchRoute.ForSettings("models").RelativePath.ShouldBe("settings/models");
        WorkbenchRoute.ForSession("a b/c").RelativePath.ShouldBe("sessions/a%20b%2Fc");

        foreach (var route in new[] { WorkbenchRoute.Agents, WorkbenchRoute.Kanban, WorkbenchRoute.About, WorkbenchRoute.ForSession("a b"), WorkbenchRoute.ForSettings("models") })
        {
            WorkbenchRoute.TryParse(route.RelativePath, out var parsed).ShouldBeTrue();
            parsed.ShouldBe(route);
        }
    }

    /// <summary>Titles list the most specific part first and end with the product.</summary>
    [Fact]
    public void TitlesEndWithTheProduct()
    {
        ViewTitles.Compose("Kanban").ShouldBe("Kanban - Autonogrammer");
        ViewTitles.Compose("Alpha", "Agents").ShouldBe("Alpha - Agents - Autonogrammer");
        ViewTitles.Compose().ShouldBe("Autonogrammer");
    }
}
