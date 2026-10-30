using Autonogrammer.UI.Components.Auth;
using Autonogrammer.UI.Components.Shell;
using Autonogrammer.UI.Tests.Auth;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Autonogrammer.UI.Tests.Shell;

/// <summary>The top bar offers Sign out only while a token is held.</summary>
public sealed class TopBarSignOutTests : UiTestContext
{
    /// <summary>A head with no sign-in at all (the Demo, a development API) registers no session, and its bar shows no Sign out button.</summary>
    [Fact]
    public void AHeadWithoutSignInHasNoSignOut() =>
        Render<TopBar>().FindAll(".autonogrammer-topbar-signout").ShouldBeEmpty();

    /// <summary>A signed-out tab shows no Sign out button.</summary>
    [Fact]
    public void WithoutATokenTheBarHasNoSignOut()
    {
        Services.AddSingleton<IAuthSession>(new FakeAuthSession());

        Render<TopBar>().FindAll(".autonogrammer-topbar-signout").ShouldBeEmpty();
    }

    /// <summary>With a token the bar shows one Sign out button named by its text, between the source link and the theme switch.</summary>
    [Fact]
    public void WithATokenTheBarShowsSignOut()
    {
        var session = new FakeAuthSession();
        session.SetSignedIn(true);
        Services.AddSingleton<IAuthSession>(session);

        var cut = Render<TopBar>();

        var button = cut.Find("header button.autonogrammer-topbar-signout");
        button.TextContent.ShouldBe(AuthCopy.SignOut);
        button.GetAttribute("type").ShouldBe("button");
    }
}
