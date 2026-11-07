using System.Buffers.Text;
using System.Text;
using Autonogrammer.Accounts.Tests.Support;
using Autonogrammer.Contracts.Accounts;
using Autonogrammer.Harness.Secrets;
using Shouldly;
using Xunit;

namespace Autonogrammer.Accounts.Tests;

/// <summary>
/// Verifies the options validation and the six GoTrue calls against the in-process fake: round trips, headers, the S256 exchange, refresh rotation and the
/// reuse interval, sign-out scopes, and the mapping of every service answer to a closed failure kind without server text.
/// </summary>
public sealed class GoTrueClientTests
{
    private const string Email = "person@example.org";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string Jwt(string role) =>
        Base64Url.EncodeToString("""{"alg":"HS256","typ":"JWT"}"""u8) + "." + Base64Url.EncodeToString(Encoding.UTF8.GetBytes($$"""{"role":"{{role}}","iss":"supabase"}""")) + ".signature";

    [Theory]
    [InlineData("sb_secret_abcdef0123456789")]
    [InlineData(" sb_secret_abcdef0123456789")]
    [InlineData("\tsb_secret_abcdef0123456789\n")]
    [InlineData("SB_SECRET_abcdef0123456789")]
    public void OptionsRefuseASecretKeyNamingTheFieldNotTheValue(string key)
    {
        GoTrueOptionsResult result = GoTrueOptions.Create(FakeGoTrueHandler.ProjectUrl, key);

        result.IsValid.ShouldBeFalse();
        result.Options.ShouldBeNull();
        result.Field.ShouldBe("PublishableKey");
        result.Message.ShouldNotBeNull();
        result.Message.ShouldNotContain(key);
    }

    [Fact]
    public void OptionsRefuseAServiceRoleJwtNamingTheFieldNotTheValue()
    {
        string key = Jwt("service_role");

        GoTrueOptionsResult result = GoTrueOptions.Create(FakeGoTrueHandler.ProjectUrl, key);

        result.IsValid.ShouldBeFalse();
        result.Field.ShouldBe("PublishableKey");
        result.Message!.ShouldNotContain(key);
        result.Message!.ShouldNotContain("service_role");
    }

    [Fact]
    public void OptionsRefuseAServiceRoleJwtWithSurroundingWhitespaceOrPaddedPayload()
    {
        string unpadded = Jwt("service_role");
        string[] parts = unpadded.Split('.');
        string padded = parts[0] + "." + parts[1] + new string('=', (4 - (parts[1].Length % 4)) % 4) + "." + parts[2];

        foreach (string key in new[] { " " + unpadded, unpadded + "\n", padded, "\t" + padded + " " })
        {
            GoTrueOptionsResult result = GoTrueOptions.Create(FakeGoTrueHandler.ProjectUrl, key);

            result.IsValid.ShouldBeFalse();
            result.Field.ShouldBe("PublishableKey");
        }
    }

    [Theory]
    [InlineData("anon")]
    [InlineData("authenticated")]
    public void OptionsAcceptAPublishableOrAnonKey(string role)
    {
        GoTrueOptions.Create(FakeGoTrueHandler.ProjectUrl, Jwt(role)).IsValid.ShouldBeTrue();
        GoTrueOptions.Create(FakeGoTrueHandler.ProjectUrl, "sb_publishable_abc").IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("http://project.example.org")]
    [InlineData("https://user:pass@project.example.org")]
    [InlineData("https://project.example.org/?apikey=x")]
    [InlineData("https://project.example.org/#frag")]
    [InlineData("project.example.org")]
    [InlineData("")]
    public void OptionsRefuseAProjectUrlThatIsNotPlainAbsoluteHttps(string address)
    {
        GoTrueOptionsResult result = GoTrueOptions.Create(address, "sb_publishable_abc");

        result.IsValid.ShouldBeFalse();
        result.Field.ShouldBe("ProjectUrl");
        if (address.Length > 0)
        {
            result.Message!.ShouldNotContain(address);
        }
    }

