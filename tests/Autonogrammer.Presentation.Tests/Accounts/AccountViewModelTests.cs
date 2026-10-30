using Autonogrammer.Contracts.Accounts;
using Autonogrammer.Presentation.Accounts;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Accounts;

/// <summary>The five account states, the offline notice, the devices row and the consent action.</summary>
public sealed class AccountViewModelTests
{
    /// <summary>Each of the five phases is the only visible state.</summary>
    [Fact]
    public async Task EachPhaseIsExclusive()
    {
        var clock = new ManualTimeProvider();
        var host = new FakeAccountHost();
        var model = new AccountViewModel(host, clock);
        model.VisibleStateCount.ShouldBe(1);
        model.ShowSignedOut.ShouldBeTrue();

        await model.SendCodeAsync(CancellationToken.None);
        model.ShowAwaitingLink.ShouldBeTrue();
        model.VisibleStateCount.ShouldBe(1);

        host.Set(host.State with { Phase = AuthPhase.SigningIn });
        model.ShowSigningIn.ShouldBeTrue();
        model.CanCancel.ShouldBeTrue();
        model.VisibleStateCount.ShouldBe(1);
        await model.CancelFromEscapeAsync(CancellationToken.None);
        host.CancelCalls.ShouldBe(1);
        model.ShowSignedOut.ShouldBeTrue();

        host.Set(AccountHostState.SignedOut with { Phase = AuthPhase.SignedIn, Email = "a@example.test" });
        model.ShowSignedIn.ShouldBeTrue();
        model.VisibleStateCount.ShouldBe(1);

        host.Set(AccountHostState.SignedOut with { Phase = AuthPhase.Error, Error = AuthErrorKind.EmailInvalid });
        model.ShowError.ShouldBeTrue();
        model.ErrorSentence.ShouldBe(AccountCopy.For(AuthErrorKind.EmailInvalid));
        model.VisibleStateCount.ShouldBe(1);
        (model.ErrorSentence ?? string.Empty).Contains("exception", StringComparison.OrdinalIgnoreCase).ShouldBeFalse();
    }

    /// <summary>Offline is a notice on the signed-in state, not a sign-out.</summary>
    [Fact]
    public void OfflineStaysSignedIn()
    {
        var host = new FakeAccountHost();
        var model = new AccountViewModel(host, new ManualTimeProvider());
        host.Set(AccountHostState.SignedOut with { Phase = AuthPhase.SignedIn, Email = "a@example.test", Offline = true });

        model.ShowSignedIn.ShouldBeTrue();
        model.ShowSignedOut.ShouldBeFalse();
        model.ShowOffline.ShouldBeTrue();
        model.OfflineSentence.ShouldBe(AccountCopy.OfflineNotice);
        model.VisibleStateCount.ShouldBe(1);
    }

    /// <summary>The presentation assembly does not reference Accounts.</summary>
    [Fact]
    public void PresentationDoesNotReferenceAccounts()
    {
        var names = typeof(AccountViewModel).Assembly.GetReferencedAssemblies().Select(name => name.Name).ToArray();
        names.ShouldNotContain("Autonogrammer.Accounts", StringComparer.Ordinal);
        names.ShouldNotContain("Autonogrammer.Accounts.Desktop", StringComparer.Ordinal);
    }

    /// <summary>Devices is drawn only when the host says the features list contains dispatch.</summary>
    [Fact]
    public void DevicesFollowsTheHostFlag()
    {
        var host = new FakeAccountHost();
        var model = new AccountViewModel(host, new ManualTimeProvider());
        host.Set(AccountHostState.SignedOut with { Phase = AuthPhase.SignedIn });
        model.ShowDevices.ShouldBeFalse();

        host.Set(host.State with { DevicesAvailable = true });
        model.ShowDevices.ShouldBeTrue();

        host.Set(host.State with { DevicesAvailable = false });
        model.ShowDevices.ShouldBeFalse();
    }

