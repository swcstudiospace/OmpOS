using Autonogrammer.UI.Components.Auth;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Autonogrammer.UI.Tests.Auth;

/// <summary>The gate that decides what a browser tab sees before it may use the API: the Demo and a development API pass through, an owner-token API needs a token, and a deployment that signs in with OIDC explains itself.</summary>
public sealed class AuthGateTests : UiTestContext
{
    private const string Content = "<p id=\"content\">the routed page</p>";

    private readonly FakeAuthSession _session = new();
    private readonly FakeAuthConfigSource _config = new();

    /// <summary>Initializes a new instance of the <see cref="AuthGateTests"/> class.</summary>
    public AuthGateTests()
    {
        Services.AddSingleton<IAuthSession>(_session);
        Services.AddSingleton<IAuthConfigSource>(_config);
    }

    private NavigationManager Navigation => Services.GetRequiredService<NavigationManager>();

    private IRenderedComponent<AuthGate> RenderGate(bool isDemo = false) => Render<AuthGate>(parameters => parameters
        .Add(p => p.IsDemo, isDemo)
        .AddChildContent(Content));

    /// <summary>A Demo build has no API and no sign-in: the content shows at once and the configuration is never loaded.</summary>
    [Fact]
    public void TheDemoPassesThroughWithoutLoadingTheConfiguration()
    {
        var cut = RenderGate(isDemo: true);

        cut.Find("#content").TextContent.ShouldBe("the routed page");
        _config.Loads.ShouldBe(0);
    }

    /// <summary>While the configuration loads, the gate says so in a status and shows no content.</summary>
    [Fact]
    public void WhileTheConfigurationLoadsTheGateShowsAStatus()
    {
        var pending = _config.EnqueuePending();

        var cut = RenderGate();

        cut.Find("[role='status']").TextContent.ShouldBe(AuthCopy.Loading);
        cut.FindAll("#content").ShouldBeEmpty();
        cut.FindAll("h1").Count.ShouldBe(1);

        pending.SetResult(new AuthConfig(AuthConfig.DevelopmentMode, null, null));
        cut.WaitForAssertion(() => cut.Find("#content").ShouldNotBeNull());
    }

    /// <summary>A development API needs no credential: the content shows and nobody is sent to the connect page.</summary>
    [Fact]
    public void ADevelopmentApiPassesThrough()
    {
        _config.Enqueue(new AuthConfig(AuthConfig.DevelopmentMode, null, null));

        var cut = RenderGate();

        cut.WaitForAssertion(() => cut.Find("#content").ShouldNotBeNull());
        Navigation.ToBaseRelativePath(Navigation.Uri).ShouldBeEmpty();
    }

    /// <summary>An owner-token API with no token sends the tab to the connect page and remembers the page it asked for, in memory only.</summary>
    [Fact]
    public void AnOwnerTokenApiWithoutATokenRedirectsToConnectAndRemembersThePath()
    {
        Navigation.NavigateTo("sessions/abc?x=1");
        _config.Enqueue(new AuthConfig(AuthConfig.OwnerTokenMode, null, null));

        var cut = RenderGate();

        cut.WaitForAssertion(() => Navigation.ToBaseRelativePath(Navigation.Uri).ShouldBe(AuthRoutes.ConnectPath));
        _session.ReturnPath.ShouldBe("sessions/abc?x=1");
        cut.FindAll("#content").ShouldBeEmpty();
    }

    /// <summary>The connect page itself is never gated, or nobody could sign in.</summary>
    [Fact]
    public void TheConnectPageIsReachableWithoutAToken()
    {
        Navigation.NavigateTo(AuthRoutes.ConnectPath);
        _config.Enqueue(new AuthConfig(AuthConfig.OwnerTokenMode, null, null));

        var cut = RenderGate();

        cut.WaitForAssertion(() => cut.Find("#content").ShouldNotBeNull());
        _session.ReturnPath.ShouldBeNull();
    }

    /// <summary>An owner-token API with a token shows the content, and losing the token (sign-out, a refused token) sends the tab to the connect page.</summary>
    [Fact]
    public void AnOwnerTokenApiWithATokenShowsTheContentUntilTheTokenIsLost()
    {
        _session.SetSignedIn(true);
        _config.Enqueue(new AuthConfig(AuthConfig.OwnerTokenMode, null, null));

        var cut = RenderGate();

        cut.WaitForAssertion(() => cut.Find("#content").ShouldNotBeNull());
        Navigation.ToBaseRelativePath(Navigation.Uri).ShouldBeEmpty();

        _session.SetSignedIn(false);

        cut.WaitForAssertion(() => Navigation.ToBaseRelativePath(Navigation.Uri).ShouldBe(AuthRoutes.ConnectPath));
        cut.FindAll("#content").ShouldBeEmpty();
    }

    /// <summary>A deployment that signs in with OIDC gets an explanation that is information, not an error: no alert, no content, one heading.</summary>
    [Fact]
    public void AnOidcDeploymentIsExplainedWithoutAnError()
    {
        _config.Enqueue(new AuthConfig(AuthConfig.OidcMode, "https://issuer.example", "autonogrammer"));

        var cut = RenderGate();

        cut.WaitForAssertion(() => cut.Find("h1").TextContent.ShouldBe(AuthCopy.OidcHeading));
        cut.Find("main").TextContent.ShouldContain("not available in this web build", Case.Sensitive);
        cut.FindAll("[role='alert']").ShouldBeEmpty();
        cut.FindAll("#content").ShouldBeEmpty();
        cut.FindAll("h1").Count.ShouldBe(1);
    }

    /// <summary>An unreachable API shows an alert with a Retry button instead of a blank page, and Retry loads the configuration again.</summary>
    [Fact]
    public async Task AnUnreachableApiShowsAnAlertAndRetryLoadsAgain()
    {
        _config.Enqueue(null);
        _config.Enqueue(new AuthConfig(AuthConfig.DevelopmentMode, null, null));

        var cut = RenderGate();

        await cut.WaitForAssertionAsync(() => cut.Find("[role='alert']").TextContent.ShouldBe(AuthCopy.UnavailableBody)).ConfigureAwait(true);
        cut.Find("h1").TextContent.ShouldBe(AuthCopy.UnavailableHeading);
        var retry = cut.Find("button");
        retry.TextContent.ShouldBe(AuthCopy.Retry);

        await retry.ClickAsync().ConfigureAwait(true);

        await cut.WaitForAssertionAsync(() => cut.Find("#content").ShouldNotBeNull()).ConfigureAwait(true);
        _config.Loads.ShouldBe(2);
    }

    /// <summary>A mode this client does not know is treated like an unreachable API: it never lets the content through.</summary>
    [Fact]
    public void AnUnknownModeNeverLetsTheContentThrough()
    {
        _config.Enqueue(new AuthConfig("saml", null, null));

        var cut = RenderGate();

        cut.WaitForAssertion(() => cut.Find("[role='alert']").ShouldNotBeNull());
        cut.FindAll("#content").ShouldBeEmpty();
    }
}
