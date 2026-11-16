using Autonogrammer.Accounts.Tests.Support;
using Autonogrammer.Contracts.Accounts;
using Autonogrammer.Harness.Secrets;
using Shouldly;
using Xunit;

namespace Autonogrammer.Accounts.Tests;

/// <summary>
/// Two managers over one secret store and one refresh guard stand in for two desktop processes. A refresh in one never overwrites, or removes, a newer
/// sign-in the other stored, and never brings back a session the other signed out.
/// </summary>
public sealed class AccountSessionCrossProcessTests
{
    private const string Email = "first@example.org";
    private const string OtherEmail = "second@example.org";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ASignInInAnotherProcessWaitsForARunningRefreshAndIsTheSessionLeftInTheStore()
    {
        using var harness = AccountsHarness.Create();
        using var guard = new SharedGuard();
        using AccountSessionManager first = harness.NewManager(guard);
        using AccountSessionManager second = harness.NewManager(guard);
        GoTrueSession older = harness.Handler.IssueSession(Email);
        await first.SignInAsync(older, harness.Options.Issuer, Token).ConfigureAwait(true);
        harness.Time.Advance(TimeSpan.FromMinutes(50));
        harness.Handler.HoldRefresh();

        Task<AuthResult<SecretString>> refreshing = first.GetAccessTokenAsync(Token).AsTask();
        await TimeDriver.UntilAsync(() => harness.Handler.Count("token:refresh") == 1).ConfigureAwait(true);
        GoTrueSession newer = harness.Handler.IssueSession(OtherEmail);
        Task signingIn = second.SignInAsync(newer, harness.Options.Issuer, Token);
        harness.Handler.ReleaseRefresh();
        (await refreshing.ConfigureAwait(true)).IsSuccess.ShouldBeTrue();
        await signingIn.ConfigureAwait(true);

        string? stored = await harness.Store.ReadSessionJsonAsync().ConfigureAwait(true);
        stored.ShouldNotBeNull();
        stored.ShouldContain(newer.RefreshToken.Reveal());
        stored.ShouldContain(newer.UserId);
        stored.ShouldNotContain(older.UserId);
    }

    [Fact]
    public async Task ARefreshThatFindsANewerSignInStoredLeavesItAndKeepsItsOwnSessionInMemoryOnly()
    {
        using var harness = AccountsHarness.Create();
        await harness.Manager.SignInAsync(harness.Handler.IssueSession(Email), harness.Options.Issuer, Token).ConfigureAwait(true);
        harness.Time.Advance(TimeSpan.FromMinutes(50));
        harness.Handler.HoldRefresh();

        Task<AuthResult<SecretString>> refreshing = harness.Manager.GetAccessTokenAsync(Token).AsTask();
        await TimeDriver.UntilAsync(() => harness.Handler.Count("token:refresh") == 1).ConfigureAwait(true);
        GoTrueSession newer = harness.Handler.IssueSession(OtherEmail);
        await harness.SeedStoredSessionAsync(newer).ConfigureAwait(true);
        harness.Handler.ReleaseRefresh();
        (await refreshing.ConfigureAwait(true)).IsSuccess.ShouldBeTrue();

        string? stored = await harness.Store.ReadSessionJsonAsync().ConfigureAwait(true);
        stored.ShouldNotBeNull();
        stored.ShouldContain(newer.RefreshToken.Reveal());
        stored.ShouldContain(newer.UserId);
        harness.Manager.State.Phase.ShouldBe(AuthPhase.SignedIn);
        harness.Manager.State.Notices.ShouldBe(AccountNotices.SessionNotRemembered);
    }

    [Fact]
    public async Task ARefreshDoesNotBringBackASessionAnotherProcessSignedOut()
    {
        using var harness = AccountsHarness.Create();
        await harness.Manager.SignInAsync(harness.Handler.IssueSession(Email), harness.Options.Issuer, Token).ConfigureAwait(true);
        harness.Time.Advance(TimeSpan.FromMinutes(50));
        harness.Handler.HoldRefresh();

        Task<AuthResult<SecretString>> refreshing = harness.Manager.GetAccessTokenAsync(Token).AsTask();
        await TimeDriver.UntilAsync(() => harness.Handler.Count("token:refresh") == 1).ConfigureAwait(true);
        _ = await harness.Store.RemoveAsync(AccountSessionManager.SecretName, Token).ConfigureAwait(true);
        harness.Handler.ReleaseRefresh();
        _ = await refreshing.ConfigureAwait(true);

        (await harness.Store.ReadSessionJsonAsync().ConfigureAwait(true)).ShouldBeNull();
    }