    [Fact]
    public void OptionsDeriveTheIssuerAndTheAuthBaseFromTheProjectUrl()
    {
        GoTrueOptions options = GoTrueOptions.Create("https://xpfoidcvbwmpbwodjvqb.supabase.co/", "sb_publishable_abc").Options!;

        options.Issuer.ShouldBe("https://xpfoidcvbwmpbwodjvqb.supabase.co/auth/v1");
        options.AuthBaseUri.AbsoluteUri.ShouldBe("https://xpfoidcvbwmpbwodjvqb.supabase.co/auth/v1/");
        options.MagicLinkEnabled.ShouldBeFalse();
        options.EmailCodeEnabled.ShouldBeTrue();
    }

    [Fact]
    public void OptionsFromAConfigRefuseMagicLinkWithoutEmailCode()
    {
        GoTrueOptionsResult result = GoTrueOptions.Create(new AuthConfigResponse(
            "oidc", null, null, new Uri(FakeGoTrueHandler.ProjectUrl), "sb_publishable_abc", new AuthMethods(EmailCode: false, MagicLink: true, Providers: [], Wallet: false), null, null));

        result.IsValid.ShouldBeFalse();
        result.Field.ShouldBe("Methods.MagicLink");
    }

    [Theory]
    [InlineData("12345678", "12345678")]
    [InlineData("1234 5678", "12345678")]
    [InlineData("1234-5678", "12345678")]
    [InlineData(" 123 456 ", "123456")]
    [InlineData("1234567890", "1234567890")]
    [InlineData("12345", null)]
    [InlineData("12345678901", null)]
    [InlineData("1234abcd", null)]
    [InlineData("", null)]
    public void CodesAreStrippedAndAcceptedAtSixToTenDigits(string typed, string? expected)
    {
        GoTrueClient.NormalizeCode(typed).ShouldBe(expected);
    }

    [Fact]
    public async Task IssueAndVerifyRoundTripReturnsASessionForAStrippedCode()
    {
        using var harness = AccountsHarness.Create();
        harness.Handler.ConfirmSignup(Email);

        AuthResult<AuthUnit> sent = await harness.Client.SendEmailOtpAsync(Email, null, null, Token).ConfigureAwait(true);
        sent.IsSuccess.ShouldBeTrue();
        string code = harness.Handler.LastCode(Email);
        string typed = code[..4] + " - " + code[4..];

        AuthResult<GoTrueSession> verified = await harness.Client.VerifyEmailOtpAsync(Email, typed, Token).ConfigureAwait(true);

        verified.IsSuccess.ShouldBeTrue();
        verified.Value.Email.ShouldBe(Email);
        verified.Value.AccessToken.Reveal().ShouldStartWith("access-");
        verified.Value.RefreshToken.Reveal().ShouldStartWith("refresh-");
        verified.Value.Lifetime.ShouldBe(TimeSpan.FromHours(1));
        verified.Value.ExpiresAt.ShouldBe(harness.Time.GetUtcNow() + TimeSpan.FromHours(1));
        verified.Value.ToString().ShouldNotContain(verified.Value.RefreshToken.Reveal());
    }

    [Fact]
    public async Task AWrongCodeIsRefusedAsCodeInvalidOrExpiredAndAMalformedOneSendsNothing()
    {
        using var harness = AccountsHarness.Create();
        harness.Handler.ConfirmSignup(Email);
        _ = await harness.Client.SendEmailOtpAsync(Email, null, null, Token).ConfigureAwait(true);

        AuthResult<GoTrueSession> wrong = await harness.Client.VerifyEmailOtpAsync(Email, "00000000", Token).ConfigureAwait(true);
        int before = harness.Handler.Count("verify");
        AuthResult<GoTrueSession> malformed = await harness.Client.VerifyEmailOtpAsync(Email, "12ab", Token).ConfigureAwait(true);

        wrong.IsSuccess.ShouldBeFalse();
        wrong.Failure.Kind.ShouldBe(AuthErrorKind.CodeInvalidOrExpired);
        wrong.Failure.ServerCode.ShouldBe("otp_expired");
        wrong.Failure.HttpStatus.ShouldBe(403);
        malformed.Failure.Kind.ShouldBe(AuthErrorKind.CodeInvalidOrExpired);
        harness.Handler.Count("verify").ShouldBe(before);
    }

