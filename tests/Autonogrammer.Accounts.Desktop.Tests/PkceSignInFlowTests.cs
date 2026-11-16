using System.Net;
using System.Net.Sockets;
using Autonogrammer.Accounts.Desktop.Tests.Support;
using Autonogrammer.Contracts.Accounts;
using Autonogrammer.Harness.Secrets;
using Shouldly;
using Xunit;

namespace Autonogrammer.Accounts.Desktop.Tests;

/// <summary>The PKCE link flow end to end over real sockets: the loopback listener, the coordinator and the raw-socket fake service (AG-67-2, D-09).</summary>
public sealed class PkceSignInFlowTests
{
    private const string Email = "person@example.com";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ALinkCallbackSignsInEndToEndAndTheServiceCheckedTheS256Challenge()
    {
        var rig = new Rig();
        await using (rig.ConfigureAwait(true))
        {
            AuthResult<SignInChallenge> sent = await rig.Flow.SendCodeAsync(Email, Token).ConfigureAwait(true);

            sent.IsSuccess.ShouldBeTrue();
            sent.Value.LinkAvailable.ShouldBeTrue();
            Uri redirect = rig.Flow.RedirectUri!;
            redirect.Host.ShouldBe("127.0.0.1");
            rig.Host.LinkRequests.ShouldHaveSingleItem().RedirectTo.ShouldBe(redirect.AbsoluteUri);
            Uri link = rig.Host.LinkFor(Email);
            link.GetLeftPart(UriPartial.Path).ShouldBe(redirect.AbsoluteUri);

            using var browser = new HttpClient();
            using HttpResponseMessage page = await browser.GetAsync(link, Token).ConfigureAwait(true);

            page.StatusCode.ShouldBe(HttpStatusCode.OK);
            AuthResult<AuthUnit>? done = await rig.Flow.WaitForLinkAsync(Token).WaitAsync(TimeSpan.FromSeconds(10), Token).ConfigureAwait(true);
            done.ShouldNotBeNull();
            done.IsSuccess.ShouldBeTrue();
            rig.Host.Count("token:pkce").ShouldBe(1);
            rig.Manager.State.Phase.ShouldBe(AuthPhase.SignedIn);
            rig.Manager.State.Identity!.Email.ShouldBe(Email);
            (await rig.Store.GetAsync(AccountSessionManager.SecretName, Token).ConfigureAwait(true)).ShouldNotBeNull();
        }
    }

    [Fact]
    public async Task AResendReusesTheVerifierAndNonceWhileANewAttemptGeneratesFreshOnes()
    {
        var rig = new Rig();
        await using (rig.ConfigureAwait(true))
        {
            _ = await rig.Flow.SendCodeAsync(Email, Token).ConfigureAwait(true);
            Uri firstRedirect = rig.Flow.RedirectUri!;
            rig.Time.Advance(TimeSpan.FromMinutes(2));

            AuthResult<SignInChallenge> resent = await rig.Flow.ResendCodeAsync(Token).ConfigureAwait(true);

            resent.IsSuccess.ShouldBeTrue();
            rig.Flow.RedirectUri.ShouldBe(firstRedirect);
            rig.Host.LinkRequests.Count.ShouldBe(2);
            rig.Host.LinkRequests[1].ShouldBe(rig.Host.LinkRequests[0]);

            rig.Time.Advance(TimeSpan.FromMinutes(2));
            AuthResult<SignInChallenge> again = await rig.Flow.SendCodeAsync(Email, Token).ConfigureAwait(true);

            again.IsSuccess.ShouldBeTrue();
            rig.Flow.RedirectUri.ShouldBe(firstRedirect);
            rig.Host.LinkRequests.Count.ShouldBe(3);
            rig.Host.LinkRequests[2].ShouldBe(rig.Host.LinkRequests[0]);

            _ = await rig.Flow.SendCodeAsync("second@example.com", Token).ConfigureAwait(true);

            rig.Flow.RedirectUri.ShouldNotBe(firstRedirect);
            rig.Host.LinkRequests.Count.ShouldBe(4);
            string.Equals(rig.Host.LinkRequests[3].Challenge, rig.Host.LinkRequests[0].Challenge, StringComparison.Ordinal).ShouldBeFalse();
            string.Equals(rig.Host.LinkRequests[3].RedirectTo, rig.Host.LinkRequests[0].RedirectTo, StringComparison.Ordinal).ShouldBeFalse();
            (await ConnectsAsync(firstRedirect.Port).ConfigureAwait(true)).ShouldBeFalse();
        }
    }

