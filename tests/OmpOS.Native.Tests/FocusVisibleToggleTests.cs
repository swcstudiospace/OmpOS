using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests;

/// <summary>Keyboard focus on the toolbar's toggles draws the same AccentPurple focus border as the other shared controls.</summary>
/// <remarks>
/// Focus arrives by Tab presses, so the <c>:focus-visible</c> style applies. The toggle is the workspace tool row's
/// <c>ConventionsToggle</c> in the Tools view of the rail in <c>MainView.axaml</c>, reached from the skip link in XAML order.
/// </remarks>
public sealed class FocusVisibleToggleTests
{
    /// <summary>The toolbar toggle that keyboard focus is moved to in the test.</summary>
    private const string ToggleName = "ConventionsToggle";

    /// <summary>The most Tab presses the test allows while it looks for the toggle, so a broken tab order fails instead of looping.</summary>
    private const int TabLimit = 40;

    /// <summary>Focusing a toolbar ToggleButton by keyboard draws the AccentPurple token as its border at the FocusBorder thickness.</summary>
    [Fact]
    public async Task FocusingAToolbarToggleDrawsTheAccentFocusBorder()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var window = ShellHarness.Show();
            ShellHarness.ShowToolsView(window);
            var toggle = ShellHarness.MainViewOf(window).FindControl<ToggleButton>(ToggleName)!;

            ShellHarness.SkipLink(window).Focus();
            for (var presses = 0; !toggle.IsFocused && presses < TabLimit; presses++)
            {
                ShellHarness.Press(window, Key.Tab, PhysicalKey.Tab);
            }

            toggle.IsFocused.ShouldBeTrue($"Tab from the skip link must reach {ToggleName} within {TabLimit} presses");
            AssertAccentFocusBorder(toggle);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    private static void AssertAccentFocusBorder(ToggleButton toggle)
    {
        var application = Application.Current!;
        application.TryGetResource("OmpOS.Color.AccentPurple", application.ActualThemeVariant, out var accentBrush).ShouldBeTrue("the accent token must resolve for the active theme variant");
        var accent = ((ISolidColorBrush)accentBrush!).Color;

        application.TryGetResource("OmpOS.Size.FocusBorder", application.ActualThemeVariant, out var focusBorder).ShouldBeTrue("the focus border token must resolve for the active theme variant");

        var border = toggle.BorderBrush as ISolidColorBrush;
        border.ShouldNotBeNull($"{ToggleName} must have a solid focus border");
        border.Color.ShouldBe(accent, $"{ToggleName} focus border must be the AccentPurple token");
        toggle.BorderThickness.ShouldBe((Thickness)focusBorder!, $"{ToggleName} focus border must be OmpOS.Size.FocusBorder");
    }
}
