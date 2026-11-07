using System.Text.RegularExpressions;
using Autonogrammer.Accounts.Tests.Support;
using Shouldly;
using Xunit;

namespace Autonogrammer.Accounts.Tests;

/// <summary>
/// Scans everything the library logs while a full sign-in, refresh, failure and sign-out run, and proves that no email address, code, token, verifier,
/// URL query or server message text reaches a log line, a failure or a state.
/// </summary>
public sealed partial class LogHygieneTests
{
    private const string Email = "hygiene.person+tag@example.org";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [GeneratedRegex("[A-Za-z0-9_-]{30,}", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 2000)]
    private static partial Regex LongOpaqueString();

    [Fact]
    public async Task NoLogLineCarriesAnEmailCodeTokenVerifierQueryOrServerText()
    {
        using var harness = AccountsHarness.Create(magicLink: true);
        harness.Handler.ConfirmSignup(Email);
        var secrets = new List<string> { Email, "hygiene.person", "example.org", FakeGoTrueHandler.ServerMessageText, FakeGoTrueHandler.PublishableKey };

        await DriveSignInAsync(harness, secrets).ConfigureAwait(true);
        await DriveSessionAsync(harness, secrets).ConfigureAwait(true);

        IReadOnlyList<string> lines = harness.Sink.Lines;
        lines.Count.ShouldBeGreaterThan(10);
        foreach (string line in lines)
        {
            foreach (string secret in secrets)
            {
                line.ShouldNotContain(secret, Case.Insensitive);
            }

            line.ShouldNotContain("code_verifier");
            line.ShouldNotContain("code_challenge");
            line.ShouldNotContain("redirect_to");
            line.ShouldNotContain("a_code_outside_the_vocabulary");
            line.ShouldNotContain("?");
            LongOpaqueString().IsMatch(line).ShouldBeFalse($"An opaque string reached a log line: {line}");
        }

        lines.Select(line => int.Parse(line[..line.IndexOf('|', StringComparison.Ordinal)], System.Globalization.CultureInfo.InvariantCulture))
            .ShouldAllBe(id => id >= 3000 && id <= 3049);
    }

    private static async Task DriveSignInAsync(AccountsHarness harness, List<string> secrets)
    {
        // Request, wrong codes, a refused request, an out-of-vocabulary refusal and a good code.
        _ = await harness.Coordinator.SendCodeAsync(Email, nonce => new Uri($"http://127.0.0.1:50123/cb/{nonce}"), Token).ConfigureAwait(true);
        secrets.Add(harness.Coordinator.Current!.LinkNonce!);
        _ = await harness.Coordinator.VerifyCodeAsync("00000000", Token).ConfigureAwait(true);
        _ = await harness.Coordinator.CompleteLinkAsync("bad-link-code-0123456789", Token).ConfigureAwait(true);
        harness.Time.Advance(TimeSpan.FromSeconds(60));
        harness.Handler.ScriptError("otp", 429, "over_email_send_rate_limit");
        _ = await harness.Coordinator.ResendCodeAsync(Token).ConfigureAwait(true);
        harness.Handler.ScriptError("otp", 400, "a_code_outside_the_vocabulary");
        _ = await harness.Coordinator.ResendCodeAsync(Token).ConfigureAwait(true);
        _ = await harness.Coordinator.ResendCodeAsync(Token).ConfigureAwait(true);
        string code = harness.Handler.LastCode(Email);
        secrets.Add(code);
        AuthResult<AuthUnit> signedIn = await harness.Coordinator.VerifyCodeAsync(code, Token).ConfigureAwait(true);
        signedIn.IsSuccess.ShouldBeTrue();
    }

    private static async Task DriveSessionAsync(AccountsHarness harness, List<string> secrets)
    {
        // A rotation, a failing store, a refresh that fails twice and a sign-out the service does not confirm.
        secrets.Add((await harness.Manager.GetAccessTokenAsync(Token).ConfigureAwait(true)).Value.Reveal());
        harness.Store.FailSet = true;
        harness.Time.Advance(TimeSpan.FromMinutes(50));
        secrets.Add((await harness.Manager.GetAccessTokenAsync(Token).ConfigureAwait(true)).Value.Reveal());
        string stored = (await harness.Store.ReadSessionJsonAsync().ConfigureAwait(true))!;
        secrets.Add(Between(stored, "\"refreshToken\":\""));
        harness.Store.FailSet = false;
        harness.Handler.ScriptError("token:refresh", 500, null, times: 2);
        harness.Time.Advance(TimeSpan.FromMinutes(50));
        Task<AuthResult<Autonogrammer.Harness.Secrets.SecretString>> pending = harness.Manager.GetAccessTokenAsync(Token).AsTask();
        await harness.Time.AdvanceUntilAsync(() => pending.IsCompleted, TimeSpan.FromSeconds(1)).ConfigureAwait(true);
        harness.Handler.ScriptError("logout", 503, null);
        _ = await harness.Manager.SignOutAsync(SignOutScope.Local, Token).ConfigureAwait(true);
    }

    [Fact]
    public async Task FailuresAndStatesCarryOnlyFixedVocabularyAndNeverServerText()
    {
        using var harness = AccountsHarness.Create();
        harness.Handler.ConfirmSignup(Email);
        harness.Handler.ScriptError("otp", 400, "inject_this_text");

        AuthResult<SignInChallenge> failed = await harness.Coordinator.SendCodeAsync(Email, null, Token).ConfigureAwait(true);

        failed.Failure.ServerCode.ShouldBeNull();
        failed.Failure.ToString().ShouldNotContain("inject_this_text");
        failed.Failure.ToString().ShouldNotContain(FakeGoTrueHandler.ServerMessageText);
        failed.Failure.ToString().ShouldNotContain(Email);
        AuthServerCodes.Vocabulary.Count.ShouldBe(16);
        AuthServerCodes.Normalize("otp_expired").ShouldBe("otp_expired");
        AuthServerCodes.Normalize("OTP_EXPIRED").ShouldBeNull();
        AuthServerCodes.Normalize("<script>").ShouldBeNull();
        AuthServerCodes.Normalize(null).ShouldBeNull();
    }

    [Fact]
    public async Task ASessionAndItsStateNeverRenderATokenOrTheStoredSecret()
    {
        using var harness = AccountsHarness.Create();
        GoTrueSession session = harness.Handler.IssueSession(Email);
        await harness.Manager.SignInAsync(session, harness.Options.Issuer, Token).ConfigureAwait(true);

        string rendered = session + " " + harness.Manager.State + " " + harness.Manager.State.Identity;

        rendered.ShouldNotContain(session.AccessToken.Reveal());
        rendered.ShouldNotContain(session.RefreshToken.Reveal());
        (await harness.Manager.GetAccessTokenAsync(Token).ConfigureAwait(true)).Value.ToString().ShouldBe("[redacted]");
    }

    private static string Between(string text, string start)
    {
        int from = text.IndexOf(start, StringComparison.Ordinal) + start.Length;
        return text[from..text.IndexOf('"', from)];
    }
}
