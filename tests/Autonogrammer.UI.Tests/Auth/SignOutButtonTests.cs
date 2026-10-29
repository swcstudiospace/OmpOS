using Autonogrammer.UI.Components.Auth;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Autonogrammer.UI.Tests.Auth;

/// <summary>The Sign out button of the top bar: visible only while a token is held, it forgets the token and goes to the connect page.</summary>
public sealed class SignOutButtonTests : UiTestContext
{
    /// <summary>A head that registers no session (the Demo, a development API) never shows the button.</summary>
    [Fact]
    public void WithoutASessionServiceNothingShows()
    {
        var cut = Render<SignOutButton>();

        cut.Markup.Trim().ShouldBeEmpty();
    }

    /// <summary>The button shows only while a token is held, and follows the session when it changes.</summary>
    [Fact]
    public void TheButtonFollowsTheSession()
    {
        var session = new FakeAuthSession();
        Services.AddSingleton<IAuthSession>(session);
        var cut = Render<SignOutButton>();
        cut.FindAll("button").ShouldBeEmpty();

        session.SetSignedIn(true);

        cut.WaitForAssertion(() => cut.Find("button").TextContent.ShouldBe(AuthCopy.SignOut));

        session.SetSignedIn(false);

        cut.WaitForAssertion(() => cut.FindAll("button").ShouldBeEmpty());
    }

    /// <summary>Signing out clears the token and navigates to the connect page.</summary>
    [Fact]
    public async Task SigningOutClearsTheTokenAndGoesToTheConnectPage()
    {
        var session = new FakeAuthSession();
        session.SetSignedIn(true);
        Services.AddSingleton<IAuthSession>(session);
        var navigation = Services.GetRequiredService<NavigationManager>();
        var cut = Render<SignOutButton>();

        await cut.Find("button").ClickAsync().ConfigureAwait(true);

        session.ClearCount.ShouldBe(1);
        session.HasToken.ShouldBeFalse();
        navigation.ToBaseRelativePath(navigation.Uri).ShouldBe(AuthRoutes.ConnectPath);
        cut.WaitForAssertion(() => cut.FindAll("button").ShouldBeEmpty());
    }
}
