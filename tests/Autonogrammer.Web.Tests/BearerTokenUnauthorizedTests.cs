using System.Net;
using Autonogrammer.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Autonogrammer.Web.Tests;

/// <summary>
/// The bearer handler is the one place a 401 ends the account session: once, without retrying and without looping. A refusal for an earlier sign-in
/// ends nothing, and the request carries the access token of the sign-in that is current.
/// </summary>
public sealed class BearerTokenUnauthorizedTests
{
    private static HttpClient HttpOver(BearerTokenHandler handler) =>
        new(handler, disposeHandler: false) { BaseAddress = new Uri("http://api.invalid/", UriKind.Absolute) };

    /// <summary>An unauthorized answer to a bearer request ends the account session, and the request carried the account's access token.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task AnUnauthorizedAnswerExpiresTheAccountSession()
    {
        var ct = TestContext.Current.CancellationToken;
        var services = new ServiceCollection().BuildServiceProvider();
        using var authHandler = new AccountTokenHandler("access-token-value");
        using var account = new WebAccountSession(TimeProvider.System, services, authHandler: authHandler);
        account.Apply(OidcConfig(), "https://project.example", "https://web.example/");
        await SignInAsync(account, "123456", ct).ConfigureAwait(true);
        account.HasToken.ShouldBeTrue();

        using var routes = new RoutedHandler();
        routes.OnProblem(HttpMethod.Get, "/account/board", HttpStatusCode.Unauthorized, null, "Unauthorized.", "Sign in.");
        using var handler = new BearerTokenHandler(account, routes, account.Expire);
        using var http = HttpOver(handler);

        _ = await Should.ThrowAsync<ApiProblemException>(() => new OmpApiClient(http).GetBoardAsync(ct)).ConfigureAwait(true);

        account.HasToken.ShouldBeFalse();
        account.SessionEnded.ShouldBeTrue();
        account.State.Error.ShouldBe(Autonogrammer.Contracts.Accounts.AuthErrorKind.AccountRejected);
        routes.Headers[0]["Authorization"].ShouldBe("Bearer access-token-value");
    }

    /// <summary>
    /// A refusal for the token of an earlier sign-in ends nothing. After a sign-out and a second sign-in, a late 401 for the first token leaves the second
    /// sign-in intact; only the token that second sign-in was handed ends it.
    /// </summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task ALateRefusalForAnEarlierSignInDoesNotEndTheCurrentOne()
    {
        var ct = TestContext.Current.CancellationToken;
        var services = new ServiceCollection().BuildServiceProvider();
        using var authHandler = new AccountTokenHandler("access-token-value-1", "access-token-value-2");
        using var account = new WebAccountSession(TimeProvider.System, services, authHandler: authHandler);
        account.Apply(OidcConfig(), "https://project.example", "https://web.example/");

        await SignInAsync(account, "111111", ct).ConfigureAwait(true);
        var firstToken = await account.GetTokenAsync(ct).ConfigureAwait(true);
        await account.SignOutAsync(everywhere: false, ct).ConfigureAwait(true);
        await SignInAsync(account, "222222", ct).ConfigureAwait(true);

        account.Expire(firstToken!);

        account.HasToken.ShouldBeTrue("a refusal for an earlier sign-in must not sign out the current one");
        account.SessionEnded.ShouldBeFalse();
        var secondToken = await account.GetTokenAsync(ct).ConfigureAwait(true);
        secondToken.ShouldBe("access-token-value-2");

        account.Expire(secondToken!);

        account.HasToken.ShouldBeFalse();
        account.SessionEnded.ShouldBeTrue();
    }

    private static Autonogrammer.UI.Components.Auth.AuthConfig OidcConfig() =>
        new(
            Autonogrammer.UI.Components.Auth.AuthConfig.OidcMode,
            null,
            null,
            new Uri("https://project.example"),
            "sb_publishable_test",
            new Autonogrammer.Contracts.Accounts.AuthMethods(true, false));

    private static async Task SignInAsync(WebAccountSession account, string code, CancellationToken cancellationToken)
    {
        await account.SendCodeAsync("person@example.com", cancellationToken).ConfigureAwait(true);
        await account.SubmitCodeAsync(code, cancellationToken).ConfigureAwait(true);
    }

    /// <summary>Answers each sign-in verification with the next access token in its list, so a test can tell one sign-in from the next.</summary>
    private sealed class AccountTokenHandler(params string[] accessTokens) : HttpMessageHandler
    {
        private int _verifications;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            var body = "{}";
            if (path.Contains("verify", StringComparison.Ordinal))
            {
                var accessToken = accessTokens[Math.Min(_verifications, accessTokens.Length - 1)];
                _verifications++;
                body = """{"access_token":"ACCESS_TOKEN","refresh_token":"refresh-token-value","expires_in":3600,"user":{"id":"user-1","email":"person@example.com"}}""".Replace("ACCESS_TOKEN", accessToken, StringComparison.Ordinal);
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
            });
        }
    }
}