    [Fact]
    public async Task ARefreshWhoseStoredSessionIsUnchangedStillStoresTheRotatedToken()
    {
        using var harness = AccountsHarness.Create();
        GoTrueSession issued = harness.Handler.IssueSession(Email);
        await harness.Manager.SignInAsync(issued, harness.Options.Issuer, Token).ConfigureAwait(true);
        harness.Time.Advance(TimeSpan.FromMinutes(50));

        AuthResult<SecretString> token = await harness.Manager.GetAccessTokenAsync(Token).ConfigureAwait(true);

        token.IsSuccess.ShouldBeTrue();
        string? stored = await harness.Store.ReadSessionJsonAsync().ConfigureAwait(true);
        stored.ShouldNotBeNull();
        stored.ShouldNotContain(issued.RefreshToken.Reveal());
        harness.Manager.State.Notices.ShouldBe(AccountNotices.None);
    }

    [Fact]
    public async Task ASessionThatCannotBeRefreshedAnyMoreDoesNotRemoveANewerSignInAnotherProcessStored()
    {
        using var harness = AccountsHarness.Create();
        await harness.Manager.SignInAsync(harness.Handler.IssueSession(Email), harness.Options.Issuer, Token).ConfigureAwait(true);
        harness.Time.Advance(TimeSpan.FromMinutes(50));
        harness.Handler.ScriptError("token:refresh", 400, "refresh_token_not_found");
        harness.Handler.HoldRefresh();

        Task<AuthResult<SecretString>> refreshing = harness.Manager.GetAccessTokenAsync(Token).AsTask();
        await TimeDriver.UntilAsync(() => harness.Handler.Count("token:refresh") == 1).ConfigureAwait(true);
        GoTrueSession newer = harness.Handler.IssueSession(OtherEmail);
        await harness.SeedStoredSessionAsync(newer).ConfigureAwait(true);
        harness.Handler.ReleaseRefresh();
        (await refreshing.ConfigureAwait(true)).IsSuccess.ShouldBeFalse();

        harness.Manager.State.Phase.ShouldBe(AuthPhase.SignedOut);
        harness.Manager.State.Notices.ShouldBe(AccountNotices.SessionEnded);
        string? stored = await harness.Store.ReadSessionJsonAsync().ConfigureAwait(true);
        stored.ShouldNotBeNull();
        stored.ShouldContain(newer.RefreshToken.Reveal());
    }

    [Fact]
    public async Task ASignOutDoesNotWaitLongBehindAnotherProcessesRefresh()
    {
        using var harness = AccountsHarness.Create();
        using var guard = new SharedGuard();
        using AccountSessionManager manager = harness.NewManager(guard);
        await manager.SignInAsync(harness.Handler.IssueSession(Email), harness.Options.Issuer, Token).ConfigureAwait(true);
        IAsyncDisposable heldByOther = await guard.AcquireAsync(Token).ConfigureAwait(true);

        Task<AuthResult<AuthUnit>> signingOut = manager.SignOutAsync(SignOutScope.Local, Token);
        await harness.Time.AdvanceUntilAsync(() => signingOut.IsCompleted, TimeSpan.FromSeconds(1)).ConfigureAwait(true);
        AuthResult<AuthUnit> result = await signingOut.ConfigureAwait(true);
        await heldByOther.DisposeAsync().ConfigureAwait(true);

        result.IsSuccess.ShouldBeTrue();
        manager.State.Phase.ShouldBe(AuthPhase.SignedOut);
        (await harness.Store.ReadSessionJsonAsync().ConfigureAwait(true)).ShouldBeNull();
    }

    /// <summary>One lock shared by the managers of a test, as the lock file is shared by two app instances.</summary>
    private sealed class SharedGuard : IRefreshGuard, IDisposable
    {
        private readonly SemaphoreSlim _gate = new(1, 1);

        public void Dispose() => _gate.Dispose();

        public async ValueTask<IAsyncDisposable> AcquireAsync(CancellationToken cancellationToken)
        {
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            return new Handle(_gate);
        }

        private sealed class Handle(SemaphoreSlim gate) : IAsyncDisposable
        {
            public ValueTask DisposeAsync()
            {
                _ = gate.Release();
                return ValueTask.CompletedTask;
            }
        }
    }
}
