using System.Xml.Linq;
using Avalonia.Styling;
using Autonogrammer.Native.Settings;
using Autonogrammer.Presentation;
using Autonogrammer.Presentation.Settings;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Theme;

/// <summary>Light is the default theme on every head (OQ-94): the presentation default, the settings file, and the application markup of both heads. A saved choice, Dark included, is kept.</summary>
public sealed class DefaultThemeTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "autonogrammer-theme-" + Guid.NewGuid().ToString("N"));

    /// <summary>Removes the temporary state directory.</summary>
    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>The presentation default and the head's stored default are both Light, and the variant of Light is the Light variant.</summary>
    [Fact]
    public void TheDefaultsAreLight()
    {
        GeneralSettingsValues.Defaults.Theme.ShouldBe(ThemePreference.Light);
        StoredSettings.Defaults.Theme.ShouldBe(ThemePreference.Light);
        HeadSettings.VariantOf(StoredSettings.Defaults.Theme).ShouldBe(ThemeVariant.Light);
    }

    /// <summary>A settings file that has no theme, an empty object, or other members only reads as Light; a file with a theme keeps it. Nothing is written by reading.</summary>
    [Theory]
    [InlineData(null, ThemePreference.Light)]
    [InlineData("{}", ThemePreference.Light)]
    [InlineData("{\"motion\":\"reduced\"}", ThemePreference.Light)]
    [InlineData("{\"theme\":\"light\"}", ThemePreference.Light)]
    [InlineData("{\"theme\":\"dark\"}", ThemePreference.Dark)]
    [InlineData("{\"theme\":\"DARK\"}", ThemePreference.Dark)]
    [InlineData("{\"theme\":\"system\"}", ThemePreference.System)]
    public void AMissingThemeReadsAsLightAndASavedOneIsKept(string? json, ThemePreference expected)
    {
        var store = new FileSettingsStore(_root);
        if (json is not null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(store.FilePath)!);
            File.WriteAllText(store.FilePath, json);
        }

        store.Read().Theme.ShouldBe(expected);
        (File.Exists(store.FilePath) ? File.ReadAllText(store.FilePath) : null).ShouldBe(json);
    }

    /// <summary>Every preference survives a write and a read by a new store, whichever default the file started from.</summary>
    [Theory]
    [InlineData(ThemePreference.System)]
    [InlineData(ThemePreference.Light)]
    [InlineData(ThemePreference.Dark)]
    public void EveryPreferenceSurvivesAWrite(ThemePreference preference)
    {
        new FileSettingsStore(_root).SetTheme(preference);

        new FileSettingsStore(_root).Read().Theme.ShouldBe(preference);
    }

    /// <summary>Saving a General page with its default writes Light, not a hidden Dark.</summary>
    [Fact]
    public void SavingTheGeneralDefaultsWritesLight()
    {
        var store = new FileSettingsStore(_root);

        store.SetGeneral(GeneralSettingsValues.Defaults.Motion, GeneralSettingsValues.Defaults.Theme);

        File.ReadAllText(store.FilePath).ShouldContain("\"theme\": \"light\"", Case.Insensitive);
    }

    /// <summary>The desktop and the Android application markup both request the Light variant before any setting is read.</summary>
    [Theory]
    [InlineData("apps/Autonogrammer.Native/App.axaml")]
    [InlineData("apps/Autonogrammer.Android/AutonogrammerAndroidApp.axaml")]
    public void TheApplicationsRequestLight(string relativePath)
    {
        var root = XDocument.Load(Path.Combine(ShellHarness.RepoRoot(), relativePath)).Root.ShouldNotBeNull();

        ((string?)root.Attribute("RequestedThemeVariant")).ShouldBe("Light");
    }
}
