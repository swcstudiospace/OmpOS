using Autonogrammer.Presentation.Accounts;
using Autonogrammer.UI.Components.Auth;
using Autonogrammer.UI.Components.Shell;
using Autonogrammer.UI.Tests.Auth;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Autonogrammer.UI.Tests.Shell;

/// <summary>The top bar offers Sign out only while a token is held and no account host is registered.</summary>
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
        AssertSignOutSitsBetweenSourceAndTheme(cut);
        cut.FindAll(".autonogrammer-account-menu").ShouldBeEmpty();
    }

    /// <summary>A head that registers an account host draws the account menu in the header end slot and no Sign out button.</summary>
    [Fact]
    public void ARegisteredAccountHostPlacesTheMenuAndHidesSignOut()
    {
        Services.AddSingleton<IAccountHost>(new QuietAccountHost());
        var session = new FakeAuthSession();
        session.SetSignedIn(true);
        Services.AddSingleton<IAuthSession>(session);

        var cut = Render<TopBar>();

        cut.FindAll("header button.autonogrammer-topbar-signout").ShouldBeEmpty();
        cut.Find("header .autonogrammer-account-menu");
        var header = cut.Find("header.autonogrammer-topbar").InnerHtml;
        var source = header.IndexOf("autonogrammer-topbar-link", StringComparison.Ordinal);
        var menu = header.IndexOf("autonogrammer-account-menu", StringComparison.Ordinal);
        var theme = header.IndexOf("autonogrammer-theme-switch", StringComparison.Ordinal);
        source.ShouldBeGreaterThanOrEqualTo(0);
        source.ShouldBeLessThan(menu);
        menu.ShouldBeLessThan(theme);
    }

    private static void AssertSignOutSitsBetweenSourceAndTheme(IRenderedComponent<TopBar> cut)
    {
        var header = cut.Find("header.autonogrammer-topbar").InnerHtml;
        var source = header.IndexOf("autonogrammer-topbar-link", StringComparison.Ordinal);
        var signOut = header.IndexOf("autonogrammer-topbar-signout", StringComparison.Ordinal);
        var theme = header.IndexOf("autonogrammer-theme-switch", StringComparison.Ordinal);
        source.ShouldBeGreaterThanOrEqualTo(0);
        source.ShouldBeLessThan(signOut);
        signOut.ShouldBeLessThan(theme);
    }

    private sealed class QuietAccountHost : IAccountHost
    {
        public event EventHandler? Changed { add { } remove { } }

        public AccountHostState State => AccountHostState.SignedOut;

        public Task SendCodeAsync(string email, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task ResendCodeAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SubmitCodeAsync(string code, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task CancelAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SignOutAsync(bool everywhere, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RememberWithoutSystemProtectionAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public void ChangeEmail()
        {
        }

        public Task RefreshDevicesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
