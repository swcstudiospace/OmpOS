using System.Net;
using Autonogrammer.Accounts;
using Autonogrammer.Contracts.Accounts;
using Autonogrammer.Harness.Secrets;
using Autonogrammer.Native.Composition;
using Autonogrammer.Native.Tests.AgentLoop;
using Autonogrammer.Native.Tests.Settings;
using Autonogrammer.Native.Views.Account;
using Autonogrammer.Presentation.Accounts;
using Avalonia.Controls;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Account;

/// <summary>The five states, notices, startup without the network, and the secret-store bookkeeping.</summary>
public sealed class AccountStateTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>The view draws exactly one state, with the awaiting-link controls and the notices.</summary>
    [Fact]
    public async Task TheViewDrawsOneStateAndTheNotices()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var host = new FakeAccountHost();
            var model = new AccountViewModel(host, new ManualTimeProvider(DateTimeOffset.UnixEpoch));
            var view = new AccountView();
            var window = new Window { Content = view, Width = 480, Height = 720 };
            view.Attach(model);
            window.Show();
            SettingsTree.SettleSafe(window);
            view.SendCode.IsVisible.ShouldBeTrue();
            view.CodeField.IsVisible.ShouldBeFalse();

            host.Set(AccountHostState.SignedOut with
            {
                Phase = AuthPhase.AwaitingLink,
                Email = "a@example.test",
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(2),
                ResendAvailableAt = DateTimeOffset.UtcNow.AddSeconds(60),
                LoopbackUnavailable = true,
            });
            SettingsTree.SettleSafe(window);
            view.CodeField.IsVisible.ShouldBeTrue();
            view.Resend.IsVisible.ShouldBeTrue();
            view.ChangeEmail.IsVisible.ShouldBeTrue();
            model.ExpiryText.ShouldNotBeNullOrWhiteSpace();
            model.CooldownText.ShouldNotBeNullOrWhiteSpace();
            model.LoopbackSentence.ShouldBe(AccountCopy.LoopbackUnavailable);

            host.Set(host.State with { MustResend = true, RemainingAttempts = 0 });
            SettingsTree.SettleSafe(window);
            view.CodeField.IsEnabled.ShouldBeFalse();
            model.MustResendSentence.ShouldBe(AccountCopy.MustResend);

            host.Set(AccountHostState.SignedOut with
            {
                Phase = AuthPhase.SignedIn,
                Email = "a@example.test",
                Offline = true,
                SessionNotRemembered = true,
                SignOutNotConfirmed = true,
                ServiceChanged = true,
                DevicesAvailable = false,
            });
            SettingsTree.SettleSafe(window);
            model.ShowSignedIn.ShouldBeTrue();
            model.ShowOffline.ShouldBeTrue();
            view.Remember.IsVisible.ShouldBeTrue();
            view.Devices.IsVisible.ShouldBeFalse();
            model.SignOutNotConfirmedSentence.ShouldBe(AccountCopy.SignOutNotConfirmedNotice);
            model.ServiceChangedSentence.ShouldBe(AccountCopy.ServiceChangedNotice);
            window.Close();
        }, Token).ConfigureAwait(true);
    }

    /// <summary>A stored signed-in session is shown without a network call.</summary>
    [Fact]
    public async Task AStoredSessionShowsSignedInWithoutTheNetwork()
    {
        using var tree = new TempTree();
        var handler = new CountingHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var session = new ScriptedSession(AccountState.SignedOut with { Phase = AuthPhase.SignedIn, Identity = new AccountIdentity("user", "a@example.test") }, "account-token");
        var services = await HeadServices.CreateAsync(Options(tree, handler, session), Token).ConfigureAwait(true);
        await using var scope = services.ConfigureAwait(true);

        services.Account.ShouldNotBeNull();
        services.Account!.State.Phase.ShouldBe(AuthPhase.SignedIn);
        handler.Calls.ShouldBe(0);
        (await services.ReadApiTokenAsync(Token).ConfigureAwait(true)).ShouldBe("account-token");
    }

    /// <summary>With no stored session the head does not call the secret store. Consent writes the file flag and the next secret write uses the file store.</summary>
    [Fact]
    public async Task FirstLaunchSkipsTheStoreAndConsentUsesTheFileStore()
    {
        using var tree = new TempTree();
        var os = new CountingStore();
        var file = new CountingStore();
        var services = await HeadServices.CreateAsync(Options(tree, new CountingHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound))) with
        {
            AccountOsStore = os,
            AccountFileStore = file,
        }, Token).ConfigureAwait(true);
        await using var scope = services.ConfigureAwait(true);

        os.Calls.ShouldBe(0);
        file.Calls.ShouldBe(0);
        var settings = new Autonogrammer.Native.Settings.FileSettingsStore(tree.State);
        settings.ReadHasStoredSession().ShouldBeFalse();

        await services.AccountHost.RememberWithoutSystemProtectionAsync(Token).ConfigureAwait(true);
        settings.ReadAllowFileSecretStore().ShouldBeTrue();
        await services.AccountHost.Selector!.SetAsync("autonogrammer.account.session", new SecretString("stored"), Token).ConfigureAwait(true);
        file.Sets.ShouldBe(1);
        os.Sets.ShouldBe(0);
    }

    /// <summary>A sign-in sets the stored-session flag and a sign-out clears it.</summary>
    [Fact]
    public async Task SignInSetsTheStoredSessionFlagAndSignOutClearsIt()
    {
        using var tree = new TempTree();
        var session = new ScriptedSession(AccountState.SignedOut, "account-token");
        var services = await HeadServices.CreateAsync(Options(tree, new CountingHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)), session), Token).ConfigureAwait(true);
        await using var scope = services.ConfigureAwait(true);
        var settings = new Autonogrammer.Native.Settings.FileSettingsStore(tree.State);

        session.Set(AccountState.SignedOut with { Phase = AuthPhase.SignedIn, Identity = new AccountIdentity("user", "a@example.test") });
        settings.ReadHasStoredSession().ShouldBeTrue();
        session.Set(AccountState.SignedOut);
        settings.ReadHasStoredSession().ShouldBeFalse();
    }

    /// <summary>Devices opens only when GET account lists dispatch.</summary>
    [Theory]
    [InlineData("""{"id":"u","features":["cowork"]}""", false)]
    [InlineData("""{"id":"u","features":["dispatch"]}""", true)]
    [InlineData("not-json", false)]
    public async Task DevicesOpensOnlyWhenDispatchIsListed(string body, bool open)
    {
        using var tree = new TempTree();
        var handler = new CountingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
        var session = new ScriptedSession(AccountState.SignedOut with { Phase = AuthPhase.SignedIn, Identity = new AccountIdentity("user", "a@example.test") }, "account-token");
        var services = await HeadServices.CreateAsync(Options(tree, handler, session, "https://api.example.test/"), Token).ConfigureAwait(true);
        await using var scope = services.ConfigureAwait(true);

        await services.Account!.RefreshDevicesAsync(Token).ConfigureAwait(true);
        services.Account.State.DevicesAvailable.ShouldBe(open);
    }

    private static HeadServicesOptions Options(TempTree tree, HttpMessageHandler handler, IAccountSession? session = null, string? address = null) => new()
    {
        StateRoot = tree.State,
        PluginsRoot = tree.Plugins,
        WorkspaceRoot = tree.Workspace,
        HomeDirectory = Path.Combine(tree.Root, "home"),
        ConfigDirectory = Path.Combine(tree.Root, "config"),
        GetVariable = name => name == HeadServices.ApiBaseUrlVariable ? address : null,
        ApiHandler = handler,
        OmpAvailable = static () => false,
        AccountSession = session,
    };

    private sealed class CountingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(respond(request));
        }
    }

    private sealed class CountingStore : ISecretStore
    {
        public int Calls { get; private set; }

        public int Sets { get; private set; }

        public Task SetAsync(string name, SecretString secret, CancellationToken cancellationToken)
        {
            _ = name;
            _ = secret;
            _ = cancellationToken;
            Calls++;
            Sets++;
            return Task.CompletedTask;
        }

        public Task<SecretString?> GetAsync(string name, CancellationToken cancellationToken)
        {
            _ = name;
            _ = cancellationToken;
            Calls++;
            return Task.FromResult<SecretString?>(null);
        }

        public Task<bool> RemoveAsync(string name, CancellationToken cancellationToken)
        {
            _ = name;
            _ = cancellationToken;
            Calls++;
            return Task.FromResult(false);
        }

        public Task<IReadOnlyList<string>> ListNamesAsync(CancellationToken cancellationToken)
        {
            _ = cancellationToken;
            Calls++;
            return Task.FromResult<IReadOnlyList<string>>([]);
        }
    }

    private sealed class ScriptedSession(AccountState state, string token) : IAccountSession
    {
        private AccountState _state = state;

        public event EventHandler<AccountStateChangedEventArgs>? StateChanged;

        public AccountState State => _state;

        public void Set(AccountState state)
        {
            _state = state;
            StateChanged?.Invoke(this, new AccountStateChangedEventArgs(state));
        }

        public ValueTask<AuthResult<SecretString>> GetAccessTokenAsync(CancellationToken cancellationToken)
        {
            _ = cancellationToken;
            return ValueTask.FromResult(_state.Phase == AuthPhase.SignedIn
                ? AuthResult.Success(new SecretString(token))
                : AuthResult.Fail<SecretString>(AuthErrorKind.SessionEnded));
        }

        public Task<AuthResult<AuthUnit>> SignOutAsync(SignOutScope scope, CancellationToken cancellationToken)
        {
            _ = scope;
            _ = cancellationToken;
            Set(AccountState.SignedOut);
            return Task.FromResult(AuthResult.Success(AuthUnit.Value));
        }
    }
}