    [Fact]
    public async Task ACodeExpiresWithTheClock()
    {
        using var harness = AccountsHarness.Create();
        harness.Handler.ConfirmSignup(Email);
        _ = await harness.Client.SendEmailOtpAsync(Email, null, null, Token).ConfigureAwait(true);
        string code = harness.Handler.LastCode(Email);
        harness.Time.Advance(TimeSpan.FromHours(1));

        AuthResult<GoTrueSession> verified = await harness.Client.VerifyEmailOtpAsync(Email, code, Token).ConfigureAwait(true);

        verified.Failure.Kind.ShouldBe(AuthErrorKind.CodeInvalidOrExpired);
    }

    [Fact]
    public async Task EveryRequestSendsTheApiKeyAndOnlyUserCallsSendABearerThatIsNeverTheApiKey()
    {
        using var harness = AccountsHarness.Create();
        GoTrueSession session = harness.Handler.IssueSession(Email);

        _ = await harness.Client.SendEmailOtpAsync(Email, null, null, Token).ConfigureAwait(true);
        _ = await harness.Client.RefreshAsync(session.RefreshToken, Token).ConfigureAwait(true);
        _ = await harness.Client.GetUserAsync(session.AccessToken, Token).ConfigureAwait(true);
        _ = await harness.Client.SignOutAsync(session.AccessToken, SignOutScope.Local, Token).ConfigureAwait(true);

        string accessToken = session.AccessToken.Reveal();
        harness.Handler.Requests.All(request => string.Equals(request.ApiKey, FakeGoTrueHandler.PublishableKey, StringComparison.Ordinal)).ShouldBeTrue();
        harness.Handler.Requests.Where(request => request.Path is "otp" or "token").All(request => request.Bearer is null).ShouldBeTrue();
        harness.Handler.Requests.Where(request => request.Path is "user" or "logout").All(request => string.Equals(request.Bearer, accessToken, StringComparison.Ordinal)).ShouldBeTrue();
        harness.Handler.Requests.Any(request => string.Equals(request.Bearer, FakeGoTrueHandler.PublishableKey, StringComparison.Ordinal)).ShouldBeFalse();
    }

    [Fact]
    public async Task APlainOtpRequestCarriesNoChallengeAndNoRedirect()
    {
        using var harness = AccountsHarness.Create();

        _ = await harness.Client.SendEmailOtpAsync(Email, null, null, Token).ConfigureAwait(true);

        RecordedRequest request = harness.Handler.Requests.Single();
        request.Body.ShouldContain("\"create_user\":true");
        request.Body.ShouldNotContain("code_challenge");
        request.Query.ShouldBeEmpty();
    }

    [Fact]
    public async Task ALinkRequestCarriesTheS256ChallengeAndTheRedirect()
    {
        using var harness = AccountsHarness.Create(magicLink: true);
        SecretString verifier = Pkce.CreateVerifier();
        string challenge = Pkce.ComputeChallenge(verifier);
        var redirect = new Uri("http://127.0.0.1:50123/cb/abc");

        _ = await harness.Client.SendEmailOtpAsync(Email, challenge, redirect, Token).ConfigureAwait(true);

        RecordedRequest request = harness.Handler.Requests.Single();
        request.Body.ShouldContain($"\"code_challenge\":\"{challenge}\"");
        request.Body.ShouldContain("\"code_challenge_method\":\"s256\"");
        request.Query.ShouldBe("redirect_to=" + Uri.EscapeDataString(redirect.AbsoluteUri));
        harness.Handler.Outbox.Single().RedirectTo.ShouldBe(redirect.AbsoluteUri);
        request.Body.ShouldNotContain(verifier.Reveal());
    }

