using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests;

/// <summary>
/// The skip link of the shell (AL-22, spec 4.2): it is transparent while the keyboard is elsewhere, so it does not draw over the rail, and it becomes
/// opaque when it has focus. It stays in the tree and in the accessibility tree at rest, it is the first stop of the tab order, and it still moves focus
/// to the conversation when activated.
/// </summary>
public sealed class SkipLinkVisibilityTests
{
    /// <summary>At rest the link is fully transparent yet present: a visible, enabled, focusable control with its accessible name, out of the pointer's way.</summary>
    [Fact]
    public async Task AtRestTheSkipLinkIsTransparentButStillThereForAssistiveTechnology()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var window = ShellHarness.Show();
            ShellHarness.Rows(window).Focus();
            ShellHarness.Settle(window);
            var link = ShellHarness.SkipLink(window);

            link.Opacity.ShouldBe(0d);
            link.IsHitTestVisible.ShouldBeFalse("a transparent control must not catch the pointer");
            link.IsVisible.ShouldBeTrue("it stays in the visual tree, so assistive technology lists it");
            link.IsEffectivelyEnabled.ShouldBeTrue();
            link.Focusable.ShouldBeTrue();
            AutomationProperties.GetName(link).ShouldBe("Skip to conversation");
            link.GetVisualAncestors().OfType<Control>().ShouldAllBe(static ancestor => ancestor.Opacity > 0, "no ancestor hides it for assistive technology either");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Focus makes the link opaque and hit-testable; moving focus away makes it transparent again.</summary>
    [Fact]
    public async Task FocusMakesTheSkipLinkOpaqueAndLeavingMakesItTransparentAgain()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var window = ShellHarness.Show();
            var link = ShellHarness.SkipLink(window);

            link.Focus(NavigationMethod.Tab).ShouldBeTrue();
            ShellHarness.Settle(window);

            link.IsFocused.ShouldBeTrue();
            link.Opacity.ShouldBe(1d);
            link.IsHitTestVisible.ShouldBeTrue();

            ShellHarness.Rows(window).Focus();
            ShellHarness.Settle(window);

            link.IsFocused.ShouldBeFalse();
            link.Opacity.ShouldBe(0d);
            link.IsHitTestVisible.ShouldBeFalse();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The skip link is the first focusable element of the window, and Shift+Tab from the first control of the shell cannot go earlier; Tab from the link reaches the rail.</summary>
    [Fact]
    public async Task TheSkipLinkIsTheFirstStopOfTheTabOrder()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var window = ShellHarness.Show();
            var link = ShellHarness.SkipLink(window);

            window.FocusManager!.FindFirstFocusableElement().ShouldBeSameAs(link, "nothing precedes the skip link in the tab order");

            link.Focus(NavigationMethod.Tab);
            ShellHarness.Press(window, Key.Tab, PhysicalKey.Tab);

            window.FocusManager.GetFocusedElement().ShouldNotBeSameAs(link, "Tab leaves the link for the next control");
            link.Opacity.ShouldBe(0d);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Enter on the focused link activates it, and focus moves to the transcript, as it does for a click.</summary>
    [Fact]
    public async Task EnterOnTheFocusedSkipLinkMovesFocusToTheTranscript()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var window = ShellHarness.Show();
            ShellHarness.SkipLink(window).Focus(NavigationMethod.Tab);
            window.FocusManager!.GetFocusedElement().ShouldBeSameAs(ShellHarness.SkipLink(window));

            ShellHarness.Press(window, Key.Enter, PhysicalKey.Enter);
            ShellHarness.Settle(window);

            ShellHarness.Scroll(window).IsFocused.ShouldBeTrue("activating the skip link lands on the conversation");
            ShellHarness.SkipLink(window).Opacity.ShouldBe(0d, "the link hides again once focus has moved on");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }
}
