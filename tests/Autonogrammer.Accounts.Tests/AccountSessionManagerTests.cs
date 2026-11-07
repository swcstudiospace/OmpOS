using System.Text.Json;
using Autonogrammer.Accounts.Tests.Support;
using Autonogrammer.Contracts.Accounts;
using Autonogrammer.Harness.Secrets;
using Shouldly;
using Xunit;

namespace Autonogrammer.Accounts.Tests;

/// <summary>
/// Verifies the session manager over the in-process fake: one shared refresh, store-first rotation, startup without the network, offline as a notice,
/// definitive failures, the refresh guard seam and the order and scopes of sign-out.
/// </summary>
public sealed class AccountSessionManagerTests
{
    private const string Email = "person@example.org";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task FiftyConcurrentCallersTriggerExactlyOneRefreshRequest()
    {
        using var harness = AccountsHarness.Create();
        GoTrueSession issued = harness.Handler.IssueSession(Email);
        await harness.SeedStoredSessionAsync(issued).ConfigureAwait(true);
        harness.Handler.HoldRefresh();
        await harness.Manager.StartAsync(Token).ConfigureAwait(true);

        Task<AuthResult<SecretString>>[] callers = [.. Enumerable.Range(0, 50).Select(_ => harness.Manager.GetAccessTokenAsync(Token).AsTask())];
        await TimeDriver.UntilAsync(() => harness.Handler.Count("token:refresh") >= 1).ConfigureAwait(true);
        harness.Handler.ReleaseRefresh();
        AuthResult<SecretString>[] results = await Task.WhenAll(callers).ConfigureAwait(true);

        harness.Handler.Count("token:refresh").ShouldBe(1);
        results.ShouldAllBe(result => result.IsSuccess);
        results.Select(result => result.Value.Reveal()).Distinct(StringComparer.Ordinal).Count().ShouldBe(1);
        harness.Manager.State.Connectivity.ShouldBe(AccountConnectivity.Online);
    }

    [Fact]
    public async Task TheRotatedRefreshTokenIsStoredBeforeTheSessionIsPublished()
    {
        using var harness = AccountsHarness.Create();
        GoTrueSession issued = harness.Handler.IssueSession(Email);
        await harness.Manager.SignInAsync(issued, harness.Options.Issuer, Token).ConfigureAwait(true);
        harness.Time.Advance(TimeSpan.FromMinutes(50));
        string? visibleWhenStoring = null;
        harness.Store.BeforeSet = () => visibleWhenStoring = harness.Manager.CurrentAccessToken?.Reveal();

        AuthResult<SecretString> refreshed = await harness.Manager.GetAccessTokenAsync(Token).ConfigureAwait(true);

        refreshed.IsSuccess.ShouldBeTrue();
        string.Equals(refreshed.Value.Reveal(), issued.AccessToken.Reveal(), StringComparison.Ordinal).ShouldBeFalse();
        visibleWhenStoring.ShouldBe(issued.AccessToken.Reveal());
        harness.Manager.CurrentAccessToken!.Reveal().ShouldBe(refreshed.Value.Reveal());
        string stored = (await harness.Store.ReadSessionJsonAsync().ConfigureAwait(true))!;
        stored.ShouldNotContain(issued.RefreshToken.Reveal());
        stored.ShouldNotContain(refreshed.Value.Reveal());
    }

    [Fact]
    public async Task AFailingStoreKeepsTheSessionInMemoryAndRaisesTheNotice()
    {
        using var harness = AccountsHarness.Create();
        GoTrueSession issued = harness.Handler.IssueSession(Email);
        await harness.Manager.SignInAsync(issued, harness.Options.Issuer, Token).ConfigureAwait(true);
        harness.Store.FailSet = true;
        harness.Time.Advance(TimeSpan.FromMinutes(50));

        AuthResult<SecretString> refreshed = await harness.Manager.GetAccessTokenAsync(Token).ConfigureAwait(true);

        refreshed.IsSuccess.ShouldBeTrue();
        harness.Manager.State.Phase.ShouldBe(AuthPhase.SignedIn);
        harness.Manager.State.Notices.ShouldBe(AccountNotices.SessionNotRemembered);
        harness.Manager.State.Connectivity.ShouldBe(AccountConnectivity.Online);

        harness.Store.FailSet = false;
        harness.Time.Advance(TimeSpan.FromMinutes(50));
        _ = await harness.Manager.GetAccessTokenAsync(Token).ConfigureAwait(true);
        harness.Manager.State.Notices.ShouldBe(AccountNotices.None);
    }

    [Fact]
    public async Task SigningInWithAFailingStoreIsASessionNotRememberedNoticeNeverAnError()
    {
        using var harness = AccountsHarness.Create();
        harness.Store.FailSet = true;

        await harness.Manager.SignInAsync(harness.Handler.IssueSession(Email), harness.Options.Issuer, Token).ConfigureAwait(true);

        harness.Manager.State.Phase.ShouldBe(AuthPhase.SignedIn);
        harness.Manager.State.Notices.ShouldBe(AccountNotices.SessionNotRemembered);
    }

