using Autonogrammer.Contracts.Accounts;
using Autonogrammer.Native.Tests.AgentLoop;
using Autonogrammer.Native.Tests.Settings;
using Autonogrammer.Native.Views.Account;
using Autonogrammer.Native.Views.AgentPanel;
using Autonogrammer.Presentation.Accounts;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Styling;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Account;

/// <summary>Accessible names, focus, throttled announcements and a static spinner under reduced motion.</summary>
public sealed class AccountAccessibilityTests
{
    /// <summary>Every visible control has an automation name in Light and Dark, for each of the five states.</summary>
    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task EveryControlIsNamedInBothThemes(string theme)
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var variant = string.Equals(theme, "Dark", StringComparison.Ordinal) ? ThemeVariant.Dark : ThemeVariant.Light;
            foreach (var phase in Enum.GetValues<AuthPhase>())
            {
                var host = new FakeAccountHost();
                var model = new AccountViewModel(host, new ManualTimeProvider(DateTimeOffset.UnixEpoch));
                host.Set(AccountHostState.SignedOut with
                {
                    Phase = phase,
                    Email = "a@example.test",
                    Error = phase == AuthPhase.Error ? AuthErrorKind.NetworkUnavailable : null,
                    ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(2),
                    ResendAvailableAt = DateTimeOffset.UtcNow.AddSeconds(5),
                    SessionNotRemembered = phase == AuthPhase.SignedIn,
                    DevicesAvailable = phase == AuthPhase.SignedIn,
                });
                var view = new AccountView();
                var window = new Window { Content = view, Width = 480, Height = 640, RequestedThemeVariant = variant };
                view.Attach(model);
                window.Show();
                SettingsTree.SettleSafe(window);
                SettingsTree.Unnamed(window).ShouldBeEmpty($"unnamed controls in {phase} / {theme}");
                window.Close();
            }
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Focus moves to the code field while awaiting a code and to the error message in the error state.</summary>
    [Fact]
    public async Task FocusMovesToTheCodeFieldAndTheError()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var host = new FakeAccountHost();
            var model = new AccountViewModel(host, new ManualTimeProvider(DateTimeOffset.UnixEpoch));
            var view = new AccountView();
            var window = new Window { Content = view, Width = 480, Height = 640 };
            view.Attach(model);
            window.Show();
            SettingsTree.SettleSafe(window);

            host.Set(AccountHostState.SignedOut with
            {
                Phase = AuthPhase.AwaitingLink,
                Email = "a@example.test",
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(1),
                ResendAvailableAt = DateTimeOffset.UtcNow,
            });
            SettingsTree.SettleSafe(window);
            view.CodeField.IsFocused.ShouldBeTrue();
            view.CodeField.MaxLength.ShouldNotBe(6);

            host.Set(AccountHostState.SignedOut with { Phase = AuthPhase.Error, Error = AuthErrorKind.EmailInvalid });
            SettingsTree.SettleSafe(window);
            view.Error.IsFocused.ShouldBeTrue();
            view.Error.Text.ShouldBe(AccountCopy.For(AuthErrorKind.EmailInvalid));
            window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The live region is renamed only at the throttled moments, and the spinner is hidden under reduced motion.</summary>
    [Fact]
    public async Task AnnouncementsAreThrottledAndTheSpinnerIsStaticWhenMotionIsReduced()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var clock = new ManualTimeProvider(new DateTimeOffset(2026, 10, 30, 12, 0, 0, TimeSpan.Zero));
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
            var view = new AccountView();
            view.Classes.Add(MotionProbe.ReducedMotionClass);
            var window = new Window { Content = view, Width = 480, Height = 640 };
            view.Attach(model);
            window.Show();
            SettingsTree.SettleSafe(window);

            var names = new List<string>();
            void Capture()
            {
                var name = AutomationProperties.GetName(view.Announcement) ?? string.Empty;
                if (names.Count == 0 || !string.Equals(names[^1], name, StringComparison.Ordinal))
                {
                    names.Add(name);
                }
            }

            Capture();
            clock.Advance(TimeSpan.FromSeconds(1));
            model.RefreshClock();
            Capture();
            names.ShouldContain(AccountCopy.ExpiryIn30Seconds);
            clock.Advance(TimeSpan.FromSeconds(1));
            model.RefreshClock();
            Capture();
            names.Count(name => name == AccountCopy.ExpiryIn30Seconds).ShouldBe(1);

            host.Set(host.State with { Phase = AuthPhase.SigningIn });
            SettingsTree.SettleSafe(window);
            view.Spinner.IsVisible.ShouldBeFalse();
            view.SigningStatic.IsVisible.ShouldBeTrue();
            window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }
}
