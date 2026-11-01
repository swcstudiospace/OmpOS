using Autonogrammer.Contracts.Accounts;
using Autonogrammer.Presentation.Accounts;
using Autonogrammer.UI.Components.Account;
using Bunit;
using Shouldly;
using Xunit;

namespace Autonogrammer.UI.Tests.Account;

/// <summary>The web hand-off and the five panel states, against a fake channel and a fake host.</summary>
public sealed class WebAccountStateTests : UiTestContext
{
    /// <summary>The asking tab acknowledges a posted code and only then exchanges it. The returning tab scrubbed first.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task AskingTabExchangesAfterAcknowledgement()
    {
        var returning = new FakeBroadcastChannel();
        var asking = new FakeBroadcastChannel();
        returning.Pair(asking);
        string? exchanged = null;
        using var listen = AuthCallbackHandOff.ListenForExchange(asking, code =>
        {
            exchanged = code;
            return Task.CompletedTask;
        });

        var result = await AuthCallbackHandOff.HandOffAsync(
            "sessions/abc?code=link-code&x=1",
            returning,
            TimeProvider.System,
            Xunit.TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Status.ShouldBe(AuthCallbackStatus.Acknowledged);
        result.ScrubbedBeforePost.ShouldBeTrue();
        result.Sentence.ShouldBe(AuthCallbackHandOff.SignedInOtherTabSentence);
        result.RememberedPath.ShouldNotContain("code=");
        result.RememberedPath.ShouldBe("sessions/abc?x=1");
        exchanged.ShouldBe("link-code");
        asking.Posted.ShouldContain(envelope => envelope.Kind == "ack");
        returning.Posted[0].Kind.ShouldBe("code");
    }

    /// <summary>No acknowledgement within three seconds is the wrong-tab sentence.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task MissingAcknowledgementShowsWrongTab()
    {
        var returning = new FakeBroadcastChannel();
        var result = await AuthCallbackHandOff.HandOffAsync("?code=orphan", returning, TimeProvider.System, CancellationToken.None).ConfigureAwait(true);

        result.Status.ShouldBe(AuthCallbackStatus.Unacknowledged);
        result.Sentence.ShouldBe(AccountCopy.For(AuthErrorKind.LinkOpenedInWrongTab));
        result.ScrubbedBeforePost.ShouldBeTrue();
        result.RememberedPath.ShouldNotContain("code");
    }

    /// <summary>A channel that is not same-origin is refused and posts nothing.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task ACrossOriginChannelIsRefused()
    {
        var channel = new FakeBroadcastChannel(sameOrigin: false);

        var result = await AuthCallbackHandOff.HandOffAsync("?code=secret", channel, TimeProvider.System, CancellationToken.None).ConfigureAwait(true);

        result.Status.ShouldBe(AuthCallbackStatus.Error);
        result.Sentence.ShouldBe(AccountCopy.For(AuthErrorKind.LinkOpenedInWrongTab));
        channel.Posted.ShouldBeEmpty();
    }

    /// <summary>A typed code of 6 to 10 digits is accepted after spaces and hyphens are removed. Other lengths are not.</summary>
    /// <param name="typed">The text.</param>
    /// <param name="accepted">Whether it is a code.</param>
    [Theory]
    [InlineData("123456", true)]
    [InlineData("1234 5678", true)]
    [InlineData("12-3456-7890", true)]
    [InlineData("12345", false)]
    [InlineData("12345678901", false)]
    public void CodesOfSixToTenDigitsAreAccepted(string typed, bool accepted)
    {
        var normalized = AuthCallbackHandOff.NormalizeEmailCode(typed);
        if (accepted)
        {
            normalized.ShouldNotBeNull();
            normalized!.Length.ShouldBeInRange(6, 10);
        }
        else
        {
            normalized.ShouldBeNull();
        }
    }

    /// <summary>The panel walks the five phases with the shared sentences, and an error is an alert.</summary>
    /// <param name="phase">The phase.</param>
    [Theory]
    [InlineData(AuthPhase.SignedOut)]
    [InlineData(AuthPhase.AwaitingLink)]
    [InlineData(AuthPhase.SigningIn)]
    [InlineData(AuthPhase.SignedIn)]
    [InlineData(AuthPhase.Error)]
    public void ThePanelShowsEachPhaseWithSharedCopy(AuthPhase phase)
    {
        var host = new ScriptedHost(AccountHostState.SignedOut with
        {
            Phase = phase,
            Error = phase == AuthPhase.Error ? AuthErrorKind.NetworkUnavailable : null,
            ExpiresAt = phase == AuthPhase.AwaitingLink ? DateTimeOffset.UtcNow.AddMinutes(5) : null,
            ResendAvailableAt = phase == AuthPhase.AwaitingLink ? DateTimeOffset.UtcNow.AddSeconds(60) : null,
        });

        var cut = Render<SignInPanel>(parameters => parameters.Add(p => p.Host, host));

        cut.Markup.ShouldContain(AccountCopy.For(phase));
        if (phase == AuthPhase.Error)
        {
            cut.Find("[role='alert']").TextContent.ShouldBe(AccountCopy.For(AuthErrorKind.NetworkUnavailable));
        }
    }

    private sealed class ScriptedHost : IAccountHost
    {
        internal ScriptedHost(AccountHostState state) => State = state;

        public event EventHandler? Changed { add { } remove { } }

        public AccountHostState State { get; }

        public Task SendCodeAsync(string email, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task ResendCodeAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SubmitCodeAsync(string code, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task CancelAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SignOutAsync(bool everywhere, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RememberWithoutSystemProtectionAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public void ChangeEmail()
        {
        }

        public Task RefreshDevicesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
