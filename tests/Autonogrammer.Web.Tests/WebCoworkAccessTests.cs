using Autonogrammer.UI.Components.Auth;
using Autonogrammer.Web.Services;
using Shouldly;
using Xunit;

namespace Autonogrammer.Web.Tests;

/// <summary>Cowork is available only after the auth answer, and never in the Demo.</summary>
public sealed class WebCoworkAccessTests
{
    /// <summary>The OIDC deployment that serves a client block: the only deployment this head signs in to.</summary>
    private static AuthConfig SignInHere => new(AuthConfig.OidcMode, "https://issuer.example", "autonogrammer", new Uri("https://project.example"), "sb_publishable_test");

    /// <summary>The Demo is never available and offers no sign-in link.</summary>
    [Fact]
    public async Task DemoIsNeverAvailable()
    {
        using var access = new WebCoworkAccess(new WebWorkbenchOptions(IsDemo: true, ApiHost: "demo"), new FakeSession(), new FakeConfig(AuthConfig.DevelopmentMode));
        await WaitAsync(access).ConfigureAwait(true);

        access.IsAvailable.ShouldBeFalse();
        access.OffersSignIn.ShouldBeFalse();
    }

    /// <summary>A held session makes Cowork available after the config answers.</summary>
    [Fact]
    public async Task AHeldSessionIsAvailable()
    {
        var session = new FakeSession { HasToken = true };
        var config = new FakeConfig(SignInHere, hold: true);
        using var access = new WebCoworkAccess(new WebWorkbenchOptions(false, "api"), session, config);
        access.IsAvailable.ShouldBeFalse();
        var raised = 0;
        access.Changed += (_, _) => raised++;
        config.Release();
        await WaitAsync(access).ConfigureAwait(true);

        access.IsAvailable.ShouldBeTrue();
        raised.ShouldBe(1);
    }

    /// <summary>Development mode is available without a session.</summary>
    [Fact]
    public async Task DevelopmentIsAvailable()
    {
        using var access = new WebCoworkAccess(new WebWorkbenchOptions(false, "api"), new FakeSession(), new FakeConfig(AuthConfig.DevelopmentMode));
        await WaitAsync(access).ConfigureAwait(true);

        access.IsAvailable.ShouldBeTrue();
    }

    /// <summary>A signed-out deployment that serves a client block is not available, and it offers sign-in.</summary>
    [Fact]
    public async Task ASignedOutOidcDeploymentOffersSignIn()
    {
        using var access = new WebCoworkAccess(new WebWorkbenchOptions(false, "api"), new FakeSession(), new FakeConfig(SignInHere));
        await WaitAsync(access).ConfigureAwait(true);

        access.IsAvailable.ShouldBeFalse();
        access.OffersSignIn.ShouldBeTrue();
    }

    /// <summary>A deployment this head cannot sign in to is not available, and it offers no sign-in.</summary>
    /// <param name="mode">The auth mode, without a client block.</param>
    [Theory]
    [InlineData(AuthConfig.OwnerTokenMode)]
    [InlineData(AuthConfig.OidcMode)]
    public async Task ASignedOutDeploymentWithoutAClientBlockOffersNoSignIn(string mode)
    {
        using var access = new WebCoworkAccess(new WebWorkbenchOptions(false, "api"), new FakeSession(), new FakeConfig(mode));
        await WaitAsync(access).ConfigureAwait(true);

        access.IsAvailable.ShouldBeFalse();
        access.OffersSignIn.ShouldBeFalse();
    }

    /// <summary>A session change raises Changed.</summary>
    [Fact]
    public async Task ASessionChangeRaisesChanged()
    {
        var session = new FakeSession();
        using var access = new WebCoworkAccess(new WebWorkbenchOptions(false, "api"), session, new FakeConfig(SignInHere));
        await WaitAsync(access).ConfigureAwait(true);
        var raised = 0;
        access.Changed += (_, _) => raised++;

        session.Raise();

        raised.ShouldBe(1);
        session.HasToken = true;
        access.IsAvailable.ShouldBeTrue();
    }

    private static async Task WaitAsync(WebCoworkAccess access)
    {
        var until = DateTimeOffset.UtcNow.AddSeconds(5);
        while (!access.IsResolved && DateTimeOffset.UtcNow < until)
        {
            await Task.Delay(10).ConfigureAwait(true);
        }
    }

    private sealed class FakeSession : IAuthSession
    {
        public event EventHandler? Changed;

        public bool HasToken { get; set; }

        public bool SessionEnded => false;

        public string? ReturnPath { get; set; }

        public void Clear() => HasToken = false;

        public void Raise() => Changed?.Invoke(this, EventArgs.Empty);
    }

    private sealed class FakeConfig : IAuthConfigSource
    {
        private readonly TaskCompletionSource<AuthConfig?> _gate = new();

        public FakeConfig(string mode, bool hold = false)
            : this(new AuthConfig(mode, null, null), hold)
        {
        }

        public FakeConfig(AuthConfig answer, bool hold = false)
        {
            Answer = answer;
            if (!hold)
            {
                _gate.TrySetResult(Answer);
            }
        }

        public AuthConfig? Answer { get; }

        public async Task<AuthConfig?> LoadAsync(CancellationToken cancellationToken)
        {
            while (!_gate.Task.IsCompleted)
            {
                await Task.Delay(1, cancellationToken).ConfigureAwait(true);
            }

            return Answer;
        }

        public void Release() => _gate.TrySetResult(Answer);
    }
}