    [Fact]
    public async Task AChallengeWithoutARedirectIsAProgrammingError()
    {
        using var harness = AccountsHarness.Create(magicLink: true);

        await Should.ThrowAsync<ArgumentException>(() => harness.Client.SendEmailOtpAsync(Email, "challenge", null, Token)).ConfigureAwait(true);
    }

    [Fact]
    public async Task TheCodeExchangeChecksTheS256VerifierAndWorksOnce()
    {
        using var harness = AccountsHarness.Create(magicLink: true);
        SecretString verifier = Pkce.CreateVerifier();
        _ = await harness.Client.SendEmailOtpAsync(Email, Pkce.ComputeChallenge(verifier), new Uri("http://127.0.0.1:50123/cb/n"), Token).ConfigureAwait(true);
        string authCode = harness.Handler.FollowLink(Email);

        AuthResult<GoTrueSession> wrong = await harness.Client.ExchangeCodeAsync(new SecretString(authCode), Pkce.CreateVerifier(), Token).ConfigureAwait(true);
        AuthResult<GoTrueSession> right = await harness.Client.ExchangeCodeAsync(new SecretString(authCode), verifier, Token).ConfigureAwait(true);
        AuthResult<GoTrueSession> again = await harness.Client.ExchangeCodeAsync(new SecretString(authCode), verifier, Token).ConfigureAwait(true);

        wrong.Failure.Kind.ShouldBe(AuthErrorKind.LinkInvalidOrExpired);
        wrong.Failure.ServerCode.ShouldBe("bad_code_verifier");
        right.IsSuccess.ShouldBeTrue();
        again.Failure.Kind.ShouldBe(AuthErrorKind.LinkInvalidOrExpired);
        again.Failure.ServerCode.ShouldBe("flow_state_not_found");
    }

    [Fact]
    public void ThePkceMaterialHasTheDocumentedShape()
    {
        SecretString verifier = Pkce.CreateVerifier();

        verifier.Reveal().Length.ShouldBe(86);
        verifier.Reveal().ShouldMatch("^[A-Za-z0-9_-]+$");
        Pkce.ComputeChallenge(verifier).ShouldMatch("^[A-Za-z0-9_-]{43}$");
        Pkce.ComputeChallenge(verifier).ShouldBe(Pkce.ComputeChallenge(verifier));
        string.Equals(Pkce.CreateVerifier().Reveal(), verifier.Reveal(), StringComparison.Ordinal).ShouldBeFalse();
        string.Equals(Pkce.CreateNonce(), Pkce.CreateNonce(), StringComparison.Ordinal).ShouldBeFalse();
        // RFC 7636 appendix B: the S256 challenge of the sample verifier.
        Pkce.ComputeChallenge(new SecretString("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk")).ShouldBe("E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM");
    }

    [Fact]
    public async Task RefreshRotatesAndTheReuseIntervalAllowsOnlyTenSeconds()
    {
        using var harness = AccountsHarness.Create();
        GoTrueSession session = harness.Handler.IssueSession(Email);

        AuthResult<GoTrueSession> first = await harness.Client.RefreshAsync(session.RefreshToken, Token).ConfigureAwait(true);
        harness.Time.Advance(TimeSpan.FromSeconds(9));
        AuthResult<GoTrueSession> insideInterval = await harness.Client.RefreshAsync(session.RefreshToken, Token).ConfigureAwait(true);
        harness.Time.Advance(TimeSpan.FromSeconds(2));
        AuthResult<GoTrueSession> outsideInterval = await harness.Client.RefreshAsync(session.RefreshToken, Token).ConfigureAwait(true);

        first.IsSuccess.ShouldBeTrue();
        string.Equals(first.Value.RefreshToken.Reveal(), session.RefreshToken.Reveal(), StringComparison.Ordinal).ShouldBeFalse();
        insideInterval.IsSuccess.ShouldBeTrue();
        insideInterval.Value.RefreshToken.Reveal().ShouldBe(first.Value.RefreshToken.Reveal());
        outsideInterval.Failure.Kind.ShouldBe(AuthErrorKind.SessionEnded);
        outsideInterval.Failure.ServerCode.ShouldBe("refresh_token_already_used");
        harness.Handler.LiveSessions(Email).ShouldBe(0);
    }

