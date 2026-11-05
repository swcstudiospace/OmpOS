using Autonogrammer.Native.Tests.AgentPanel.Shell;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Theme;

/// <summary>
/// The colour three shared controls draw, in both theme variants: a TextBox placeholder (OQ-48), a keyboard-focused
/// ToggleButton (audit row 23), and the composer's stop square against its button (audit row 24).
/// </summary>
/// <remarks>
/// The checks read what each control draws, not the XAML. The placeholder check reads the colour the Fluent
/// <c>TextControlPlaceholderForeground</c> key draws. That key is aliased to <c>Autonogrammer.Color.TextSecondary</c>
/// in <c>Themes/AutonogrammerTokens.axaml</c>, in both the light and the dark variant, so a placeholder that draws
/// TextSecondary does so through that token alias.
/// </remarks>
public sealed class RenderedControlColourTests
{
    /// <summary>The WCAG floor for a non-text graphic against its background.</summary>
    private const double MinimumNonTextContrast = 3.0;

    /// <summary>The most Tab presses the toggle test allows, so a broken tab order fails instead of looping.</summary>
    private const int FocusTabLimit = 4;

    /// <summary>An empty TextBox draws its placeholder in the TextSecondary token in each theme, as rendered.</summary>
    [Theory]
    [InlineData("Dark")]
    [InlineData("Light")]
    public async Task ATextBoxPlaceholderDrawsTheTextSecondaryToken(string variantName)
    {
        var variant = VariantOf(variantName);
        const string placeholder = "Search the workspace";
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var box = new TextBox { PlaceholderText = placeholder, Width = 240 };
            var window = new Window
            {
                Content = new StackPanel { Children = { box } },
                Width = 320,
                Height = 160,
                RequestedThemeVariant = variant,
            };
            window.Show();
            ShellHarness.Settle(window);

            var marks = box.GetVisualDescendants().OfType<TextBlock>()
                .Where(text => string.Equals(text.Text, placeholder, StringComparison.Ordinal))
                .ToList();
            marks.ShouldNotBeEmpty($"{variantName}: an empty TextBox must draw its placeholder");
            foreach (var mark in marks)
            {
                Colour(mark.Foreground).ShouldBe(Role(variant, "TextSecondary"), $"{variantName}: the placeholder must draw the TextSecondary token");
            }

            window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A ToggleButton reached by Tab draws the AccentPurple token as a border at the FocusBorder thickness, with no default adorner.</summary>
    [Theory]
    [InlineData("Dark")]
    [InlineData("Light")]
    public async Task AKeyboardFocusedToggleButtonDrawsTheAccentFocusBorder(string variantName)
    {
        var variant = VariantOf(variantName);
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var before = new Button { Content = "Before" };
            var toggle = new ToggleButton { Content = "Toggle" };
            var window = new Window
            {
                Content = new StackPanel { Children = { before, toggle } },
                Width = 320,
                Height = 200,
                RequestedThemeVariant = variant,
            };
            window.Show();
            ShellHarness.Settle(window);

            var accent = Role(variant, "AccentPurple");
            Colour(toggle.BorderBrush).ShouldNotBe(accent, $"{variantName}: an unfocused toggle does not draw the accent border");

            before.Focus();
            for (var presses = 0; !toggle.IsFocused && presses < FocusTabLimit; presses++)
            {
                window.KeyPress(Key.Tab, default, PhysicalKey.Tab, null);
                ShellHarness.Settle(window);
            }

            toggle.IsFocused.ShouldBeTrue($"{variantName}: Tab from the button must reach the toggle within {FocusTabLimit} presses");
            Colour(toggle.BorderBrush).ShouldBe(accent, $"{variantName}: a focused toggle draws the AccentPurple token as its border");
            toggle.BorderThickness.ShouldBe(FocusBorder(variant), $"{variantName}: a focused toggle draws the FocusBorder thickness");
            toggle.FocusAdorner.ShouldBeNull($"{variantName}: a focused toggle draws no default adorner");
            window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// The composer's stop square is drawn in TextPrimary and keeps at least 3:1 against the rendered background of the
    /// stop button it sits on, once that background's transition has ended.
    /// </summary>
    [Theory]
    [InlineData("Dark")]
    [InlineData("Light")]
    public async Task TheComposerStopSquareContrastsWithItsButtonInEachTheme(string variantName)
    {
        var variant = VariantOf(variantName);
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var window = ShellHarness.Show();
            window.RequestedThemeVariant = variant;
            ShellHarness.Settle(window);

            var stop = ShellHarness.Stop(window);
            var square = stop.GetVisualDescendants().OfType<Rectangle>().Single();
            var fill = Colour(square.Fill);
            fill.ShouldBe(Role(variant, "TextPrimary"), $"{variantName}: the stop square is drawn in the TextPrimary token");

            // The button Background animates for 100 ms after a theme switch, so an immediate read measures a mid-transition
            // colour. Wait, bounded by ShellWait's deadline and pumping the dispatcher, until the rendered brush reaches the
            // rest colour, then measure that rendered brush. ButtonBackground aliases SurfaceRaised (AutonogrammerTokens.axaml:41).
            var restBackground = Role(variant, "SurfaceRaised");
            await ShellWait.UntilAsync(
                window,
                () => stop.Background is ISolidColorBrush brush && brush.Color == restBackground,
                $"{variantName}: the stop button background to settle",
                cancellationToken).ConfigureAwait(true);

            var rendered = Colour(stop.Background);
            var ratio = ContrastRatio(fill, rendered);
            ratio.ShouldBeGreaterThanOrEqualTo(MinimumNonTextContrast, $"{variantName}: the stop square is {ratio:0.00}:1 against its rendered button background");
            window.Close();
        }, cancellationToken).ConfigureAwait(true);
    }

    private static ThemeVariant VariantOf(string name) =>
        string.Equals(name, "Light", StringComparison.Ordinal) ? ThemeVariant.Light : ThemeVariant.Dark;

    private static Color Colour(IBrush? brush) => ((ISolidColorBrush)brush!).Color;

    private static Color Role(ThemeVariant variant, string role)
    {
        Application.Current!.TryGetResource($"Autonogrammer.Color.{role}", variant, out var value).ShouldBeTrue($"{role} must resolve");
        return ((ISolidColorBrush)value!).Color;
    }

    private static Thickness FocusBorder(ThemeVariant variant)
    {
        Application.Current!.TryGetResource("Autonogrammer.Size.FocusBorder", variant, out var value).ShouldBeTrue("the focus border token must resolve");
        return (Thickness)value!;
    }

    /// <summary>The WCAG 2.x contrast ratio of two colours, from 1:1 to 21:1.</summary>
    private static double ContrastRatio(Color first, Color second)
    {
        var a = RelativeLuminance(first);
        var b = RelativeLuminance(second);
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    private static double RelativeLuminance(Color colour)
    {
        static double Channel(byte value)
        {
            var scaled = value / 255.0;
            return scaled <= 0.03928 ? scaled / 12.92 : Math.Pow((scaled + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Channel(colour.R)) + (0.7152 * Channel(colour.G)) + (0.0722 * Channel(colour.B));
    }
}
