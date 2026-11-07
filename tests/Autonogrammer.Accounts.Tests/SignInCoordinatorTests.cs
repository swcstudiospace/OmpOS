using Autonogrammer.Accounts.Tests.Support;
using Autonogrammer.Contracts.Accounts;
using Shouldly;
using Xunit;

namespace Autonogrammer.Accounts.Tests;

/// <summary>
/// Verifies the sign-in coordinator: cooldown and the five-wrong-codes rule, verifier and nonce reuse on resend, the first-time link-then-new-code path,
/// the link exchange, address validation, and that the closed error catalogue has exactly the agreed members.
/// </summary>
public sealed class SignInCoordinatorTests
{
    private const string Email = "person@example.org";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public void TheErrorCatalogueIsClosedAndHasNoLoopbackMember()
    {
        Enum.GetNames<AuthErrorKind>().ShouldBe(
        [
            "EmailRateLimited",
            "TooManyRequests",
            "CodeInvalidOrExpired",
            "LinkInvalidOrExpired",
            "LinkOpenedInWrongTab",
            "EmailInvalid",
            "SignUpsClosed",
            "CaptchaRequired",
            "AccountBlocked",
            "MethodUnavailable",
            "SessionEnded",
            "NetworkUnavailable",
            "ServiceUnavailable",
            "ConfigUnavailable",
            "AccountRejected",
            "Unexpected",
        ]);
        Enum.GetNames<AuthPhase>().ShouldBe(["SignedOut", "AwaitingLink", "SigningIn", "SignedIn", "Error"]);
    }

    [Fact]
    public async Task ARequestedCodeReturnsAChallengeWithTimingFromTheOptions()
    {
        using var harness = AccountsHarness.Create();
        harness.Handler.ConfirmSignup(Email);
        DateTimeOffset start = harness.Time.GetUtcNow();

        AuthResult<SignInChallenge> sent = await harness.Coordinator.SendCodeAsync("  " + Email + " ", null, Token).ConfigureAwait(true);

        sent.IsSuccess.ShouldBeTrue();
        sent.Value.Email.ShouldBe(Email);
        sent.Value.SentAt.ShouldBe(start);
        sent.Value.ExpiresAt.ShouldBe(start + TimeSpan.FromSeconds(3600));
        sent.Value.ResendAvailableAt.ShouldBe(start + TimeSpan.FromSeconds(60));
        sent.Value.LinkAvailable.ShouldBeFalse();
        sent.Value.LinkNonce.ShouldBeNull();
        harness.Coordinator.Current.ShouldBe(sent.Value);
        harness.Coordinator.RemainingAttempts.ShouldBe(SignInCoordinator.MaxWrongCodes);
    }

    [Fact]
    public async Task ACorrectCodeSignsInThroughTheManagerAndEndsTheAttempt()
    {
        using var harness = AccountsHarness.Create();
        harness.Handler.ConfirmSignup(Email);
        _ = await harness.Coordinator.SendCodeAsync(Email, null, Token).ConfigureAwait(true);
        string code = harness.Handler.LastCode(Email);

        AuthResult<AuthUnit> result = await harness.Coordinator.VerifyCodeAsync(code[..4] + " " + code[4..], Token).ConfigureAwait(true);

        result.IsSuccess.ShouldBeTrue();
        harness.Manager.State.Phase.ShouldBe(AuthPhase.SignedIn);
        harness.Manager.State.Identity!.Email.ShouldBe(Email);
        harness.Coordinator.Current.ShouldBeNull();
        (await harness.Store.ReadSessionJsonAsync().ConfigureAwait(true)).ShouldNotBeNull();
    }