    [Fact]
    public async Task AnErrorCallbackFallsBackToTheCodeWhichStillSignsIn()
    {
        var rig = new Rig();
        await using (rig.ConfigureAwait(true))
        {
            _ = await rig.Flow.SendCodeAsync(Email, Token).ConfigureAwait(true);
            Uri redirect = rig.Flow.RedirectUri!;

            using var browser = new HttpClient();
            using HttpResponseMessage page = await browser.GetAsync(new Uri(redirect.AbsoluteUri + "?error=access_denied&error_code=otp_expired"), Token).ConfigureAwait(true);

            AuthResult<AuthUnit>? link = await rig.Flow.WaitForLinkAsync(Token).WaitAsync(TimeSpan.FromSeconds(10), Token).ConfigureAwait(true);
            link.ShouldNotBeNull();
            link.IsSuccess.ShouldBeFalse();
            link.Failure.Kind.ShouldBe(AuthErrorKind.LinkInvalidOrExpired);
            rig.Manager.State.Phase.ShouldNotBe(AuthPhase.SignedIn);

            AuthResult<AuthUnit> typed = await rig.Flow.VerifyCodeAsync(rig.Host.LastCode(Email), Token).ConfigureAwait(true);

            typed.IsSuccess.ShouldBeTrue();
            rig.Manager.State.Phase.ShouldBe(AuthPhase.SignedIn);
            rig.Host.Count("token:pkce").ShouldBe(0);
        }
    }

    [Fact]
    public async Task WhenNoCallbackArrivesInTenMinutesTheFlowFallsBackToTheCode()
    {
        var rig = new Rig();
        await using (rig.ConfigureAwait(true))
        {
            _ = await rig.Flow.SendCodeAsync(Email, Token).ConfigureAwait(true);

            rig.Time.Advance(LoopbackListener.Lifetime);

            AuthResult<AuthUnit>? link = await rig.Flow.WaitForLinkAsync(Token).WaitAsync(TimeSpan.FromSeconds(10), Token).ConfigureAwait(true);
            link.ShouldNotBeNull();
            link.Failure.Kind.ShouldBe(AuthErrorKind.LinkInvalidOrExpired);
            PkceSignInFlow.LinkOnAnotherDeviceSentence.ShouldNotBeNullOrWhiteSpace();
            (await rig.Flow.VerifyCodeAsync(rig.Host.LastCode(Email), Token).ConfigureAwait(true)).IsSuccess.ShouldBeTrue();
        }
    }

    [Fact]
    public async Task AProjectWithoutTheLinkMakesNoListenerAndTheCodeIsTheOnlyWay()
    {
        var rig = new Rig(magicLink: false);
        await using (rig.ConfigureAwait(true))
        {
            AuthResult<SignInChallenge> sent = await rig.Flow.SendCodeAsync(Email, Token).ConfigureAwait(true);

            sent.Value.LinkAvailable.ShouldBeFalse();
            rig.Flow.RedirectUri.ShouldBeNull();
            rig.Host.LinkRequests.ShouldBeEmpty();
            (await rig.Flow.WaitForLinkAsync(Token).ConfigureAwait(true)).ShouldBeNull();
        }
    }

    [Fact]
    public async Task VerifyingTheCodeClosesTheListener()
    {
        var rig = new Rig();
        await using (rig.ConfigureAwait(true))
        {
            _ = await rig.Flow.SendCodeAsync(Email, Token).ConfigureAwait(true);
            int port = rig.Flow.RedirectUri!.Port;
            (await ConnectsAsync(port).ConfigureAwait(true)).ShouldBeTrue();

            (await rig.Flow.VerifyCodeAsync(rig.Host.LastCode(Email), Token).ConfigureAwait(true)).IsSuccess.ShouldBeTrue();

            (await ConnectsAsync(port).ConfigureAwait(true)).ShouldBeFalse();
            rig.Flow.RedirectUri.ShouldBeNull();
        }
    }

    private static async Task<bool> ConnectsAsync(int port)
    {
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, port, Token).ConfigureAwait(false);
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    private sealed class Rig : IAsyncDisposable
    {
        private readonly HttpClient _http;
        private readonly AccountSessionManager _manager;
        private readonly SignInCoordinator _coordinator;

        public Rig(bool magicLink = true)
        {
            Time = new ManualTimeProvider();
            Host = new FakeGoTrueHost(Time, magicLink);
            _http = Host.CreateClient();
            Store = new InMemorySessionSecretStore();
            var client = new GoTrueClient(_http, Host.Options, Time);
            Func<CancellationToken, ValueTask<GoTrueClient?>> provider = AccountSessionManager.FixedClient(client);
            _manager = new AccountSessionManager(Store, provider, Time);
            _coordinator = new SignInCoordinator(_manager, provider, Time);
            Flow = new PkceSignInFlow(_coordinator, Time);
        }

        public ManualTimeProvider Time { get; }

        public FakeGoTrueHost Host { get; }

        public InMemorySessionSecretStore Store { get; }

        public AccountSessionManager Manager => _manager;

        public PkceSignInFlow Flow { get; }

        public async ValueTask DisposeAsync()
        {
            await Flow.DisposeAsync().ConfigureAwait(false);
            _coordinator.Dispose();
            _manager.Dispose();
            _http.Dispose();
            await Host.DisposeAsync().ConfigureAwait(false);
        }
    }
}