    [Fact]
    public async Task AnUnknownRefreshTokenIsSessionEnded()
    {
        using var harness = AccountsHarness.Create();

        AuthResult<GoTrueSession> result = await harness.Client.RefreshAsync(new SecretString("never-issued"), Token).ConfigureAwait(true);

        result.Failure.Kind.ShouldBe(AuthErrorKind.SessionEnded);
        result.Failure.ServerCode.ShouldBe("refresh_token_not_found");
    }

    [Theory]
    [InlineData(SignOutScope.Local, "scope=local")]
    [InlineData(SignOutScope.Global, "scope=global")]
    [InlineData(SignOutScope.Others, "scope=others")]
    public async Task SignOutIsScopeAwareAndTheScopeIsAlwaysSent(SignOutScope scope, string expectedQuery)
    {
        using var harness = AccountsHarness.Create();
        GoTrueSession caller = harness.Handler.IssueSession(Email);
        GoTrueSession other = harness.Handler.IssueSession(Email);
        harness.Handler.LiveSessions(Email).ShouldBe(2);

        AuthResult<AuthUnit> result = await harness.Client.SignOutAsync(caller.AccessToken, scope, Token).ConfigureAwait(true);

        result.IsSuccess.ShouldBeTrue();
        harness.Handler.Requests.Single(request => string.Equals(request.Path, "logout", StringComparison.Ordinal)).Query.ShouldBe(expectedQuery);
        harness.Handler.LiveSessions(Email).ShouldBe(scope == SignOutScope.Global ? 0 : 1);
        harness.Handler.IsAccessTokenLive(caller.AccessToken.Reveal()).ShouldBe(scope == SignOutScope.Others);
        harness.Handler.IsAccessTokenLive(other.AccessToken.Reveal()).ShouldBe(scope == SignOutScope.Local);
    }

    [Fact]
    public async Task SignOutWithAnExpiredTokenIsSessionEnded()
    {
        using var harness = AccountsHarness.Create();
        GoTrueSession session = harness.Handler.IssueSession(Email);
        harness.Time.Advance(TimeSpan.FromHours(2));

        AuthResult<AuthUnit> result = await harness.Client.SignOutAsync(session.AccessToken, SignOutScope.Local, Token).ConfigureAwait(true);

        result.Failure.Kind.ShouldBe(AuthErrorKind.SessionEnded);
    }

    [Fact]
    public async Task GetUserReturnsTheIdentityBehindATokenAndRefusesTheApiKeyAsBearer()
    {
        using var harness = AccountsHarness.Create();
        GoTrueSession session = harness.Handler.IssueSession(Email);

        AuthResult<GoTrueUser> user = await harness.Client.GetUserAsync(session.AccessToken, Token).ConfigureAwait(true);
        AuthResult<GoTrueUser> asApiKey = await harness.Client.GetUserAsync(new SecretString(FakeGoTrueHandler.PublishableKey), Token).ConfigureAwait(true);

        user.Value.UserId.ShouldBe(session.UserId);
        user.Value.Email.ShouldBe(Email);
        asApiKey.IsSuccess.ShouldBeFalse();
    }

