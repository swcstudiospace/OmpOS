using System.Xml.Linq;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.Branding;

/// <summary>The Android head's launcher icon: the manifest names the icon and the round icon, and every resource they and the adaptive icon name exists.</summary>
public sealed class AndroidManifestTests
{
    private static readonly XNamespace AndroidNamespace = "http://schemas.android.com/apk/res/android";
    private static readonly string[] Densities = ["mdpi", "hdpi", "xhdpi", "xxhdpi", "xxxhdpi"];

    /// <summary>The application element names the label OmpOS, the icon and the round icon.</summary>
    [Fact]
    public void TheManifestNamesTheLabelAndBothIcons()
    {
        var application = Manifest().Root.ShouldNotBeNull().Element("application").ShouldNotBeNull();

        ((string?)application.Attribute(AndroidNamespace + "label")).ShouldBe("OmpOS");
        ((string?)application.Attribute(AndroidNamespace + "icon")).ShouldBe("@mipmap/ic_launcher");
        ((string?)application.Attribute(AndroidNamespace + "roundIcon")).ShouldBe("@mipmap/ic_launcher_round");
    }

    /// <summary>Each density has the launcher, round and foreground bitmaps, and each bitmap is a PNG.</summary>
    [Fact]
    public void EveryDensityHasTheLauncherBitmaps()
    {
        var resources = Path.Combine(AndroidDirectory(), "Resources");

        foreach (var density in Densities)
        {
            foreach (var name in new[] { "ic_launcher.png", "ic_launcher_round.png", "ic_launcher_foreground.png" })
            {
                var path = Path.Combine(resources, "mipmap-" + density, name);
                File.Exists(path).ShouldBeTrue(path);
                File.ReadAllBytes(path).Take(8).ToArray().ShouldBe([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], path);
            }
        }
    }

    /// <summary>The adaptive icons of Android 8 and later point at the foreground bitmap and at a background colour that exists and is the brand purple.</summary>
    [Theory]
    [InlineData("ic_launcher.xml")]
    [InlineData("ic_launcher_round.xml")]
    public void TheAdaptiveIconPointsAtExistingResources(string file)
    {
        var resources = Path.Combine(AndroidDirectory(), "Resources");
        var icon = XDocument.Load(Path.Combine(resources, "mipmap-anydpi-v26", file)).Root.ShouldNotBeNull();
        icon.Name.LocalName.ShouldBe("adaptive-icon");

        var foreground = (string?)icon.Element("foreground").ShouldNotBeNull().Attribute(AndroidNamespace + "drawable");
        var background = (string?)icon.Element("background").ShouldNotBeNull().Attribute(AndroidNamespace + "drawable");

        foreground.ShouldBe("@mipmap/ic_launcher_foreground");
        background.ShouldBe("@color/ic_launcher_background");
        var colours = XDocument.Load(Path.Combine(resources, "values", "ic_launcher_background.xml")).Root.ShouldNotBeNull();
        colours.Elements("color")
            .Single(static element => string.Equals((string?)element.Attribute("name"), "ic_launcher_background", StringComparison.Ordinal))
            .Value.ToUpperInvariant().ShouldBe("#6D28D9");
    }

    private static XDocument Manifest() => XDocument.Load(Path.Combine(AndroidDirectory(), "Properties", "AndroidManifest.xml"));

    private static string AndroidDirectory() => Path.Combine(ShellHarness.RepoRoot(), "apps", "OmpOS.Android");
}