    [Fact]
    public async Task FiveWrongCodesForceAResendAndAResendBeforeTheCooldownReturnsTheCooldownKind()
    {
        using var harness = AccountsHarness.Create();
        harness.Handler.ConfirmSignup(Email);
        _ = await harness.Coordinator.SendCodeAsync(Email, null, Token).ConfigureAwait(true);
        string code = harness.Handler.LastCode(Email);

        for (int i = 0; i < SignInCoordinator.MaxWrongCodes; i++)
        {
            AuthResult<AuthUnit> wrong = await harness.Coordinator.VerifyCodeAsync("00000000", Token).ConfigureAwait(true);
            wrong.Failure.Kind.ShouldBe(AuthErrorKind.CodeInvalidOrExpired);
            harness.Coordinator.RemainingAttempts.ShouldBe(SignInCoordinator.MaxWrongCodes - i - 1);
        }

        int verifiesBefore = harness.Handler.Count("verify");
        AuthResult<AuthUnit> locked = await harness.Coordinator.VerifyCodeAsync(code, Token).ConfigureAwait(true);
        harness.Time.Advance(TimeSpan.FromSeconds(30));
        AuthResult<SignInChallenge> tooSoon = await harness.Coordinator.ResendCodeAsync(Token).ConfigureAwait(true);

        locked.Failure.Kind.ShouldBe(AuthErrorKind.CodeInvalidOrExpired);
        harness.Handler.Count("verify").ShouldBe(verifiesBefore);
        tooSoon.Failure.Kind.ShouldBe(AuthErrorKind.TooManyRequests);
        tooSoon.Failure.RetryAfter.ShouldBe(TimeSpan.FromSeconds(30));
        harness.Handler.Count("otp").ShouldBe(1);

        harness.Time.Advance(TimeSpan.FromSeconds(30));
        AuthResult<SignInChallenge> resent = await harness.Coordinator.ResendCodeAsync(Token).ConfigureAwait(true);
        resent.IsSuccess.ShouldBeTrue();
        harness.Coordinator.RemainingAttempts.ShouldBe(SignInCoordinator.MaxWrongCodes);
        AuthResult<AuthUnit> signedIn = await harness.Coordinator.VerifyCodeAsync(harness.Handler.LastCode(Email), Token).ConfigureAwait(true);
        signedIn.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task AMalformedOrUnreachableCheckDoesNotCountAsAWrongCode()
    {
        using var harness = AccountsHarness.Create();
        harness.Handler.ConfirmSignup(Email);
        _ = await harness.Coordinator.SendCodeAsync(Email, null, Token).ConfigureAwait(true);
        harness.Handler.ScriptTransportFailure("verify");

        AuthResult<AuthUnit> malformed = await harness.Coordinator.VerifyCodeAsync("12ab", Token).ConfigureAwait(true);
        AuthResult<AuthUnit> unreachable = await harness.Coordinator.VerifyCodeAsync(harness.Handler.LastCode(Email), Token).ConfigureAwait(true);

        malformed.Failure.Kind.ShouldBe(AuthErrorKind.CodeInvalidOrExpired);
        unreachable.Failure.Kind.ShouldBe(AuthErrorKind.NetworkUnavailable);
        harness.Coordinator.RemainingAttempts.ShouldBe(SignInCoordinator.MaxWrongCodes);
        (await harness.Coordinator.VerifyCodeAsync(harness.Handler.LastCode(Email), Token).ConfigureAwait(true)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task ACodeThatOutlivedItsExpiryByTheClockIsRefusedWithoutARequest()
    {
        using var harness = AccountsHarness.Create();
        harness.Handler.ConfirmSignup(Email);
        _ = await harness.Coordinator.SendCodeAsync(Email, null, Token).ConfigureAwait(true);
        string code = harness.Handler.LastCode(Email);
        harness.Time.Advance(TimeSpan.FromSeconds(3600));

        AuthResult<AuthUnit> result = await harness.Coordinator.VerifyCodeAsync(code, Token).ConfigureAwait(true);

        result.Failure.Kind.ShouldBe(AuthErrorKind.CodeInvalidOrExpired);
        harness.Handler.Count("verify").ShouldBe(0);
    }

    [Fact]
    public async Task AFirstTimeUserGetsTheHintAndSignsInAfterOpeningTheFirstLinkAndRequestingANewCode()
    {
        using var harness = AccountsHarness.Create();

        AuthResult<SignInChallenge> first = await harness.Coordinator.SendCodeAsync("new.person@example.org", null, Token).ConfigureAwait(true);
        SentEmail signup = harness.Handler.Outbox.Single();
        AuthResult<AuthUnit> noCode = await harness.Coordinator.VerifyCodeAsync("12345678", Token).ConfigureAwait(true);

        first.Value.ShowFirstTimeHint.ShouldBeTrue();
        signup.Kind.ShouldBe("signup");
        signup.Code.ShouldBeNull();
        noCode.Failure.Kind.ShouldBe(AuthErrorKind.CodeInvalidOrExpired);

        harness.Handler.ConfirmSignup("new.person@example.org");
        harness.Time.Advance(TimeSpan.FromSeconds(60));
        AuthResult<SignInChallenge> second = await harness.Coordinator.ResendCodeAsync(Token).ConfigureAwait(true);
        SentEmail magic = harness.Handler.Outbox[^1];

        second.IsSuccess.ShouldBeTrue();
        magic.Kind.ShouldBe("magiclink");
        magic.Code.ShouldNotBeNull();
        (await harness.Coordinator.VerifyCodeAsync(magic.Code, Token).ConfigureAwait(true)).IsSuccess.ShouldBeTrue();
        harness.Manager.State.Identity!.Email.ShouldBe("new.person@example.org");
    }

    [Fact]
    public async Task AResendReusesTheVerifierAndNonceAndANewAddressStartsFresh()
    {
        using var harness = AccountsHarness.Create(magicLink: true);
        harness.Handler.ConfirmSignup(Email);
        var nonces = new List<string>();
        Uri? Redirect(string nonce)
        {
            nonces.Add(nonce);
            return new Uri($"http://127.0.0.1:50123/cb/{nonce}");
        }

        AuthResult<SignInChallenge> first = await harness.Coordinator.SendCodeAsync(Email, Redirect, Token).ConfigureAwait(true);
        harness.Time.Advance(TimeSpan.FromSeconds(60));
        AuthResult<SignInChallenge> resent = await harness.Coordinator.ResendCodeAsync(Token).ConfigureAwait(true);
        harness.Time.Advance(TimeSpan.FromSeconds(60));
        AuthResult<SignInChallenge> other = await harness.Coordinator.SendCodeAsync("other@example.org", Redirect, Token).ConfigureAwait(true);

        first.Value.LinkAvailable.ShouldBeTrue();
        resent.Value.LinkNonce.ShouldBe(first.Value.LinkNonce);
        string.Equals(other.Value.LinkNonce, first.Value.LinkNonce, StringComparison.Ordinal).ShouldBeFalse();
        nonces.Count.ShouldBe(2);
        IReadOnlyList<RecordedRequest> otps = [.. harness.Handler.Requests.Where(request => request.PathIs("otp"))];
        otps.Count.ShouldBe(3);
        ChallengeOf(otps[0]).ShouldBe(ChallengeOf(otps[1]));
        string.Equals(ChallengeOf(otps[2]), ChallengeOf(otps[0]), StringComparison.Ordinal).ShouldBeFalse();
        otps[0].Query.ShouldBe(otps[1].Query);
        otps[0].Query.ShouldContain(Uri.EscapeDataString("/cb/" + first.Value.LinkNonce));
    }

    [Fact]
    public async Task AMagicLinkCallbackSignsInAndAFailedExchangeKeepsTheCodePathAlive()
    {
        using var harness = AccountsHarness.Create(magicLink: true);
        harness.Handler.ConfirmSignup(Email);
        _ = await harness.Coordinator.SendCodeAsync(Email, nonce => new Uri($"http://127.0.0.1:50123/cb/{nonce}"), Token).ConfigureAwait(true);
        string authCode = harness.Handler.FollowLink(Email);

        AuthResult<AuthUnit> bad = await harness.Coordinator.CompleteLinkAsync("not-a-real-code", Token).ConfigureAwait(true);
        harness.Coordinator.Current.ShouldNotBeNull();
        AuthResult<AuthUnit> good = await harness.Coordinator.CompleteLinkAsync(authCode, Token).ConfigureAwait(true);

        bad.Failure.Kind.ShouldBe(AuthErrorKind.LinkInvalidOrExpired);
        good.IsSuccess.ShouldBeTrue();
        harness.Manager.State.Phase.ShouldBe(AuthPhase.SignedIn);
        harness.Coordinator.Current.ShouldBeNull();
    }

    [Fact]
    public async Task AFailedExchangeStillLetsTheEmailedCodeSignIn()
    {
        using var harness = AccountsHarness.Create(magicLink: true);
        harness.Handler.ConfirmSignup(Email);
        _ = await harness.Coordinator.SendCodeAsync(Email, nonce => new Uri($"http://127.0.0.1:50123/cb/{nonce}"), Token).ConfigureAwait(true);
        _ = await harness.Coordinator.CompleteLinkAsync("bad", Token).ConfigureAwait(true);

        AuthResult<AuthUnit> byCode = await harness.Coordinator.VerifyCodeAsync(harness.Handler.LastCode(Email), Token).ConfigureAwait(true);

        byCode.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task ALinkWithoutAnAttemptOrWithoutAVerifierIsLinkInvalidOrExpired()
    {
        using var harness = AccountsHarness.Create();
        harness.Handler.ConfirmSignup(Email);

        AuthResult<AuthUnit> none = await harness.Coordinator.CompleteLinkAsync("code", Token).ConfigureAwait(true);
        _ = await harness.Coordinator.SendCodeAsync(Email, null, Token).ConfigureAwait(true);
        AuthResult<AuthUnit> codeOnly = await harness.Coordinator.CompleteLinkAsync("code", Token).ConfigureAwait(true);

        none.Failure.Kind.ShouldBe(AuthErrorKind.LinkInvalidOrExpired);
        codeOnly.Failure.Kind.ShouldBe(AuthErrorKind.LinkInvalidOrExpired);
        harness.Handler.Count("token:pkce").ShouldBe(0);
    }

    [Fact]
    public async Task AProjectWithoutTheLinkNeverCallsTheRedirectFactoryAndSendsNoChallenge()
    {
        using var harness = AccountsHarness.Create(magicLink: false);
        harness.Handler.ConfirmSignup(Email);
        int calls = 0;

        AuthResult<SignInChallenge> sent = await harness.Coordinator.SendCodeAsync(Email, _ =>
        {
            calls++;
            return new Uri("http://127.0.0.1:1/cb/x");
        }, Token).ConfigureAwait(true);

        sent.Value.LinkAvailable.ShouldBeFalse();
        calls.ShouldBe(0);
        harness.Handler.Requests.Single().Body.ShouldNotContain("code_challenge");
        harness.Handler.Requests.Single().Query.ShouldBeEmpty();
    }

    [Fact]
    public async Task ALoopbackThatCouldNotBindMeansTheCodeOnlyNotAnError()
    {
        using var harness = AccountsHarness.Create(magicLink: true);
        harness.Handler.ConfirmSignup(Email);

        AuthResult<SignInChallenge> sent = await harness.Coordinator.SendCodeAsync(Email, _ => null, Token).ConfigureAwait(true);

        sent.IsSuccess.ShouldBeTrue();
        sent.Value.LinkAvailable.ShouldBeFalse();
        sent.Value.LinkNonce.ShouldBeNull();
        harness.Handler.Requests.Single().Body.ShouldNotContain("code_challenge");
        harness.Handler.Requests.Single().Query.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no-at-sign")]
    [InlineData("two@@example.org")]
    [InlineData("a@b@c.org")]
    [InlineData("@example.org")]
    [InlineData("person@")]
    [InlineData("per son@example.org")]
    public async Task AnAddressThatIsNotPlausibleIsEmailInvalidWithoutARequest(string address)
    {
        using var harness = AccountsHarness.Create();

        AuthResult<SignInChallenge> result = await harness.Coordinator.SendCodeAsync(address, null, Token).ConfigureAwait(true);

        result.Failure.Kind.ShouldBe(AuthErrorKind.EmailInvalid);
        harness.Handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task WithoutAConfiguredClientEveryStepIsConfigUnavailable()
    {
        using var harness = AccountsHarness.Create(configured: false);

        AuthResult<SignInChallenge> send = await harness.Coordinator.SendCodeAsync(Email, null, Token).ConfigureAwait(true);

        send.Failure.Kind.ShouldBe(AuthErrorKind.ConfigUnavailable);
        harness.Handler.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(429, "over_email_send_rate_limit", AuthErrorKind.EmailRateLimited)]
    [InlineData(400, "captcha_failed", AuthErrorKind.CaptchaRequired)]
    [InlineData(422, "signup_disabled", AuthErrorKind.SignUpsClosed)]
    [InlineData(400, "validation_failed", AuthErrorKind.EmailInvalid)]
    [InlineData(503, null, AuthErrorKind.ServiceUnavailable)]
    public async Task AFailedRequestLeavesNoAttemptAndSurfacesTheMappedKind(int status, string? code, AuthErrorKind expected)
    {
        using var harness = AccountsHarness.Create();
        harness.Handler.ScriptError("otp", status, code);

        AuthResult<SignInChallenge> result = await harness.Coordinator.SendCodeAsync(Email, null, Token).ConfigureAwait(true);

        result.Failure.Kind.ShouldBe(expected);
        harness.Coordinator.Current.ShouldBeNull();
        harness.Manager.State.ShouldBe(AccountState.SignedOut);
    }

    [Fact]
    public async Task VerifyingWithoutAnAttemptIsAFailureNotAnException()
    {
        using var harness = AccountsHarness.Create();

        AuthResult<AuthUnit> result = await harness.Coordinator.VerifyCodeAsync("12345678", Token).ConfigureAwait(true);

        result.Failure.Kind.ShouldBe(AuthErrorKind.Unexpected);
        (await harness.Coordinator.ResendCodeAsync(Token).ConfigureAwait(true)).Failure.Kind.ShouldBe(AuthErrorKind.Unexpected);
    }

    [Fact]
    public async Task ResetForgetsTheAttemptSoChangeEmailStartsClean()
    {
        using var harness = AccountsHarness.Create();
        harness.Handler.ConfirmSignup(Email);
        _ = await harness.Coordinator.SendCodeAsync(Email, null, Token).ConfigureAwait(true);

        harness.Coordinator.Reset();

        harness.Coordinator.Current.ShouldBeNull();
        harness.Coordinator.RemainingAttempts.ShouldBe(0);
        AuthResult<SignInChallenge> again = await harness.Coordinator.SendCodeAsync(Email, null, Token).ConfigureAwait(true);
        again.IsSuccess.ShouldBeTrue();
    }

    private static string ChallengeOf(RecordedRequest request)
    {
        const string Key = "\"code_challenge\":\"";
        int start = request.Body.IndexOf(Key, StringComparison.Ordinal);
        start.ShouldBeGreaterThanOrEqualTo(0);
        start += Key.Length;
        return request.Body[start..request.Body.IndexOf('"', start)];
    }
}
