using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests;

/// <summary>
/// The static font instances behind the folder-URI tokens (R3, spec 2.4). Avalonia cannot change a variable axis, so a
/// weight is only drawn when a face of that weight is bundled and the font manager groups it under the token family.
/// The default headless platform draws with a stub font manager that ignores faces, so these tests run on the real
/// Skia font stack in a headless session of their own.
/// </summary>
public sealed class FontFaceResolutionTests
{
    private const string Sample = "Needs you · Running · Waiting for review";

    /// <summary>DM Sans at weight 400 and at weight 600 are two different faces: the same string measures differently.</summary>
    [Fact]
    public async Task DmSansRegularAndSemiBoldMeasureTheSameStringDifferently()
    {
        await using var session = StartSkiaSession();
        await session.Dispatch(() =>
        {
            var family = Token("Autonogrammer.Font.Family");

            var regular = Advance(family, FontWeight.Normal);
            var medium = Advance(family, FontWeight.Medium);
            var semiBold = Advance(family, FontWeight.SemiBold);

            semiBold.ShouldBeGreaterThan(regular, "a SemiBold face is wider than Regular, so weight 600 is not drawn from the Regular outline");
            medium.ShouldNotBe(regular);
            medium.ShouldNotBe(semiBold);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Each requested weight of each family resolves to a bundled face of that weight and of that family.</summary>
    [Theory]
    [InlineData("Autonogrammer.Font.Family", 400, "DM Sans")]
    [InlineData("Autonogrammer.Font.Family", 500, "DM Sans")]
    [InlineData("Autonogrammer.Font.Family", 600, "DM Sans")]
    [InlineData("Autonogrammer.Font.Display", 600, "Anybody")]
    [InlineData("Autonogrammer.Font.Mono", 400, "Space Mono")]
    [InlineData("Autonogrammer.Font.Mono", 700, "Space Mono")]
    public async Task TheFontManagerResolvesTheRequestedWeightToABundledFace(string token, int weight, string familyName)
    {
        await using var session = StartSkiaSession();
        await session.Dispatch(() =>
        {
            var typeface = new Typeface(Token(token), FontStyle.Normal, (FontWeight)weight);

            FontManager.Current.TryGetGlyphTypeface(typeface, out var face).ShouldBeTrue($"{token} at {weight} must resolve to a face");
            (string.IsNullOrEmpty(face.TypographicFamilyName) ? face.FamilyName : face.TypographicFamilyName).ShouldBe(familyName, "the face must be grouped under the token family, not a fallback");
            face.FontSimulations.ShouldBe(FontSimulations.None, "a bundled static face is drawn as is, never synthesised bold");
            ((int)face.Weight).ShouldBe(weight, $"{token} at {weight} must resolve to the weight-{weight} face, not a synthesised or default instance");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    private static HeadlessUnitTestSession StartSkiaSession() =>
        HeadlessUnitTestSession.StartNew(typeof(SkiaFontAppBuilder), AvaloniaTestIsolationLevel.PerTest);

    private static FontFamily Token(string key) => (FontFamily)Application.Current!.FindResource(key)!;

    /// <summary>The summed horizontal advance of the sample, in font design units, read from the resolved face.</summary>
    private static int Advance(FontFamily family, FontWeight weight)
    {
        FontManager.Current.TryGetGlyphTypeface(new Typeface(family, FontStyle.Normal, weight), out var face).ShouldBeTrue();
        var total = 0;
        foreach (var rune in Sample.EnumerateRunes())
        {
            face.CharacterToGlyphMap.TryGetGlyph(rune.Value, out var glyph).ShouldBeTrue($"the face must have a glyph for U+{rune.Value:X4}");
            face.TryGetHorizontalGlyphAdvance(glyph, out var advance).ShouldBeTrue();
            total += advance;
        }

        return total;
    }

    /// <summary>The application on the Skia headless platform: real glyph typefaces.</summary>
    internal static class SkiaFontAppBuilder
    {
        /// <summary>Builds the native application on Skia with headless input and no stub drawing.</summary>
        /// <returns>The application builder the headless runner starts.</returns>
        public static AppBuilder BuildAvaloniaApp() =>
            AppBuilder.Configure<App>()
                .UseSkia()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
    }
}
