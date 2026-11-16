using Autonogrammer.Accounts;
using Autonogrammer.Contracts.Accounts;
using Autonogrammer.Contracts.Agents;
using Autonogrammer.Cowork;
using Autonogrammer.Cowork.Sessions;
using Autonogrammer.Harness.Secrets;
using Autonogrammer.Native.Cowork;
using Autonogrammer.Native.Tests.AgentLoop;
using Autonogrammer.Presentation.AgentPanel;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Cowork;

// The CoWork host follows the signed-in account: a switch or a sign-out releases the previous account's sessions and registry,
// and a session one account created cannot be opened by another.
public sealed class CoworkAccountSwitchTests
{
    [Fact]
    public async Task SwitchingAccountsGivesEachAccountItsOwnRegistry()
    {
        using var root = new TempDir();
        var ct = TestContext.Current.CancellationToken;
        var account = new ScriptedAccount(SignedIn("account-a"));
        var host = Host(root.Path, account);
        try
        {
            var sessionA = await host.CreateSessionAsync(ct).ConfigureAwait(true);
            account.Change(SignedIn("account-b"));
            await SettleAsync(host).ConfigureAwait(true);
            var sessionB = await host.CreateSessionAsync(ct).ConfigureAwait(true);

            Ids(root.Path, account).ShouldBe([sessionB]);
            account.Change(SignedIn("account-a"));
            await SettleAsync(host).ConfigureAwait(true);
            Ids(root.Path, account).ShouldBe([sessionA]);
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task SignedOutHostRefusesStorageAndKeepsTheAccountsRegistry()
    {
        using var root = new TempDir();
        var ct = TestContext.Current.CancellationToken;
        var account = new ScriptedAccount(SignedIn("account-a"));
        var host = Host(root.Path, account);
        try
        {
            var sessionA = await host.CreateSessionAsync(ct).ConfigureAwait(true);

            account.Change(AccountState.SignedOut);
            await SettleAsync(host).ConfigureAwait(true);

            await Should.ThrowAsync<InvalidOperationException>(() => host.CreateSessionAsync(ct)).ConfigureAwait(true);
            account.Change(SignedIn("account-a"));
            await SettleAsync(host).ConfigureAwait(true);
            Ids(root.Path, account).ShouldBe([sessionA]);
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task AccountSwitchWithoutItsEventIsRefusedUntilTheEventArrives()
    {
        using var root = new TempDir();
        var ct = TestContext.Current.CancellationToken;
        var account = new ScriptedAccount(SignedIn("account-a"));
        var host = Host(root.Path, account);
        try
        {
            _ = await host.CreateSessionAsync(ct).ConfigureAwait(true);

            account.ChangeWithoutEvent(SignedIn("account-b"));
            await Should.ThrowAsync<InvalidOperationException>(() => host.CreateSessionAsync(ct)).ConfigureAwait(true);

            account.RaiseChanged();
            await SettleAsync(host).ConfigureAwait(true);
            var sessionB = await host.CreateSessionAsync(ct).ConfigureAwait(true);
            Ids(root.Path, account).ShouldBe([sessionB]);
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task ASwitchedAccountCannotOpenASessionAnotherAccountCreated()
    {
        using var root = new TempDir();
        var ct = TestContext.Current.CancellationToken;
        var account = new ScriptedAccount(SignedIn("account-a"));
        var host = Host(root.Path, account, new ScriptedOmp());
        try
        {
            var sessionA = await host.CreateSessionAsync(ct).ConfigureAwait(true);
            _ = await host.OpenSessionAsync(Row(sessionA), ct).ConfigureAwait(true);
            host.ConversationOf(sessionA).ShouldNotBeNull();
            Directory.Exists(SessionDirectory(root.Path, "account-a", sessionA)).ShouldBeTrue();

            account.Change(SignedIn("account-b"));
            await SettleAsync(host).ConfigureAwait(true);

            await Should.ThrowAsync<InvalidOperationException>(() => host.OpenSessionAsync(Row(sessionA), ct)).ConfigureAwait(true);
            host.ConversationOf(sessionA).ShouldBeNull();
            Directory.Exists(SessionDirectory(root.Path, "account-b", sessionA)).ShouldBeFalse();
            Directory.Exists(Path.Combine(root.Path, "cowork-sessions", sessionA)).ShouldBeFalse();
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task ASignedOutHostCannotOpenASession()
    {
        using var root = new TempDir();
        var ct = TestContext.Current.CancellationToken;
        var account = new ScriptedAccount(SignedIn("account-a"));
        var host = Host(root.Path, account, new ScriptedOmp());
        try
        {
            var sessionA = await host.CreateSessionAsync(ct).ConfigureAwait(true);
            _ = await host.OpenSessionAsync(Row(sessionA), ct).ConfigureAwait(true);

            account.Change(AccountState.SignedOut);
            await SettleAsync(host).ConfigureAwait(true);

            await Should.ThrowAsync<InvalidOperationException>(() => host.OpenSessionAsync(Row(sessionA), ct)).ConfigureAwait(true);
            host.ConversationOf(sessionA).ShouldBeNull();
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(true);
        }
    }

    // SignOutAsync queues behind the account release, so awaiting it waits for the release. The host has no engines in the tests that do not open a session.
    private static Task SettleAsync(CoworkSessionHost host) => host.SignOutAsync(confirmed: true);

    private static CoworkSessionHost Host(string root, IAccountSession account, ScriptedOmp? omp = null) =>
        new(new CoworkSessionHostOptions
        {
            StateRoot = root,
            WorkingDirectory = root,
            Gate = new NativeCoworkAccountGate(account),
            AccountSession = account,
            TransportFactory = omp?.TransportFactory,
        });

    private static IReadOnlyList<string> Ids(string root, IAccountSession account) =>
        [.. CoworkSessionRegistry.Open(new CoworkSessionOptions { StateRoot = root }, new NativeCoworkAccountGate(account), TimeProvider.System).List().Select(static row => row.Id)];

    // An account's session files live in its folder, under cowork-sessions.
    private static string SessionDirectory(string root, string userId, string id) =>
        Path.Combine(CoworkAccountFolder.AccountDirectory(root, CoworkAccountFolder.KeyFromSubject(userId)), "cowork-sessions", id);

    private static AgentRow Row(string id) => new()
    {
        Id = id,
        Title = "task",
        Summary = new SessionSummary { Id = id, Title = "task", Lifecycle = SessionLifecycle.Created, CreatedAt = DateTimeOffset.UnixEpoch, LastActivity = DateTimeOffset.UnixEpoch },
        Status = AgentRowStatus.Ready,
        Group = AgentRowGroup.Idle,
        StatusText = "Ready",
        IconKey = "ready",
        LastActivityText = "now",
        IsLongRunning = false,
        IsStale = false,
        IsLost = false,
        AccessibleName = "task",
    };

    private static AccountState SignedIn(string userId) =>
        new(AuthPhase.SignedIn, new AccountIdentity(userId, null), AccountConnectivity.Online, AccountNotices.None);

    private sealed class ScriptedAccount(AccountState state) : IAccountSession
    {
        private AccountState _state = state;

        public event EventHandler<AccountStateChangedEventArgs>? StateChanged;

        public AccountState State => _state;

        public void Change(AccountState next)
        {
            ChangeWithoutEvent(next);
            RaiseChanged();
        }

        public void ChangeWithoutEvent(AccountState next) => _state = next;

        public void RaiseChanged() => StateChanged?.Invoke(this, new AccountStateChangedEventArgs(_state));

        public ValueTask<AuthResult<SecretString>> GetAccessTokenAsync(CancellationToken cancellationToken)
        {
            _ = cancellationToken;
            return ValueTask.FromResult(AuthResult.Fail<SecretString>(AuthErrorKind.SessionEnded));
        }

        public Task<AuthResult<AuthUnit>> SignOutAsync(SignOutScope scope, CancellationToken cancellationToken)
        {
            _ = scope;
            _ = cancellationToken;
            return Task.FromResult(AuthResult.Fail<AuthUnit>(AuthErrorKind.SessionEnded));
        }
    }

    private sealed class TempDir : IDisposable
    {
        public TempDir()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cowork-account-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, true);
    }
}
