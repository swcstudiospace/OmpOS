using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.Theme;

/// <summary>
/// The text a button presents takes the button's own colour. An application-wide TextBlock foreground would win over the button's
/// state colour and draw a primary button's label in the body text colour: dark on the Light accent, light on the Dark accent.
/// </summary>
public sealed class ButtonLabelColourTests
{
    /// <summary>A primary button's string label is drawn in the on-accent role, and a plain button's in the body role, in both variants.</summary>
    [Theory]
    [InlineData("Dark")]
    [InlineData("Light")]
    public async Task AButtonLabelUsesTheButtonsOwnColour(string variantName)
    {
        var variant = string.Equals(variantName, "Light", StringComparison.Ordinal) ? ThemeVariant.Light : ThemeVariant.Dark;
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var primary = new Button { Content = "Save", Classes = { "primary" } };
            var plain = new Button { Content = "Close" };
            var window = new Window
            {
                Content = new StackPanel { Children = { primary, plain } },
                Width = 320,
                Height = 200,
                RequestedThemeVariant = variant,
            };
            window.Show();
            ShellHarness.Settle(window);

            Colour(LabelOf(primary).Foreground).ShouldBe(Role(variant, "AccentPurpleOn"), "a primary label sits on the accent fill");
            Colour(LabelOf(plain).Foreground).ShouldBe(Role(variant, "TextPrimary"), "a plain label is body text");
            window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    private static TextBlock LabelOf(Button button) => button.GetVisualDescendants().OfType<TextBlock>().Single();

    private static Color Colour(IBrush? brush) => ((ISolidColorBrush)brush!).Color;

    private static Color Role(ThemeVariant variant, string role)
    {
        Application.Current!.TryGetResource($"OmpOS.Color.{role}", variant, out var value).ShouldBeTrue($"{role} must resolve");
        return ((ISolidColorBrush)value!).Color;
    }
}
