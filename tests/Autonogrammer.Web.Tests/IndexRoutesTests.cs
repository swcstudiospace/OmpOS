using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Autonogrammer.Web.Tests;

/// <summary>The workbench page answers every workbench address, over the Demo: one page, so moving between views keeps the workbench.</summary>
public sealed class IndexRoutesTests : BunitContext
{
    /// <summary>The page declares the address of every workbench view, so the router sends them all to it.</summary>
    [Fact]
    public void ThePageDeclaresEveryWorkbenchAddress()
    {
        var templates = typeof(Autonogrammer.Web.Pages.Index).GetCustomAttributes(typeof(RouteAttribute), inherit: false)
            .Cast<RouteAttribute>()
            .Select(static route => route.Template);

        templates.ShouldBe(["/", "/kanban", "/about", "/sessions/{SessionId}", "/settings", "/settings/{SettingsPage}"], ignoreOrder: true);
    }

    /// <summary>A deep link to the Kanban view or to About renders that view, with its one heading, over the Demo.</summary>
    /// <param name="path">The address, relative to the base address.</param>
    /// <param name="heading">The heading of the view.</param>
    [Theory]
    [InlineData("kanban", "Kanban")]
    [InlineData("about", "About Autonogrammer")]
    public void ADeepLinkRendersItsView(string path, string heading)
    {
        using var host = new DemoTestHost();
        DemoPageSetup.Register(Services, host);
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.GetRequiredService<NavigationManager>().NavigateTo(path);

        var cut = Render<Autonogrammer.Web.Pages.Index>();

        cut.WaitForAssertion(() => cut.Find("main h1").TextContent.ShouldBe(heading), TimeSpan.FromSeconds(10));
        cut.FindAll("h1").Count.ShouldBe(1);
        cut.FindAll("header h1").ShouldBeEmpty();
    }
}
