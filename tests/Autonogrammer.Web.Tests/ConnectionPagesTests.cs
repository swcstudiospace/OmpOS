using Autonogrammer.UI.Components.Auth;
using Autonogrammer.Web.Services;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Autonogrammer.Web.Tests;

/// <summary>The pages the web head adds outside the workbench: the connect page and the not-found page.</summary>
public sealed class ConnectionPagesTests : BunitContext
{
    private const string FocusInterop = "Blazor._internal.domWrapper.focus";

    /// <summary>The not-found page has one heading that takes the focus, a link home and its own title.</summary>
    [Fact]
    public void TheNotFoundPageHasAHeadingALinkHomeAndATitle()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var head = Render<HeadOutlet>();

        var cut = Render<Autonogrammer.Web.Pages.NotFound>();

        cut.FindAll("h1").Select(static heading => heading.TextContent).ShouldBe(["Page not found"]);
        cut.Find("main#main a[href='/']").TextContent.ShouldBe("Go to Agents");
        JSInterop.Invocations.Count(static call => string.Equals(call.Identifier, FocusInterop, StringComparison.Ordinal)).ShouldBe(1);
        head.WaitForAssertion(() => head.Find("title").TextContent.ShouldBe("Page not found - Autonogrammer"));
    }

    /// <summary>The connect page is the connect view at /connect, with its own title.</summary>
    [Fact]
    public void TheConnectPageShowsTheConnectView()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var session = new OwnerTokenSession();
        using var routes = new RoutedHandler();
        using var http = new HttpClient(routes, disposeHandler: false) { BaseAddress = new Uri("http://api.invalid/", UriKind.Absolute) };
        Services.AddSingleton<IAuthSession>(session);
        Services.AddSingleton<IOwnerTokenConnector>(new OwnerTokenConnector(session, new OmpApiClient(http)));
        var head = Render<HeadOutlet>();

        var cut = Render<Autonogrammer.Web.Pages.Connect>();

        cut.FindAll("h1").Select(static heading => heading.TextContent).ShouldBe([AuthCopy.ConnectHeading]);
        cut.Find("form input[type='password'][autocomplete='current-password']").ShouldNotBeNull();
        head.WaitForAssertion(() => head.Find("title").TextContent.ShouldBe("Connect - Autonogrammer"));
        typeof(Autonogrammer.Web.Pages.Connect).GetCustomAttributes(typeof(RouteAttribute), inherit: false)
            .Cast<RouteAttribute>()
            .Select(static route => route.Template)
            .ShouldBe(["/connect"]);
    }
}
