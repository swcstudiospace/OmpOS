#pragma warning disable CA2007
using Autonogrammer.Accounts;
using Autonogrammer.Contracts.Accounts;
using Autonogrammer.Cowork;
using Autonogrammer.Cowork.Sessions;
using Autonogrammer.Harness.Secrets;
using Autonogrammer.Native.Cowork;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Cowork;

// The CoWork host follows the signed-in account: a switch or a sign-out releases the previous account's sessions and registry.
public sealed class CoworkAccountSwitchTests
{
    [Fact]
    public async Task SwitchingAccountsGivesEachAccountItsOwnRegistry()
    {
        using var root = new TempDir();
        var ct = TestContext.Current.CancellationToken;
        var account = new ScriptedAccount(SignedIn("account-a"));
        await using var host = Host(root.Path, account);

        var sessionA = await host.CreateSessionAsync(ct).ConfigureAwait(true);
        account.Change(SignedIn("account-b"));
        await SettleAsync(host).ConfigureAwait(true);
        var sessionB = await host.CreateSessionAsync(ct).ConfigureAwait(true);

        Ids(root.Path, account).ShouldBe([sessionB]);
        account.Change(SignedIn("account-a"));
        await SettleAsync(host).ConfigureAwait(true);
        Ids(root.Path, account).ShouldBe([sessionA]);
    }

    [Fact]
    public async Task SignedOutHostRefusesStorageAndKeepsTheAccountsRegistry()
    {
        using var root = new TempDir();
        var ct = TestContext.Current.CancellationToken;
        var account = new ScriptedAccount(SignedIn("account-a"));
        await using var host = Host(root.Path, account);
        var sessionA = await host.CreateSessionAsync(ct).ConfigureAwait(true);

        account.Change(AccountState.SignedOut);
        await SettleAsync(host).ConfigureAwait(true);

        await Should.ThrowAsync<InvalidOperationException>(() => host.CreateSessionAsync(ct)).ConfigureAwait(true);
        account.Change(SignedIn("account-a"));
        await SettleAsync(host).ConfigureAwait(true);
        Ids(root.Path, account).ShouldBe([sessionA]);
    }

    [Fact]
    public async Task AccountSwitchWithoutItsEventIsRefusedUntilTheEventArrives()
    {
        using var root = new TempDir();
        var ct = TestContext.Current.CancellationToken;
        var account = new ScriptedAccount(SignedIn("account-a"));
        await using var host = Host(root.Path, account);
        _ = await host.CreateSessionAsync(ct).ConfigureAwait(true);

        account.ChangeWithoutEvent(SignedIn("account-b"));
        await Should.ThrowAsync<InvalidOperationException>(() => host.CreateSessionAsync(ct)).ConfigureAwait(true);

        account.RaiseChanged();
        await SettleAsync(host).ConfigureAwait(true);
        var sessionB = await host.CreateSessionAsync(ct).ConfigureAwait(true);
        Ids(root.Path, account).ShouldBe([sessionB]);
    }

    // SignOutAsync queues behind the account release, so awaiting it waits for the release. The host has no engines in these tests, so it stops nothing.
    private static Task SettleAsync(CoworkSessionHost host) => host.SignOutAsync(confirmed: true);

    private static CoworkSessionHost Host(string root, IAccountSession account) =>
        new(new CoworkSessionHostOptions
        {
            StateRoot = root,
            WorkingDirectory = root,
            Gate = new NativeCoworkAccountGate(account),
            AccountSession = account,
        });

    private static IReadOnlyList<string> Ids(string root, IAccountSession account) =>
        [.. CoworkSessionRegistry.Open(new CoworkSessionOptions { StateRoot = root }, new NativeCoworkAccountGate(account), TimeProvider.System).List().Select(static row => row.Id)];

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

#pragma warning restore CA2007
