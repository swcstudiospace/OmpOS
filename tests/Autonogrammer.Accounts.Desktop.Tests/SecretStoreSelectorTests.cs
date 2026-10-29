using Autonogrammer.Contracts.Accounts;
using Autonogrammer.Harness.Secrets;
using Shouldly;
using Xunit;

namespace Autonogrammer.Accounts.Desktop.Tests;

/// <summary>The secret-store selector: first-launch silence, the degrade paths with consent, and the device key that never reaches the file fallback (AG-67-11, AG-67-12, D-26).</summary>
public sealed class SecretStoreSelectorTests : IDisposable
{
    private const string Email = "person@example.com";
    private const string Issuer = "https://fake-project.supabase.co/auth/v1";
    private const string DeviceKey = AccountSecretStoreSelector.DeviceKeyPrefix + ".device-1";

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "ag-67-04-sel-" + Guid.NewGuid().ToString("N"));

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public async Task AFirstLaunchWithNoStoredSessionMakesNoKeyringCallAtAll()
    {
        var os = new CountingStore();
        var file = new CountingStore();
        using var selector = new AccountSecretStoreSelector(os, file, hasStoredSession: false, allowFileSecretStore: false);
        using AccountSessionManager manager = NewManager(selector, new ManualTimeProvider());

        await manager.StartAsync(Token).ConfigureAwait(true);
        (await selector.GetAsync(AccountSessionManager.SecretName, Token).ConfigureAwait(true)).ShouldBeNull();
        (await selector.ListNamesAsync(Token).ConfigureAwait(true)).ShouldBeEmpty();
        (await selector.RemoveAsync(AccountSessionManager.SecretName, Token).ConfigureAwait(true)).ShouldBeFalse();

        manager.State.ShouldBe(AccountState.SignedOut);
        os.Calls.ShouldBe(0);
        file.Calls.ShouldBe(0);
        selector.Mode.ShouldBe(AccountSecretStoreMode.NotAsked);
        selector.HasStoredSession.ShouldBeFalse();
    }

    [Fact]
    public async Task ASignInWritesToTheSystemStoreAndRecordsAStoredSession()
    {
        var os = new CountingStore();
        using var selector = new AccountSecretStoreSelector(os, new CountingStore(), hasStoredSession: false, allowFileSecretStore: false);
        int changes = 0;
        selector.Changed += (_, _) => changes++;
        var time = new ManualTimeProvider();
        using AccountSessionManager manager = NewManager(selector, time);

        await manager.SignInAsync(Session(time, "rt-1"), Issuer, Token).ConfigureAwait(true);

        os.Calls.ShouldBeGreaterThanOrEqualTo(1);
        os.Contains(AccountSessionManager.SecretName).ShouldBeTrue();
        selector.Mode.ShouldBe(AccountSecretStoreMode.OperatingSystem);
        selector.HasStoredSession.ShouldBeTrue();
        manager.State.Notices.ShouldBe(AccountNotices.None);
        changes.ShouldBe(1);

        await manager.SignOutAsync(SignOutScope.Local, Token).ConfigureAwait(true);

        selector.HasStoredSession.ShouldBeFalse();
        os.Contains(AccountSessionManager.SecretName).ShouldBeFalse();
    }

    [Theory]
    [InlineData(SecretStoreFailure.Unavailable)]
    [InlineData(SecretStoreFailure.Denied)]
    [InlineData(SecretStoreFailure.Locked)]
    public async Task AFailingSystemStoreMakesTheSessionMemoryOnlyWithTheNoticeAndConsentSwitchesToTheOwnerOnlyFile(SecretStoreFailure failure)
    {
        var os = new CountingStore { Failure = failure };
        string path = Path.Combine(_directory, "account.secrets");
        var file = new OwnerOnlyFileSecretStore(path);
        using var selector = new AccountSecretStoreSelector(os, file, hasStoredSession: false, allowFileSecretStore: false);
        int changes = 0;
        selector.Changed += (_, _) => changes++;
        var time = new ManualTimeProvider();
        using AccountSessionManager manager = NewManager(selector, time);

        await manager.SignInAsync(Session(time, "rt-1"), Issuer, Token).ConfigureAwait(true);

        manager.State.Phase.ShouldBe(AuthPhase.SignedIn);
        manager.State.Notices.ShouldBe(AccountNotices.SessionNotRemembered);
        selector.Mode.ShouldBe(AccountSecretStoreMode.MemoryOnly);
        selector.HasStoredSession.ShouldBeFalse();
        selector.AllowFileSecretStore.ShouldBeFalse();
        (await selector.GetAsync(AccountSessionManager.SecretName, Token).ConfigureAwait(true)).ShouldNotBeNull();
        File.Exists(path).ShouldBeFalse();
        int osCallsAfterDegrade = os.Calls;

        await selector.RememberWithoutSystemProtectionAsync(Token).ConfigureAwait(true);

        selector.AllowFileSecretStore.ShouldBeTrue();
        selector.Mode.ShouldBe(AccountSecretStoreMode.OwnerOnlyFile);
        selector.HasStoredSession.ShouldBeTrue();
        (await file.GetAsync(AccountSessionManager.SecretName, Token).ConfigureAwait(true)).ShouldNotBeNull();
        changes.ShouldBeGreaterThanOrEqualTo(2);

        await manager.SignInAsync(Session(time, "rt-2"), Issuer, Token).ConfigureAwait(true);

        manager.State.Notices.ShouldBe(AccountNotices.None);
        os.Calls.ShouldBe(osCallsAfterDegrade);
        (await file.GetAsync(AccountSessionManager.SecretName, Token).ConfigureAwait(true))!.Reveal().ShouldContain("rt-2");

        // After a restart the settings are the stored values and the file alone restores the session; the system store is never asked.
        using var restarted = new AccountSecretStoreSelector(os, new OwnerOnlyFileSecretStore(path), selector.HasStoredSession, selector.AllowFileSecretStore);
        using AccountSessionManager again = NewManager(restarted, time);
        await again.StartAsync(Token).ConfigureAwait(true);
        again.State.Phase.ShouldBe(AuthPhase.SignedIn);
        os.Calls.ShouldBe(osCallsAfterDegrade);
    }

    [Fact]
    public async Task EveryWriteInMemoryOnlyModeKeepsReportingTheFailureSoTheNoticeStays()
    {
        var os = new CountingStore { Failure = SecretStoreFailure.Locked };
        using var selector = new AccountSecretStoreSelector(os, new CountingStore(), hasStoredSession: false, allowFileSecretStore: false);
        var secret = new SecretString("v");

        SecretStoreException first = await Should.ThrowAsync<SecretStoreException>(selector.SetAsync(AccountSessionManager.SecretName, secret, Token)).ConfigureAwait(true);
        SecretStoreException second = await Should.ThrowAsync<SecretStoreException>(selector.SetAsync(AccountSessionManager.SecretName, secret, Token)).ConfigureAwait(true);

        first.Failure.ShouldBe(SecretStoreFailure.Locked);
        second.Failure.ShouldBe(SecretStoreFailure.Locked);
        first.Message.ShouldNotContain(AccountSessionManager.SecretName);
        os.Calls.ShouldBe(1);
        (await selector.GetAsync(AccountSessionManager.SecretName, Token).ConfigureAwait(true))!.Reveal().ShouldBe("v");
    }

    [Fact]
    public async Task ADismissedUnlockPromptWhileReadingAStoredSessionStartsSignedOutInMemoryOnlyMode()
    {
        var os = new CountingStore { Failure = SecretStoreFailure.Locked };
        using var selector = new AccountSecretStoreSelector(os, new CountingStore(), hasStoredSession: true, allowFileSecretStore: false);
        using AccountSessionManager manager = NewManager(selector, new ManualTimeProvider());

        await manager.StartAsync(Token).ConfigureAwait(true);
        (await selector.GetAsync(AccountSessionManager.SecretName, Token).ConfigureAwait(true)).ShouldBeNull();

        manager.State.Phase.ShouldBe(AuthPhase.SignedOut);
        selector.Mode.ShouldBe(AccountSecretStoreMode.MemoryOnly);
        os.Calls.ShouldBe(1);
    }

    [Theory]
    [InlineData(SecretStoreFailure.Corrupt)]
    [InlineData(SecretStoreFailure.Other)]
    public async Task AFailureThatIsNotAnUnavailableStoreIsNotADegradeAndPropagates(SecretStoreFailure failure)
    {
        var os = new CountingStore { Failure = failure };
        using var selector = new AccountSecretStoreSelector(os, new CountingStore(), hasStoredSession: true, allowFileSecretStore: false);

        SecretStoreException thrown = await Should.ThrowAsync<SecretStoreException>(selector.GetAsync(AccountSessionManager.SecretName, Token)).ConfigureAwait(true);

        thrown.Failure.ShouldBe(failure);
        selector.Mode.ShouldBe(AccountSecretStoreMode.OperatingSystem);
    }

    [Fact]
    public async Task TheDeviceKeyNeverReachesTheFileFallbackAndAFailingSystemStoreReportsItUnavailable()
    {
        var os = new CountingStore { Failure = SecretStoreFailure.Unavailable };
        var file = new CountingStore();
        using var selector = new AccountSecretStoreSelector(os, file, hasStoredSession: false, allowFileSecretStore: true);

        SecretStoreException thrown = await Should.ThrowAsync<SecretStoreException>(selector.SetAsync(DeviceKey, new SecretString("private-key"), Token)).ConfigureAwait(true);
        _ = await Should.ThrowAsync<SecretStoreException>(selector.GetAsync(DeviceKey, Token)).ConfigureAwait(true);

        thrown.Failure.ShouldBe(SecretStoreFailure.Unavailable);
        file.Calls.ShouldBe(0);
        os.Calls.ShouldBe(2);
        selector.Mode.ShouldBe(AccountSecretStoreMode.OwnerOnlyFile);
    }

    [Fact]
    public async Task TheDeviceKeyIsNeverKeptInMemoryEitherAndDoesNotChangeTheSessionState()
    {
        var os = new CountingStore { Failure = SecretStoreFailure.Locked };
        using var selector = new AccountSecretStoreSelector(os, new CountingStore(), hasStoredSession: false, allowFileSecretStore: false);

        _ = await Should.ThrowAsync<SecretStoreException>(selector.SetAsync(DeviceKey, new SecretString("private-key"), Token)).ConfigureAwait(true);

        selector.Mode.ShouldBe(AccountSecretStoreMode.NotAsked);
        selector.HasStoredSession.ShouldBeFalse();
        os.Failure = null;
        (await selector.GetAsync(DeviceKey, Token).ConfigureAwait(true)).ShouldBeNull();
        await selector.SetAsync(DeviceKey, new SecretString("private-key"), Token).ConfigureAwait(true);
        os.Contains(DeviceKey).ShouldBeTrue();
        selector.HasStoredSession.ShouldBeFalse();
        selector.Mode.ShouldBe(AccountSecretStoreMode.NotAsked);
    }

    [Fact]
    public async Task ConsentIsIdempotentAndMovesOnlyTheSessionHeldInMemoryIntoTheFile()
    {
        var os = new CountingStore { Failure = SecretStoreFailure.Denied };
        string path = Path.Combine(_directory, "consent.secrets");
        var file = new OwnerOnlyFileSecretStore(path);
        using var selector = new AccountSecretStoreSelector(os, file, hasStoredSession: false, allowFileSecretStore: false);
        await Should.ThrowAsync<SecretStoreException>(selector.SetAsync(AccountSessionManager.SecretName, new SecretString("session"), Token)).ConfigureAwait(true);

        await selector.RememberWithoutSystemProtectionAsync(Token).ConfigureAwait(true);
        await selector.RememberWithoutSystemProtectionAsync(Token).ConfigureAwait(true);

        (await file.ListNamesAsync(Token).ConfigureAwait(true)).ShouldBe([AccountSessionManager.SecretName]);
        (await selector.ListNamesAsync(Token).ConfigureAwait(true)).ShouldBe([AccountSessionManager.SecretName]);
    }

    [Fact]
    public void TheSettingsKeysHaveTheirAgreedNames()
    {
        AccountSettingsKeys.HasStoredSession.ShouldBe("account.hasStoredSession");
        AccountSettingsKeys.AllowFileSecretStore.ShouldBe("account.allowFileSecretStore");
        AccountSecretStoreSelector.DeviceKeyPrefix.ShouldBe("autonogrammer.dispatch.device-key");
    }

    private static AccountSessionManager NewManager(ISecretStore store, TimeProvider time) =>
        new(store, AccountSessionManager.FixedClient(null), time);

    private static GoTrueSession Session(TimeProvider time, string refreshToken) =>
        new(new SecretString("at-" + refreshToken), new SecretString(refreshToken), time.GetUtcNow() + TimeSpan.FromHours(1), TimeSpan.FromHours(1), "user-1", Email);

    private sealed class CountingStore : ISecretStore
    {
        private readonly Lock _gate = new();
        private readonly Dictionary<string, SecretString> _values = new(StringComparer.Ordinal);
        private int _calls;

        public SecretStoreFailure? Failure { get; set; }

        public int Calls => Volatile.Read(ref _calls);

        public bool Contains(string name)
        {
            lock (_gate)
            {
                return _values.ContainsKey(name);
            }
        }

        public Task SetAsync(string name, SecretString secret, CancellationToken cancellationToken)
        {
            Enter();
            lock (_gate)
            {
                _values[name] = secret;
            }

            return Task.CompletedTask;
        }

        public Task<SecretString?> GetAsync(string name, CancellationToken cancellationToken)
        {
            Enter();
            lock (_gate)
            {
                return Task.FromResult(_values.GetValueOrDefault(name));
            }
        }

        public Task<bool> RemoveAsync(string name, CancellationToken cancellationToken)
        {
            Enter();
            lock (_gate)
            {
                return Task.FromResult(_values.Remove(name));
            }
        }

        public Task<IReadOnlyList<string>> ListNamesAsync(CancellationToken cancellationToken)
        {
            Enter();
            lock (_gate)
            {
                return Task.FromResult<IReadOnlyList<string>>([.. _values.Keys.Order(StringComparer.Ordinal)]);
            }
        }

        private void Enter()
        {
            _ = Interlocked.Increment(ref _calls);
            if (Failure is { } failure)
            {
                throw new SecretStoreException("The fake store fails.", failure);
            }
        }
    }
}
