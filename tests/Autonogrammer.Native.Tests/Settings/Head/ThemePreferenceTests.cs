using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Autonogrammer.Contracts.Ui;
using Autonogrammer.Native.Agents;
using Autonogrammer.Native.Settings;
using Autonogrammer.Native.Tests.AgentLoop;
using Autonogrammer.Native.Views.Blocks;
using Autonogrammer.Native.Views.Terminal;
using Autonogrammer.Presentation;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Settings.Head;

/// <summary>
/// The theme setting (spec 2.6): the application starts Light (OQ-94), a saved choice changes the running window at once, System follows the
/// platform, and controls built in code re-read the tokens when the variant changes.
/// </summary>
public sealed class ThemePreferenceTests
{
    private const string Markdown = "```\ncode\n```\n\n> quoted\n\n---";

    /// <summary>The application requests Light before any setting is read, so the first frame is light.</summary>
    [Fact]
    public async Task TheApplicationStartsLight()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            Application.Current!.RequestedThemeVariant.ShouldBe(ThemeVariant.Light);
            var window = ShellHarness.Show();

            window.ActualThemeVariant.ShouldBe(ThemeVariant.Light);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Each preference maps to its variant: System is the platform's own (Default), Light and Dark are fixed.</summary>
    [Theory]
    [InlineData(ThemePreference.System)]
    [InlineData(ThemePreference.Light)]
    [InlineData(ThemePreference.Dark)]
    public void EachPreferenceMapsToItsVariant(ThemePreference preference)
    {
        var expected = preference switch
        {
            ThemePreference.System => ThemeVariant.Default,
            ThemePreference.Light => ThemeVariant.Light,
            _ => ThemeVariant.Dark,
        };

        HeadSettings.VariantOf(preference).ShouldBe(expected);
    }

    /// <summary>A saved theme reaches the running window at once; System hands the choice back to the platform.</summary>
    [Fact]
    public async Task ASavedThemeChangesTheRunningWindow()
    {
        using var tree = new TempTree();
        var engines = new SessionEngineSet(new SessionEngineSetOptions
        {
            Loop = AgentLoopEnvironment.Resolve(static _ => null, tree.Workspace, tree.Workspace, tree.Root),
            AllowedKinds = [],
        });
        var store = new FileSettingsStore(tree.State);
        var settings = HeadSettings.Create(engines, store, TimeProvider.System, dispatch: null, new HeadSettingsOptions { GetVariable = static _ => null });
        try
        {
            settings.Theme.ShouldBe(ThemePreference.Light, "a missing file gives the Light default");
            await using var session = TestAppBuilder.StartSession();
            await session.Dispatch(() =>
            {
                var window = ShellHarness.Show();
                settings.Attach(window);
                window.ActualThemeVariant.ShouldBe(ThemeVariant.Light);

                store.SetTheme(ThemePreference.Dark);
                Application.Current!.RequestedThemeVariant.ShouldBe(ThemeVariant.Dark);
                window.ActualThemeVariant.ShouldBe(ThemeVariant.Dark);

                store.SetTheme(ThemePreference.Light);
                window.ActualThemeVariant.ShouldBe(ThemeVariant.Light);

                store.SetTheme(ThemePreference.System);
                Application.Current.RequestedThemeVariant.ShouldBe(ThemeVariant.Default);

                store.SetTheme(ThemePreference.Dark);
                window.ActualThemeVariant.ShouldBe(ThemeVariant.Dark);
            }, TestContext.Current.CancellationToken).ConfigureAwait(true);
        }
        finally
        {
            await settings.DisposeAsync().ConfigureAwait(true);
            await engines.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>
    /// A markdown block builds its code, quote and rule colours in code. After the variant changes they show the new variant's roles,
    /// not the roles of the variant that was showing when the block was built.
    /// </summary>
    [Fact]
    public async Task AMarkdownBlockRecoloursWhenTheVariantChanges()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var view = new MarkdownBlockView(new MarkdownBlock { Id = "m-1", Markdown = Markdown });
            var window = new Window { Content = view, Width = 480, Height = 480, RequestedThemeVariant = ThemeVariant.Dark };
            window.Show();
            ShellHarness.Settle(window);

            var borders = view.GetVisualDescendants().OfType<Border>().Where(static border => border.Parent is not null && border.Name is null).ToList();
            var code = borders.Single(static border => border.Background is not null && border.Height != 1);
            var quote = borders.Single(static border => border.BorderThickness.Left == 2);
            var rule = borders.Single(static border => border.Height == 1);

            // Spec 4.6: a code block is the sunken card, a block quote has a 2 px BorderStrong bar and a rule is a 1 px Border line.
            Colour(code.Background).ShouldBe(Role(ThemeVariant.Dark, "SurfaceSunken"));
            Colour(quote.BorderBrush).ShouldBe(Role(ThemeVariant.Dark, "BorderStrong"));
            Colour(rule.Background).ShouldBe(Role(ThemeVariant.Dark, "Border"));

            window.RequestedThemeVariant = ThemeVariant.Light;
            ShellHarness.Settle(window);

            Colour(code.Background).ShouldBe(Role(ThemeVariant.Light, "SurfaceSunken"));
            Colour(quote.BorderBrush).ShouldBe(Role(ThemeVariant.Light, "BorderStrong"));
            Colour(rule.Background).ShouldBe(Role(ThemeVariant.Light, "Border"));
            Role(ThemeVariant.Light, "SurfaceRaised").ShouldNotBe(Role(ThemeVariant.Dark, "SurfaceRaised"));
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The terminal palette is the spec table: Light and Dark differ and every ANSI colour is set.</summary>
    [Fact]
    public void TheTerminalPaletteHasSixteenColoursPerVariant()
    {
        var dark = Ansi(TerminalPalette.For(ThemeVariant.Dark));
        var light = Ansi(TerminalPalette.For(ThemeVariant.Light));

        dark.Count.ShouldBe(16);
        light.Count.ShouldBe(16);
        foreach (var colour in dark.Concat(light))
        {
            colour.ShouldNotBeNull();
            colour.Length.ShouldBe(7);
            colour[0].ShouldBe('#');
        }

        dark.ShouldNotBe(light);
    }

    private static List<string?> Ansi(XTerm.Options.ThemeOptions theme) =>
    [
        theme.Black, theme.Red, theme.Green, theme.Yellow, theme.Blue, theme.Magenta, theme.Cyan, theme.White,
        theme.BrightBlack, theme.BrightRed, theme.BrightGreen, theme.BrightYellow, theme.BrightBlue, theme.BrightMagenta, theme.BrightCyan, theme.BrightWhite,
    ];

    private static Color Colour(IBrush? brush) => ((ISolidColorBrush)brush!).Color;

    private static Color Role(ThemeVariant variant, string role)
    {
        Application.Current!.TryGetResource("Autonogrammer.Color." + role, variant, out var value).ShouldBeTrue();
        return ((ISolidColorBrush)value!).Color;
    }
}