    /// <summary>The consent action is offered only with the session-not-remembered notice and calls the host once.</summary>
    [Fact]
    public async Task ConsentIsOfferedOnlyWithTheNoticeAndCallsOnce()
    {
        var host = new FakeAccountHost();
        var model = new AccountViewModel(host, new ManualTimeProvider());
        host.Set(AccountHostState.SignedOut with { Phase = AuthPhase.SignedIn, SessionNotRemembered = false });
        model.ShowConsent.ShouldBeFalse();
        await model.RememberWithoutSystemProtectionAsync(CancellationToken.None);
        host.ConsentCalls.ShouldBe(0);

        host.Set(host.State with { SessionNotRemembered = true });
        model.ShowConsent.ShouldBeTrue();
        model.ShowSessionNotRemembered.ShouldBeTrue();
        model.SessionNotRememberedSentence.ShouldBe(AccountCopy.SessionNotRememberedNotice);
        await model.RememberWithoutSystemProtectionAsync(CancellationToken.None);
        host.ConsentCalls.ShouldBe(1);
    }

    /// <summary>Countdowns change every second and announce only at 30 seconds, 10 seconds and availability.</summary>
    [Fact]
    public void AnnouncementsAreThrottled()
    {
        var clock = new ManualTimeProvider();
        var host = new FakeAccountHost();
        var model = new AccountViewModel(host, clock);
        var start = clock.GetUtcNow();
        host.Set(AccountHostState.SignedOut with
        {
            Phase = AuthPhase.AwaitingLink,
            Email = "a@example.test",
            ExpiresAt = start.AddSeconds(31),
            ResendAvailableAt = start.AddSeconds(25),
        });

        var announced = new List<string>();
        string? previous = null;
        model.PropertyChanged += (_, args) =>
        {
            if (string.Equals(args.PropertyName, nameof(AccountViewModel.LiveAnnouncement), StringComparison.Ordinal) && model.LiveAnnouncement is { } text && !string.Equals(text, previous, StringComparison.Ordinal))
            {
                previous = text;
                announced.Add(text);
            }
        };
        model.RefreshClock();
        model.ExpiryText.Contains("31", StringComparison.Ordinal).ShouldBeTrue();
        clock.Advance(TimeSpan.FromSeconds(1));
        model.RefreshClock();
        model.ExpiryText.Contains("30", StringComparison.Ordinal).ShouldBeTrue();
        announced.ShouldContain(AccountCopy.ExpiryIn30Seconds, StringComparer.Ordinal);
        clock.Advance(TimeSpan.FromSeconds(20));
        model.RefreshClock();
        model.ExpiryText.Contains("10", StringComparison.Ordinal).ShouldBeTrue();
        announced.ShouldContain(AccountCopy.ExpiryIn10Seconds, StringComparer.Ordinal);
        announced.ShouldNotContain(AccountCopy.ResendAvailableAnnouncement, StringComparer.Ordinal);
        clock.Advance(TimeSpan.FromSeconds(4));
        model.RefreshClock();
        announced.ShouldContain(AccountCopy.ResendAvailableAnnouncement, StringComparer.Ordinal);
        announced.Count(text => string.Equals(text, AccountCopy.ExpiryIn30Seconds, StringComparison.Ordinal)).ShouldBe(1);
    }

    /// <summary>Every phase and every failure kind has one fixed sentence, and two calls return the same text.</summary>
    [Fact]
    public void EveryEnumValueHasOneFixedSentence()
    {
        foreach (var phase in Enum.GetValues<AuthPhase>())
        {
            var sentence = AccountCopy.For(phase);
            sentence.ShouldBe(AccountCopy.For(phase));
            sentence.Contains('{', StringComparison.Ordinal).ShouldBeFalse();
            sentence.Length.ShouldBeGreaterThan(8);
        }

        foreach (var kind in Enum.GetValues<AuthErrorKind>())
        {
            var sentence = AccountCopy.For(kind);
            sentence.ShouldBe(AccountCopy.For(kind));
            sentence.Contains("server", StringComparison.OrdinalIgnoreCase).ShouldBeFalse();
        }

        string.Equals(AccountCopy.For(AuthPhase.SignedOut), AccountCopy.For(AuthPhase.SignedIn), StringComparison.Ordinal).ShouldBeFalse();
        string.Equals(AccountCopy.For(AuthErrorKind.EmailInvalid), AccountCopy.For(AuthErrorKind.Unexpected), StringComparison.Ordinal).ShouldBeFalse();
    }
}
