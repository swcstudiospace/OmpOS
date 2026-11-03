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

    /// <summary>The connect page of a deployment without a Supabase sign-in says so, with its own title, and offers no token field.</summary>
    [Fact]
    public void TheConnectPageSaysSignInIsUnavailableWithoutAnOidcConfiguration()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        using var account = new WebAccountSession(TimeProvider.System, new ServiceCollection().BuildServiceProvider());
        Services.AddSingleton(account);
        Services.AddSingleton<IAuthConfigSource>(new NoAuthConfigSource());
        var head = Render<HeadOutlet>();

        var cut = Render<Autonogrammer.Web.Pages.Connect>();

        cut.WaitForAssertion(() => cut.FindAll("h1").Select(static heading => heading.TextContent).ShouldBe([AuthCopy.OidcHeading]));
        cut.FindAll("input[type='password']").ShouldBeEmpty();
        head.WaitForAssertion(() => head.Find("title").TextContent.ShouldBe("Sign-in not available - Autonogrammer"));
        typeof(Autonogrammer.Web.Pages.Connect).GetCustomAttributes(typeof(RouteAttribute), inherit: false)
            .Cast<RouteAttribute>()
            .Select(static route => route.Template)
            .ShouldBe(["/connect"]);
    }

    /// <summary>Answers no deployment configuration, so the connect page says that sign-in is not available here.</summary>
    private sealed class NoAuthConfigSource : IAuthConfigSource
    {
        public Task<AuthConfig?> LoadAsync(CancellationToken cancellationToken) => Task.FromResult<AuthConfig?>(null);
    }
}
