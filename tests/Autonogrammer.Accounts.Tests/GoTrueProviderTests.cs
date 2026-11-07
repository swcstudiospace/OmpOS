using System.Text.Json;
using System.Web;
using Autonogrammer.Accounts.Tests.Support;
using Autonogrammer.Contracts.Accounts;
using Shouldly;
using Xunit;

namespace Autonogrammer.Accounts.Tests;

/// <summary>
/// The provider and wallet calls of the GoTrue client: the PKCE authorize URL for Google and X, the web3 grant body and its CAPTCHA member, and the
/// settings read that decides which providers a sign-in screen offers.
/// </summary>
public sealed class GoTrueProviderTests
{
    private const string Challenge = "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("google")]
    [InlineData("x")]
    public void AuthorizeUriNamesTheProviderTheRedirectAndTheS256Challenge(string provider)
    {
        using AccountsHarness harness = AccountsHarness.Create();
        var redirect = new Uri("https://app.example.org/connect?from=web");

        Uri uri = harness.Client.BuildProviderAuthorizeUri(provider, redirect, Challenge);

        uri.AbsolutePath.ShouldBe("/auth/v1/authorize");
        var query = HttpUtility.ParseQueryString(uri.Query);
        query["provider"].ShouldBe(provider);
        query["redirect_to"].ShouldBe(redirect.AbsoluteUri);
        query["code_challenge"].ShouldBe(Challenge);
        query["code_challenge_method"].ShouldBe("s256");
        query["scopes"].ShouldBeNull();
    }

    [Theory]
    [InlineData("twitter")]
    [InlineData("github")]
    [InlineData("google&code_challenge_method=plain")]
    public void AuthorizeUriRefusesAProviderTheClientDoesNotStart(string provider)
    {
        using AccountsHarness harness = AccountsHarness.Create();

        ArgumentException refusal = Should.Throw<ArgumentException>(() =>
            harness.Client.BuildProviderAuthorizeUri(provider, new Uri("https://app.example.org/connect"), Challenge));
        refusal.ParamName.ShouldBe("provider");
    }

    [Theory]
    [InlineData("http://app.example.org/connect")]
    [InlineData("ftp://app.example.org/connect")]
    [InlineData("javascript:alert(1)")]
    public void AuthorizeUriRefusesARedirectThatIsNotHttpsOrLoopback(string redirect)
    {
        using AccountsHarness harness = AccountsHarness.Create();

        ArgumentException refusal = Should.Throw<ArgumentException>(() =>
            harness.Client.BuildProviderAuthorizeUri("google", new Uri(redirect), Challenge));
        refusal.ParamName.ShouldBe("redirectTo");
    }

    [Theory]
    [InlineData("http://127.0.0.1:53123/cb/nonce")]
    [InlineData("http://localhost:53123/cb/nonce")]
    public void AuthorizeUriAcceptsAnHttpLoopbackRedirectForTheDesktopFlow(string redirect)
    {
        using AccountsHarness harness = AccountsHarness.Create();

        Uri uri = harness.Client.BuildProviderAuthorizeUri("google", new Uri(redirect), Challenge);

        HttpUtility.ParseQueryString(uri.Query)["redirect_to"].ShouldBe(redirect);
    }

    [Fact]
    public async Task Web3SignInSendsTheSignedMessageAndTheEthereumChainOnly()
    {
        using AccountsHarness harness = AccountsHarness.Create();
        const string message = "example.org wants you to sign in with your Ethereum account:\n0x1234567890123456789012345678901234567890";

        AuthResult<GoTrueSession> result = await harness.Client.SignInWithWeb3Async(message, "0xabc", captchaToken: null, Token);

        result.IsSuccess.ShouldBeFalse("the fake has no web3 session, so the call must not report one");
        RecordedRequest sent = harness.Handler.Requests[^1];
        sent.Method.ShouldBe("POST");
        sent.PathIs("token").ShouldBeTrue();
        sent.Query.ShouldContain("grant_type=web3", Case.Sensitive);
        using JsonDocument body = JsonDocument.Parse(sent.Body);
        body.RootElement.GetProperty("message").GetString().ShouldBe(message);
        body.RootElement.GetProperty("signature").GetString().ShouldBe("0xabc");
        body.RootElement.GetProperty("chain").GetString().ShouldBe("ethereum");
        body.RootElement.TryGetProperty("gotrue_meta_security", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Web3SignInCarriesTheCaptchaTokenInGoTrueSecurity()
    {
        using AccountsHarness harness = AccountsHarness.Create();

        _ = await harness.Client.SignInWithWeb3Async("message", "0xabc", "captcha-token-value", Token);

        using JsonDocument body = JsonDocument.Parse(harness.Handler.Requests[^1].Body);
        body.RootElement.GetProperty("gotrue_meta_security").GetProperty("captcha_token").GetString().ShouldBe("captcha-token-value");
    }

    [Fact]
    public async Task Web3CaptchaRefusalIsReportedAsCaptchaRequired()
    {
        using AccountsHarness harness = AccountsHarness.Create();
        harness.Handler.ScriptError("token:web3", 400, "captcha_failed");

        AuthResult<GoTrueSession> result = await harness.Client.SignInWithWeb3Async("message", "0xabc", null, Token);

        result.IsSuccess.ShouldBeFalse();
        result.Failure.Kind.ShouldBe(AuthErrorKind.CaptchaRequired);
    }
}