    [Fact]
    public async Task TheStoredShapeHasSchemaOneAndNeverTheAccessToken()
    {
        using var harness = AccountsHarness.Create();
        GoTrueSession issued = harness.Handler.IssueSession(Email);

        await harness.Manager.SignInAsync(issued, harness.Options.Issuer, Token).ConfigureAwait(true);

        string json = (await harness.Store.ReadSessionJsonAsync().ConfigureAwait(true))!;
        using JsonDocument document = JsonDocument.Parse(json);
        document.RootElement.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal)
            .ShouldBe(["email", "issuer", "refreshToken", "schema", "userId"]);
        document.RootElement.GetProperty("schema").GetInt32().ShouldBe(1);
        document.RootElement.GetProperty("issuer").GetString().ShouldBe("https://fake-project.supabase.co/auth/v1");
        json.ShouldNotContain(issued.AccessToken.Reveal());
    }

    [Fact]
    public async Task StartupWithAStoredSessionIsSignedInAtOnceAndNeverWaitsOnTheNetwork()
    {
        using var harness = AccountsHarness.Create();
        GoTrueSession issued = harness.Handler.IssueSession(Email);
        await harness.SeedStoredSessionAsync(issued).ConfigureAwait(true);
        harness.Handler.HoldRefresh();

        await harness.Manager.StartAsync(Token).ConfigureAwait(true);

        AccountState starting = harness.Manager.State;
        starting.Phase.ShouldBe(AuthPhase.SignedIn);
        starting.Connectivity.ShouldBe(AccountConnectivity.Refreshing);
        starting.Identity.ShouldBe(new AccountIdentity(issued.UserId, Email));
        await TimeDriver.UntilAsync(() => harness.Handler.Count("token:refresh") == 1).ConfigureAwait(true);
        harness.Manager.State.Connectivity.ShouldBe(AccountConnectivity.Refreshing);

        harness.Handler.ReleaseRefresh();
        _ = await harness.Manager.StartupRefreshAsync().ConfigureAwait(true);

        harness.Manager.State.Connectivity.ShouldBe(AccountConnectivity.Online);
        harness.Manager.State.Notices.ShouldBe(AccountNotices.None);
    }

    [Fact]
    public async Task StartupWithoutAStoredSessionIsSignedOutAndMakesNoRequest()
    {
        using var harness = AccountsHarness.Create();

        await harness.Manager.StartAsync(Token).ConfigureAwait(true);

        harness.Manager.State.ShouldBe(AccountState.SignedOut);
        harness.Manager.StartupRefresh.ShouldBeNull();
        harness.Handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task FiveSecondBudgetsEndAStartupRefreshAsOfflineNotSignedOutAfterTheOneSecondRetry()
    {
        using var harness = AccountsHarness.Create();
        GoTrueSession issued = harness.Handler.IssueSession(Email);
        await harness.SeedStoredSessionAsync(issued).ConfigureAwait(true);
        harness.Handler.HoldRefresh();
        await harness.Manager.StartAsync(Token).ConfigureAwait(true);
        await TimeDriver.UntilAsync(() => harness.Handler.Count("token:refresh") == 1).ConfigureAwait(true);

        harness.Time.Advance(TimeSpan.FromSeconds(5));
        await harness.Time.AdvanceUntilAsync(() => harness.Handler.Count("token:refresh") == 2, TimeSpan.FromSeconds(1)).ConfigureAwait(true);
        harness.Time.Advance(TimeSpan.FromSeconds(5));
        _ = await harness.Manager.StartupRefreshAsync().ConfigureAwait(true);

        harness.Manager.State.Phase.ShouldBe(AuthPhase.SignedIn);
        harness.Manager.State.Connectivity.ShouldBe(AccountConnectivity.Offline);
        harness.Manager.State.Notices.ShouldBe(AccountNotices.None);
        harness.Handler.ReleaseRefresh();
    }

    [Fact]
    public async Task AStartupRefreshWhoseResponseWasLostIsRetriedOnceAfterOneSecondInsideTheReuseInterval()
    {
        using var harness = AccountsHarness.Create();
        await harness.SeedStoredSessionAsync(harness.Handler.IssueSession(Email)).ConfigureAwait(true);
        harness.Handler.ScriptLostResponse("token:refresh");
        await harness.Manager.StartAsync(Token).ConfigureAwait(true);

        await harness.Time.AdvanceUntilAsync(() => harness.Manager.StartupRefresh!.IsCompleted, TimeSpan.FromMilliseconds(500)).ConfigureAwait(true);

        (await harness.Manager.StartupRefreshAsync().ConfigureAwait(true)).IsSuccess.ShouldBeTrue();
        harness.Handler.Count("token:refresh").ShouldBe(2);
        harness.Manager.State.Phase.ShouldBe(AuthPhase.SignedIn);
        harness.Manager.State.Connectivity.ShouldBe(AccountConnectivity.Online);
        harness.Handler.LiveSessions(Email).ShouldBe(1);
    }

    [Fact]
    public async Task OfflineStartIsASignedInNoticeAndTheRetryLoopBringsItBackOnline()
    {
        using var harness = AccountsHarness.Create();
        GoTrueSession issued = harness.Handler.IssueSession(Email);
        await harness.SeedStoredSessionAsync(issued).ConfigureAwait(true);
        harness.Handler.ScriptTransportFailure("token:refresh", times: 2);
        await harness.Manager.StartAsync(Token).ConfigureAwait(true);

        await harness.Time.AdvanceUntilAsync(() => harness.Manager.StartupRefresh!.IsCompleted, TimeSpan.FromSeconds(1)).ConfigureAwait(true);

        harness.Manager.State.Phase.ShouldBe(AuthPhase.SignedIn);
        harness.Manager.State.Connectivity.ShouldBe(AccountConnectivity.Offline);
        harness.Manager.State.Identity.ShouldNotBeNull();

        await harness.Time.AdvanceUntilAsync(() => harness.Manager.State.Connectivity == AccountConnectivity.Online, TimeSpan.FromSeconds(30)).ConfigureAwait(true);

        harness.Manager.State.Phase.ShouldBe(AuthPhase.SignedIn);
        harness.Handler.Count("token:refresh").ShouldBe(3);
    }

    [Fact]
    public async Task ALostRefreshResponseIsRetriedOnceAfterOneSecondInsideTheReuseInterval()
    {
        using var harness = AccountsHarness.Create();
        await harness.Manager.SignInAsync(harness.Handler.IssueSession(Email), harness.Options.Issuer, Token).ConfigureAwait(true);
        harness.Time.Advance(TimeSpan.FromMinutes(50));
        harness.Handler.ScriptLostResponse("token:refresh");

        Task<AuthResult<SecretString>> pending = harness.Manager.GetAccessTokenAsync(Token).AsTask();
        await harness.Time.AdvanceUntilAsync(() => pending.IsCompleted, TimeSpan.FromMilliseconds(500)).ConfigureAwait(true);

        (await pending.ConfigureAwait(true)).IsSuccess.ShouldBeTrue();
        harness.Handler.Count("token:refresh").ShouldBe(2);
        harness.Manager.State.Connectivity.ShouldBe(AccountConnectivity.Online);
        harness.Handler.LiveSessions(Email).ShouldBe(1);
    }

    [Fact]
    public async Task OfflineRetriesStopAfterFifteenMinutesAndTheSessionStaysSignedIn()
    {
        using var harness = AccountsHarness.Create();
        await harness.Manager.SignInAsync(harness.Handler.IssueSession(Email), harness.Options.Issuer, Token).ConfigureAwait(true);
        harness.Time.Advance(TimeSpan.FromMinutes(50));
        harness.Handler.ScriptTransportFailure("token:refresh", times: 1000);

        Task<AuthResult<SecretString>> first = harness.Manager.GetAccessTokenAsync(Token).AsTask();
        await harness.Time.AdvanceUntilAsync(() => first.IsCompleted, TimeSpan.FromSeconds(1)).ConfigureAwait(true);
        harness.Manager.State.Connectivity.ShouldBe(AccountConnectivity.Offline);

        Task retries = harness.Manager.OfflineRetryLoop.ShouldNotBeNull("the failed refresh starts the offline retries");
        await harness.Time.AdvanceUntilAsync(() => retries.IsCompleted, TimeSpan.FromMinutes(1)).ConfigureAwait(true);
        await retries.WaitAsync(TimeSpan.FromSeconds(30), Token).ConfigureAwait(true);

        int settled = harness.Handler.Count("token:refresh");
        harness.Time.Advance(TimeSpan.FromMinutes(20));

        settled.ShouldBeGreaterThan(2);
        settled.ShouldBeLessThan(14);
        harness.Handler.Count("token:refresh").ShouldBe(settled);
        harness.Manager.State.Phase.ShouldBe(AuthPhase.SignedIn);
        harness.Manager.State.Connectivity.ShouldBe(AccountConnectivity.Offline);
    }

    [Fact]
    public async Task WithoutAConfiguredClientStartupStaysSignedInMakesNoRequestAndTokensReportConfigUnavailable()
    {
        using var harness = AccountsHarness.Create(configured: false);
        await harness.SeedStoredSessionAsync(harness.Handler.IssueSession(Email)).ConfigureAwait(true);

        await harness.Manager.StartAsync(Token).ConfigureAwait(true);
        _ = await harness.Manager.StartupRefreshAsync().ConfigureAwait(true);
        AuthResult<SecretString> token = await harness.Manager.GetAccessTokenAsync(Token).ConfigureAwait(true);

        harness.Manager.State.Phase.ShouldBe(AuthPhase.SignedIn);
        harness.Manager.State.Connectivity.ShouldBe(AccountConnectivity.Offline);
        token.Failure.Kind.ShouldBe(AuthErrorKind.ConfigUnavailable);
        harness.Handler.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("refresh_token_not_found")]
    [InlineData("refresh_token_already_used")]
    [InlineData("session_not_found")]
    [InlineData("session_expired")]
    [InlineData("user_banned")]
    public async Task ADefinitiveFailureClearsStoreAndMemoryAndSignsOutWithSessionEnded(string serverCode)
    {
        using var harness = AccountsHarness.Create();
        await harness.SeedStoredSessionAsync(harness.Handler.IssueSession(Email)).ConfigureAwait(true);
        harness.Handler.ScriptError("token:refresh", 400, serverCode);
        await harness.Manager.StartAsync(Token).ConfigureAwait(true);

        AuthResult<AuthUnit> outcome = await harness.Manager.StartupRefreshAsync().ConfigureAwait(true);

        outcome.IsSuccess.ShouldBeFalse();
        harness.Manager.State.Phase.ShouldBe(AuthPhase.SignedOut);
        harness.Manager.State.Identity.ShouldBeNull();
        harness.Manager.State.Notices.ShouldBe(AccountNotices.SessionEnded);
        (await harness.Store.ReadSessionJsonAsync().ConfigureAwait(true)).ShouldBeNull();
        (await harness.Manager.GetAccessTokenAsync(Token).ConfigureAwait(true)).Failure.Kind.ShouldBe(AuthErrorKind.SessionEnded);
    }

    [Fact]
    public async Task ARevokedSessionOnTheServiceEndsTheLocalSessionWithoutAnException()
    {
        using var harness = AccountsHarness.Create();
        GoTrueSession issued = harness.Handler.IssueSession(Email);
        await harness.Manager.SignInAsync(issued, harness.Options.Issuer, Token).ConfigureAwait(true);
        _ = await harness.Client.SignOutAsync(issued.AccessToken, SignOutScope.Global, Token).ConfigureAwait(true);
        harness.Time.Advance(TimeSpan.FromMinutes(50));

        AuthResult<SecretString> token = await harness.Manager.GetAccessTokenAsync(Token).ConfigureAwait(true);

        token.IsSuccess.ShouldBeFalse();
        harness.Manager.State.Phase.ShouldBe(AuthPhase.SignedOut);
        harness.Manager.State.Notices.ShouldBe(AccountNotices.SessionEnded);
    }

    [Fact]
    public async Task AStoredSessionOfAnotherProjectIsDiscardedWithServiceChanged()
    {
        using var harness = AccountsHarness.Create();
        await harness.SeedStoredSessionAsync(harness.Handler.IssueSession(Email), issuer: "https://other-project.supabase.co/auth/v1").ConfigureAwait(true);

        await harness.Manager.StartAsync(Token).ConfigureAwait(true);
        _ = await harness.Manager.StartupRefreshAsync().ConfigureAwait(true);

        harness.Manager.State.Phase.ShouldBe(AuthPhase.SignedOut);
        harness.Manager.State.Notices.ShouldBe(AccountNotices.ServiceChanged);
        (await harness.Store.ReadSessionJsonAsync().ConfigureAwait(true)).ShouldBeNull();
        harness.Handler.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("{\"schema\":1,\"issuer\":\"https://fake-project.supabase.co/auth/v1\",\"refreshToken\":\"r\",\"userId\":\"u\",\"unknown\":true}")]
    [InlineData("{\"schema\":2,\"issuer\":\"https://fake-project.supabase.co/auth/v1\",\"refreshToken\":\"r\",\"userId\":\"u\"}")]
    [InlineData("not json")]
    public async Task AStoredSecretOfTheWrongShapeIsDiscardedAsSignedOut(string json)
    {
        using var harness = AccountsHarness.Create();
        await harness.Store.SeedAsync(json).ConfigureAwait(true);

        await harness.Manager.StartAsync(Token).ConfigureAwait(true);

        harness.Manager.State.ShouldBe(AccountState.SignedOut);
        (await harness.Store.ReadSessionJsonAsync().ConfigureAwait(true)).ShouldBeNull();
        harness.Handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task AStoreThatCannotBeReadAtStartupMeansSignedOutNotACrash()
    {
        using var harness = AccountsHarness.Create();
        harness.Store.FailGet = true;

        await harness.Manager.StartAsync(Token).ConfigureAwait(true);

        harness.Manager.State.ShouldBe(AccountState.SignedOut);
    }

    [Fact]
    public async Task ATokenIsReusedWhileFreshAndRefreshedOnceTheMarginIsReached()
    {
        using var harness = AccountsHarness.Create();
        GoTrueSession issued = harness.Handler.IssueSession(Email);
        await harness.Manager.SignInAsync(issued, harness.Options.Issuer, Token).ConfigureAwait(true);

        harness.Time.Advance(TimeSpan.FromMinutes(47));
        AuthResult<SecretString> fresh = await harness.Manager.GetAccessTokenAsync(Token).ConfigureAwait(true);
        harness.Time.Advance(TimeSpan.FromMinutes(2));
        AuthResult<SecretString> stale = await harness.Manager.GetAccessTokenAsync(Token).ConfigureAwait(true);

        fresh.Value.Reveal().ShouldBe(issued.AccessToken.Reveal());
        string.Equals(stale.Value.Reveal(), issued.AccessToken.Reveal(), StringComparison.Ordinal).ShouldBeFalse();
        harness.Handler.Count("token:refresh").ShouldBe(1);
    }

    [Fact]
    public async Task WhenSignedOutATokenCallFailsWithSessionEndedWithoutANetworkCall()
    {
        using var harness = AccountsHarness.Create();

        AuthResult<SecretString> token = await harness.Manager.GetAccessTokenAsync(Token).ConfigureAwait(true);

        token.Failure.Kind.ShouldBe(AuthErrorKind.SessionEnded);
        harness.Handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task TheGuardIsAcquiredThenTheStoreIsReReadThenAnotherProcessesTokenIsAdopted()
    {
        using var harness = AccountsHarness.Create(guardFactory: journal => new RecordingRefreshGuard(journal));
        GoTrueSession issued = harness.Handler.IssueSession(Email);
        await harness.Manager.SignInAsync(issued, harness.Options.Issuer, Token).ConfigureAwait(true);

        // Another process rotates the token and stores the result.
        AuthResult<GoTrueSession> byOther = await harness.Client.RefreshAsync(issued.RefreshToken, Token).ConfigureAwait(true);
        await harness.SeedStoredSessionAsync(byOther.Value).ConfigureAwait(true);
        harness.Time.Advance(TimeSpan.FromMinutes(50));
        int mark = harness.Journal.Entries.Count;

        AuthResult<SecretString> token = await harness.Manager.GetAccessTokenAsync(Token).ConfigureAwait(true);

        token.IsSuccess.ShouldBeTrue();
        IReadOnlyList<string> after = [.. harness.Journal.Entries.Skip(mark)];
        after[0].ShouldBe("acquire");
        after[1].ShouldBe("get:autonogrammer.account.session");
        int released = after.ToList().FindIndex(entry => string.Equals(entry, "release", StringComparison.Ordinal));
        int stored = after.ToList().FindIndex(entry => entry.StartsWith("set:", StringComparison.Ordinal));
        stored.ShouldBeGreaterThan(1);
        released.ShouldBeGreaterThan(stored);
        RecordedRequest request = harness.Handler.Requests.Last(r => r.PathIs("token"));
        request.Body.ShouldContain(byOther.Value.RefreshToken.Reveal());
        request.Body.ShouldNotContain(issued.RefreshToken.Reveal());
        harness.Handler.LiveSessions(Email).ShouldBe(1);
    }

    [Fact]
    public async Task ADefinitiveFailureReReadsTheStoreOnceAndRetriesWithAnAdoptedTokenBeforeSigningOut()
    {
        using var harness = AccountsHarness.Create(guardFactory: journal => new RecordingRefreshGuard(journal));
        GoTrueSession issued = harness.Handler.IssueSession(Email);
        await harness.Manager.SignInAsync(issued, harness.Options.Issuer, Token).ConfigureAwait(true);
        harness.Time.Advance(TimeSpan.FromMinutes(50));
        GoTrueSession other = harness.Handler.IssueSession(Email);
        harness.Handler.ScriptError("token:refresh", 400, "refresh_token_already_used");
        harness.Handler.HoldRefresh();

        Task<AuthResult<SecretString>> pending = harness.Manager.GetAccessTokenAsync(Token).AsTask();
        await TimeDriver.UntilAsync(() => harness.Handler.Count("token:refresh") == 1).ConfigureAwait(true);
        await harness.SeedStoredSessionAsync(other).ConfigureAwait(true);
        harness.Handler.ReleaseRefresh();
        AuthResult<SecretString> token = await pending.ConfigureAwait(true);

        token.IsSuccess.ShouldBeTrue();
        harness.Manager.State.Phase.ShouldBe(AuthPhase.SignedIn);
        harness.Manager.State.Notices.ShouldBe(AccountNotices.None);
        harness.Handler.Count("token:refresh").ShouldBe(2);
        harness.Handler.Requests.Last(request => request.PathIs("token")).Body.ShouldContain(other.RefreshToken.Reveal());
    }

    [Fact]
    public async Task LocalSignOutClearsStateAndStoredSecretBeforeTheServiceIsToldAndSendsScopeLocal()
    {
        using var harness = AccountsHarness.Create();
        GoTrueSession issued = harness.Handler.IssueSession(Email);
        await harness.Manager.SignInAsync(issued, harness.Options.Issuer, Token).ConfigureAwait(true);
        AccountState? stateAtLogout = null;
        harness.Handler.OnLogout = () =>
        {
            stateAtLogout = harness.Manager.State;
            harness.Journal.Add("logout-request");
        };

        AuthResult<AuthUnit> result = await harness.Manager.SignOutAsync(SignOutScope.Local, Token).ConfigureAwait(true);

        result.IsSuccess.ShouldBeTrue();
        stateAtLogout.ShouldNotBeNull();
        stateAtLogout.Phase.ShouldBe(AuthPhase.SignedOut);
        IReadOnlyList<string> entries = harness.Journal.Entries;
        entries.ToList().FindIndex(entry => string.Equals(entry, "remove:autonogrammer.account.session", StringComparison.Ordinal)).ShouldBeGreaterThanOrEqualTo(0);
        entries.ToList().FindIndex(entry => string.Equals(entry, "remove:autonogrammer.account.session", StringComparison.Ordinal))
            .ShouldBeLessThan(entries.ToList().FindIndex(entry => string.Equals(entry, "logout-request", StringComparison.Ordinal)));
        harness.Handler.Requests.Single(request => request.PathIs("logout")).Query.ShouldBe("scope=local");
        harness.Handler.Requests.Single(request => request.PathIs("logout")).Bearer.ShouldBe(issued.AccessToken.Reveal());
        harness.Manager.State.ShouldBe(AccountState.SignedOut);
        harness.Handler.LiveSessions(Email).ShouldBe(0);
    }

    [Fact]
    public async Task SignOutWithAnExpiredAccessTokenRefreshesOnceBeforeRevoking()
    {
        using var harness = AccountsHarness.Create();
        GoTrueSession issued = harness.Handler.IssueSession(Email);
        await harness.Manager.SignInAsync(issued, harness.Options.Issuer, Token).ConfigureAwait(true);
        harness.Time.Advance(TimeSpan.FromHours(2));

        AuthResult<AuthUnit> result = await harness.Manager.SignOutAsync(SignOutScope.Local, Token).ConfigureAwait(true);

        result.IsSuccess.ShouldBeTrue();
        harness.Handler.Count("token:refresh").ShouldBe(1);
        harness.Handler.Count("logout").ShouldBe(1);
        harness.Handler.LiveSessions(Email).ShouldBe(0);
    }

    [Fact]
    public async Task AnUnconfirmedSignOutLeavesTheDeviceSignedOutWithTheNotice()
    {
        using var harness = AccountsHarness.Create();
        await harness.Manager.SignInAsync(harness.Handler.IssueSession(Email), harness.Options.Issuer, Token).ConfigureAwait(true);
        harness.Handler.ScriptTransportFailure("logout");

        AuthResult<AuthUnit> result = await harness.Manager.SignOutAsync(SignOutScope.Local, Token).ConfigureAwait(true);

        result.IsSuccess.ShouldBeFalse();
        result.Failure.Kind.ShouldBe(AuthErrorKind.NetworkUnavailable);
        harness.Manager.State.Phase.ShouldBe(AuthPhase.SignedOut);
        harness.Manager.State.Notices.ShouldBe(AccountNotices.SignOutNotConfirmed);
        (await harness.Store.ReadSessionJsonAsync().ConfigureAwait(true)).ShouldBeNull();
    }

    [Fact]
    public async Task SignOutWithoutAClientStillSignsOutLocally()
    {
        using var harness = AccountsHarness.Create(configured: false);
        await harness.SeedStoredSessionAsync(harness.Handler.IssueSession(Email)).ConfigureAwait(true);
        await harness.Manager.StartAsync(Token).ConfigureAwait(true);

        AuthResult<AuthUnit> result = await harness.Manager.SignOutAsync(SignOutScope.Local, Token).ConfigureAwait(true);

        result.Failure.Kind.ShouldBe(AuthErrorKind.ConfigUnavailable);
        harness.Manager.State.Phase.ShouldBe(AuthPhase.SignedOut);
        harness.Manager.State.Notices.ShouldBe(AccountNotices.SignOutNotConfirmed);
        (await harness.Store.ReadSessionJsonAsync().ConfigureAwait(true)).ShouldBeNull();
    }

    [Fact]
    public async Task GlobalSignOutRevokesEverySessionAndOthersKeepsThisOneSignedIn()
    {
        using var harness = AccountsHarness.Create();
        GoTrueSession mine = harness.Handler.IssueSession(Email);
        GoTrueSession elsewhere = harness.Handler.IssueSession(Email);
        await harness.Manager.SignInAsync(mine, harness.Options.Issuer, Token).ConfigureAwait(true);

        AuthResult<AuthUnit> others = await harness.Manager.SignOutAsync(SignOutScope.Others, Token).ConfigureAwait(true);

        others.IsSuccess.ShouldBeTrue();
        harness.Manager.State.Phase.ShouldBe(AuthPhase.SignedIn);
        harness.Handler.IsAccessTokenLive(mine.AccessToken.Reveal()).ShouldBeTrue();
        harness.Handler.IsAccessTokenLive(elsewhere.AccessToken.Reveal()).ShouldBeFalse();
        harness.Handler.Requests.Single(request => request.PathIs("logout")).Query.ShouldBe("scope=others");

        AuthResult<AuthUnit> global = await harness.Manager.SignOutAsync(SignOutScope.Global, Token).ConfigureAwait(true);

        global.IsSuccess.ShouldBeTrue();
        harness.Manager.State.Phase.ShouldBe(AuthPhase.SignedOut);
        harness.Handler.Requests.Last(request => request.PathIs("logout")).Query.ShouldBe("scope=global");
        harness.Handler.LiveSessions(Email).ShouldBe(0);
    }

    [Fact]
    public async Task SigningOutWhileSignedOutIsASuccessWithoutANetworkCall()
    {
        using var harness = AccountsHarness.Create();

        AuthResult<AuthUnit> result = await harness.Manager.SignOutAsync(SignOutScope.Local, Token).ConfigureAwait(true);

        result.IsSuccess.ShouldBeTrue();
        harness.Handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task ASignOutDuringARefreshWinsAndTheLateResponseNeverBringsTheSessionBack()
    {
        using var harness = AccountsHarness.Create();
        GoTrueSession issued = harness.Handler.IssueSession(Email);
        await harness.Manager.SignInAsync(issued, harness.Options.Issuer, Token).ConfigureAwait(true);
        harness.Time.Advance(TimeSpan.FromMinutes(50));
        harness.Handler.HoldRefresh();

        Task<AuthResult<SecretString>> pending = harness.Manager.GetAccessTokenAsync(Token).AsTask();
        await TimeDriver.UntilAsync(() => harness.Handler.Count("token:refresh") == 1).ConfigureAwait(true);
        _ = await harness.Manager.SignOutAsync(SignOutScope.Local, Token).ConfigureAwait(true);
        harness.Handler.ReleaseRefresh();
        _ = await pending.ConfigureAwait(true);

        harness.Manager.State.Phase.ShouldBe(AuthPhase.SignedOut);
        (await harness.Store.ReadSessionJsonAsync().ConfigureAwait(true)).ShouldBeNull();
    }

    [Fact]
    public async Task StateChangesAreRaisedInOrderAndCarryTheIdentity()
    {
        using var harness = AccountsHarness.Create();
        var seen = new List<AccountState>();
        harness.Manager.StateChanged += (_, e) => seen.Add(e.State);

        await harness.Manager.SignInAsync(harness.Handler.IssueSession(Email), harness.Options.Issuer, Token).ConfigureAwait(true);
        _ = await harness.Manager.SignOutAsync(SignOutScope.Local, Token).ConfigureAwait(true);

        seen.Select(state => state.Phase).ShouldBe([AuthPhase.SignedIn, AuthPhase.SignedOut]);
        seen[0].Identity!.Email.ShouldBe(Email);
    }

    [Fact]
    public async Task SignOutRefusesAnUndefinedScopeAsAProgrammingError()
    {
        using var harness = AccountsHarness.Create();

        await Should.ThrowAsync<ArgumentOutOfRangeException>(() => harness.Manager.SignOutAsync((SignOutScope)42, Token)).ConfigureAwait(true);
    }

    [Fact]
    public async Task AStoredTokenReadBeforeASignOutIsNeverAdoptedAfterItAndTheOldAccessTokenIsNotHandedOut()
    {
        using var harness = AccountsHarness.Create();
        await harness.Manager.SignInAsync(harness.Handler.IssueSession(Email), harness.Options.Issuer, Token).ConfigureAwait(true);
        harness.Time.Advance(TimeSpan.FromMinutes(50));
        await harness.SeedStoredSessionAsync(harness.Handler.IssueSession(Email)).ConfigureAwait(true);
        TaskCompletionSource reached = harness.Store.HoldGets();

        Task<AuthResult<SecretString>> pending = harness.Manager.GetAccessTokenAsync(Token).AsTask();
        await reached.Task.WaitAsync(TimeSpan.FromSeconds(30), Token).ConfigureAwait(true);
        _ = await harness.Manager.SignOutAsync(SignOutScope.Local, Token).ConfigureAwait(true);
        harness.Store.ReleaseGets();
        AuthResult<SecretString> token = await pending.ConfigureAwait(true);

        token.IsSuccess.ShouldBeFalse();
        token.Failure.Kind.ShouldBe(AuthErrorKind.SessionEnded);
        harness.Manager.CurrentAccessToken.ShouldBeNull();
        harness.Manager.State.Phase.ShouldBe(AuthPhase.SignedOut);
        (await harness.Manager.GetAccessTokenAsync(Token).ConfigureAwait(true)).Failure.Kind.ShouldBe(AuthErrorKind.SessionEnded);
        (await harness.Store.ReadSessionJsonAsync().ConfigureAwait(true)).ShouldBeNull();
    }

    [Fact]
    public async Task AStoreThatCannotRemoveTheSecretWithAnUnconfirmedRevokeRaisesTheNoticeAfterOneRetryAndNeverThrows()
    {
        using var harness = AccountsHarness.Create();
        await harness.Manager.SignInAsync(harness.Handler.IssueSession(Email), harness.Options.Issuer, Token).ConfigureAwait(true);
        harness.Store.FailRemovals(int.MaxValue);
        harness.Handler.ScriptTransportFailure("logout");

        AuthResult<AuthUnit> result = await harness.Manager.SignOutAsync(SignOutScope.Local, Token).ConfigureAwait(true);

        result.IsSuccess.ShouldBeFalse();
        result.Failure.Kind.ShouldBe(AuthErrorKind.NetworkUnavailable);
        harness.Manager.State.Phase.ShouldBe(AuthPhase.SignedOut);
        harness.Manager.State.Notices.ShouldBe(AccountNotices.SignOutNotConfirmed | AccountNotices.SignOutNotCleared);
        harness.Manager.CurrentAccessToken.ShouldBeNull();
        harness.Store.Events.Count(entry => string.Equals(entry, "remove:autonogrammer.account.session", StringComparison.Ordinal)).ShouldBe(2);
        (await harness.Store.ReadSessionJsonAsync().ConfigureAwait(true)).ShouldNotBeNull();
    }

    [Fact]
    public async Task AStoreThatCannotRemoveTheSecretWithAConfirmedRevokeNeedsNoNotice()
    {
        using var harness = AccountsHarness.Create();
        await harness.Manager.SignInAsync(harness.Handler.IssueSession(Email), harness.Options.Issuer, Token).ConfigureAwait(true);
        harness.Store.FailRemovals(int.MaxValue);

        AuthResult<AuthUnit> result = await harness.Manager.SignOutAsync(SignOutScope.Local, Token).ConfigureAwait(true);

        result.IsSuccess.ShouldBeTrue();
        harness.Manager.State.ShouldBe(AccountState.SignedOut);
        harness.Handler.LiveSessions(Email).ShouldBe(0);
    }

    [Fact]
    public async Task AOneOffRemovalFailureIsRetriedAndLeavesNoClearNotice()
    {
        using var harness = AccountsHarness.Create();
        await harness.Manager.SignInAsync(harness.Handler.IssueSession(Email), harness.Options.Issuer, Token).ConfigureAwait(true);
        harness.Store.FailRemovals(1);
        harness.Handler.ScriptTransportFailure("logout");

        _ = await harness.Manager.SignOutAsync(SignOutScope.Local, Token).ConfigureAwait(true);

        harness.Manager.State.Notices.ShouldBe(AccountNotices.SignOutNotConfirmed);
        (await harness.Store.ReadSessionJsonAsync().ConfigureAwait(true)).ShouldBeNull();
    }

    [Fact]
    public async Task AnExceptionInsideTheRefreshLeavesTheSessionOfflineAndRetriesInsteadOfStayingRefreshing()
    {
        using var harness = AccountsHarness.Create();
        await harness.SeedStoredSessionAsync(harness.Handler.IssueSession(Email)).ConfigureAwait(true);
        int calls = 0;
        using var manager = new AccountSessionManager(
            harness.Store,
            _ => Interlocked.Increment(ref calls) == 1 ? throw new InvalidOperationException("provider failed") : ValueTask.FromResult<GoTrueClient?>(harness.Client),
            harness.Time)
        {
            Jitter = static interval => interval,
        };
        await manager.StartAsync(Token).ConfigureAwait(true);

        AuthResult<AuthUnit> first = await manager.StartupRefreshAsync().ConfigureAwait(true);

        first.Failure.Kind.ShouldBe(AuthErrorKind.Unexpected);
        manager.State.Phase.ShouldBe(AuthPhase.SignedIn);
        manager.State.Connectivity.ShouldBe(AccountConnectivity.Offline);
        await harness.Time.AdvanceUntilAsync(() => manager.State.Connectivity == AccountConnectivity.Online, TimeSpan.FromSeconds(30)).ConfigureAwait(true);
        harness.Handler.Count("token:refresh").ShouldBe(1);
    }
}