    [Theory]
    [InlineData("otp", 429, "over_email_send_rate_limit", AuthErrorKind.EmailRateLimited, "over_email_send_rate_limit")]
    [InlineData("otp", 429, "over_request_rate_limit", AuthErrorKind.TooManyRequests, "over_request_rate_limit")]
    [InlineData("otp", 429, null, AuthErrorKind.TooManyRequests, null)]
    [InlineData("otp", 400, "validation_failed", AuthErrorKind.EmailInvalid, "validation_failed")]
    [InlineData("otp", 400, "email_address_invalid", AuthErrorKind.EmailInvalid, "email_address_invalid")]
    [InlineData("otp", 422, "signup_disabled", AuthErrorKind.SignUpsClosed, "signup_disabled")]
    [InlineData("otp", 400, "signup_disabled", AuthErrorKind.SignUpsClosed, "signup_disabled")]
    [InlineData("otp", 400, "captcha_failed", AuthErrorKind.CaptchaRequired, "captcha_failed")]
    [InlineData("otp", 400, "otp_disabled", AuthErrorKind.MethodUnavailable, "otp_disabled")]
    [InlineData("otp", 422, "email_provider_disabled", AuthErrorKind.MethodUnavailable, null)]
    [InlineData("otp", 403, "feature_disabled", AuthErrorKind.MethodUnavailable, null)]
    [InlineData("otp", 403, "user_banned", AuthErrorKind.AccountBlocked, "user_banned")]
    [InlineData("otp", 500, null, AuthErrorKind.ServiceUnavailable, null)]
    [InlineData("otp", 503, "hook_timeout", AuthErrorKind.ServiceUnavailable, null)]
    [InlineData("otp", 418, "something_new", AuthErrorKind.Unexpected, null)]
    [InlineData("verify", 403, "otp_expired", AuthErrorKind.CodeInvalidOrExpired, "otp_expired")]
    [InlineData("token:pkce", 400, "bad_code_verifier", AuthErrorKind.LinkInvalidOrExpired, "bad_code_verifier")]
    [InlineData("token:pkce", 404, "flow_state_not_found", AuthErrorKind.LinkInvalidOrExpired, "flow_state_not_found")]
    [InlineData("token:pkce", 403, "flow_state_expired", AuthErrorKind.LinkInvalidOrExpired, "flow_state_expired")]
    [InlineData("token:pkce", 400, "validation_failed", AuthErrorKind.LinkInvalidOrExpired, "validation_failed")]
    [InlineData("token:refresh", 400, "refresh_token_not_found", AuthErrorKind.SessionEnded, "refresh_token_not_found")]
    [InlineData("token:refresh", 400, "refresh_token_already_used", AuthErrorKind.SessionEnded, "refresh_token_already_used")]
    [InlineData("token:refresh", 400, "session_not_found", AuthErrorKind.SessionEnded, "session_not_found")]
    [InlineData("token:refresh", 403, "session_expired", AuthErrorKind.SessionEnded, "session_expired")]
    [InlineData("token:refresh", 403, "user_banned", AuthErrorKind.AccountBlocked, "user_banned")]
    [InlineData("token:refresh", 502, null, AuthErrorKind.ServiceUnavailable, null)]
    [InlineData("logout", 401, "bad_jwt", AuthErrorKind.SessionEnded, null)]
    [InlineData("user", 403, null, AuthErrorKind.SessionEnded, null)]
    public async Task EveryServiceAnswerMapsToOneClosedKindWithAFixedServerCodeAndNoServerText(
        string route,
        int status,
        string? errorCode,
        AuthErrorKind expected,
        string? expectedServerCode)
    {
        using var harness = AccountsHarness.Create();
        harness.Handler.ScriptError(route, status, errorCode);

        AuthFailure failure = await CallAsync(harness, route).ConfigureAwait(true);

        failure.Kind.ShouldBe(expected);
        failure.ServerCode.ShouldBe(expectedServerCode);
        failure.HttpStatus.ShouldBe(status);
        failure.ToString().ShouldNotContain(FakeGoTrueHandler.ServerMessageText);
    }

    [Fact]
    public async Task ARetryAfterHeaderIsReadAndItsAbsenceIsNull()
    {
        using var harness = AccountsHarness.Create();
        harness.Handler.ScriptError("otp", 429, "over_request_rate_limit", retryAfterSeconds: 37);
        harness.Handler.ScriptError("otp", 429, "over_request_rate_limit");

        AuthFailure with = await CallAsync(harness, "otp").ConfigureAwait(true);
        AuthFailure without = await CallAsync(harness, "otp").ConfigureAwait(true);

        with.RetryAfter.ShouldBe(TimeSpan.FromSeconds(37));
        without.RetryAfter.ShouldBeNull();
    }

    [Fact]
    public async Task ADroppedConnectionIsNetworkUnavailableAndOtpIsNotRetried()
    {
        using var harness = AccountsHarness.Create();
        harness.Handler.ScriptTransportFailure("otp", times: 2);

        AuthFailure failure = await CallAsync(harness, "otp").ConfigureAwait(true);

        failure.Kind.ShouldBe(AuthErrorKind.NetworkUnavailable);
        failure.HttpStatus.ShouldBeNull();
        harness.Handler.Count("otp").ShouldBe(1);
    }

    [Fact]
    public async Task ARequestThatHangsEndsAfterFifteenSecondsOnTheClockAsNetworkUnavailable()
    {
        using var harness = AccountsHarness.Create();
        GoTrueSession session = harness.Handler.IssueSession(Email);
        harness.Handler.HoldRefresh();

        Task<AuthResult<GoTrueSession>> pending = harness.Client.RefreshAsync(session.RefreshToken, Token);
        await TimeDriver.UntilAsync(() => harness.Handler.Count("token:refresh") == 1).ConfigureAwait(true);
        pending.IsCompleted.ShouldBeFalse();
        harness.Time.Advance(TimeSpan.FromSeconds(15));
        AuthResult<GoTrueSession> result = await pending.ConfigureAwait(true);

        result.Failure.Kind.ShouldBe(AuthErrorKind.NetworkUnavailable);
        harness.Handler.ReleaseRefresh();
    }

    [Fact]
    public async Task CallerCancellationThrowsInsteadOfBecomingAFailure()
    {
        using var harness = AccountsHarness.Create();
        GoTrueSession session = harness.Handler.IssueSession(Email);
        harness.Handler.HoldRefresh();
        using var cancel = new CancellationTokenSource();

        Task<AuthResult<GoTrueSession>> pending = harness.Client.RefreshAsync(session.RefreshToken, cancel.Token);
        await TimeDriver.UntilAsync(() => harness.Handler.Count("token:refresh") == 1).ConfigureAwait(true);
        await cancel.CancelAsync().ConfigureAwait(true);

        OperationCanceledException? caught = null;
        try
        {
            _ = await pending.ConfigureAwait(true);
        }
        catch (OperationCanceledException exception)
        {
            caught = exception;
        }

        caught.ShouldNotBeNull();
        harness.Handler.ReleaseRefresh();
    }

    [Fact]
    public async Task AGarbageSuccessBodyIsUnexpectedNotAnException()
    {
        using var harness = AccountsHarness.Create();
        GoTrueSession session = harness.Handler.IssueSession(Email);
        harness.Handler.ScriptError("token:refresh", 200, null);

        // The scripted answer is an error body with status 200: a body without a session.
        AuthResult<GoTrueSession> result = await harness.Client.RefreshAsync(session.RefreshToken, Token).ConfigureAwait(true);

        result.Failure.Kind.ShouldBe(AuthErrorKind.Unexpected);
    }

    private static async Task<AuthFailure> CallAsync(AccountsHarness harness, string route)
    {
        GoTrueSession session = harness.Handler.IssueSession("caller@example.org");
        CancellationToken token = TestContext.Current.CancellationToken;
        switch (route)
        {
            case "otp":
                return (await harness.Client.SendEmailOtpAsync(Email, null, null, token).ConfigureAwait(true)).Failure;
            case "verify":
                return (await harness.Client.VerifyEmailOtpAsync(Email, "12345678", token).ConfigureAwait(true)).Failure;
            case "token:pkce":
                return (await harness.Client.ExchangeCodeAsync(new SecretString("code"), Pkce.CreateVerifier(), token).ConfigureAwait(true)).Failure;
            case "token:refresh":
                return (await harness.Client.RefreshAsync(session.RefreshToken, token).ConfigureAwait(true)).Failure;
            case "logout":
                return (await harness.Client.SignOutAsync(session.AccessToken, SignOutScope.Local, token).ConfigureAwait(true)).Failure;
            default:
                return (await harness.Client.GetUserAsync(session.AccessToken, token).ConfigureAwait(true)).Failure;
        }
    }
}
