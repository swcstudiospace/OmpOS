using System.IO.Compression;
using Autonogrammer.Harness.PluginBundles.Tests.Support;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.PluginBundles.Tests;

/// <summary>Install refuses hostile bundles with no leftover directory, and state stores round-trip.</summary>
public sealed class BundleInstallerTests
{
    /// <summary>A traversal zip fails with <c>BundleInvalid(BundleTraversal)</c> and the target directory is absent.</summary>
    [Fact]
    public void TraversalZipRollsBack()
    {
        using var fixture = new Fixture();
        var zip = BundleFixtureBuilder.WriteZip(fixture.Work, "demo.zip", [new BundleZipEntry("../escape.txt", BundleFixtureBuilder.Text("no"))]);
        var error = Should.Throw<BundleValidationException>(() => fixture.Installer.Install(zip, fixture.Destination, BundleInstallScope.User));
        error.Message.ShouldBe("BundleInvalid(BundleTraversal)");
        Directory.Exists(Path.Combine(fixture.Destination, "demo")).ShouldBeFalse();
        File.Exists(Path.Combine(fixture.Work, "escape.txt")).ShouldBeFalse();
    }

    /// <summary>A symlink zip fails with <c>BundleInvalid(BundleSymlink)</c> and leaves no extracted bundle.</summary>
    [Fact]
    public void SymlinkZipRollsBack()
    {
        using var fixture = new Fixture();
        var zip = BundleFixtureBuilder.WriteZip(fixture.Work, "demo.zip", [new BundleZipEntry("link", BundleFixtureBuilder.Text("target"), Symlink: true)]);
        var error = Should.Throw<BundleValidationException>(() => fixture.Installer.Install(zip, fixture.Destination, BundleInstallScope.User));
        error.Message.ShouldBe("BundleInvalid(BundleSymlink)");
        Directory.Exists(Path.Combine(fixture.Destination, "demo")).ShouldBeFalse();
        Directory.GetDirectories(fixture.Destination).ShouldBeEmpty();
    }

    /// <summary>Oversized files, too many files, an oversized bundle and a malformed manifest each roll back with their named code.</summary>
    [Fact]
    public void SizeCountAndManifestRollBack()
    {
        using var fixture = new Fixture();
        var tooLarge = BundleFixtureBuilder.WriteZip(fixture.Work, "big.zip", [new BundleZipEntry("skills/foo/SKILL.md", new byte[BundleValidator.MaxFileBytes + 1])]);
        Should.Throw<BundleValidationException>(() => fixture.Installer.Install(tooLarge, fixture.Destination, BundleInstallScope.User))
            .Message.ShouldBe("BundleInvalid(BundleTooLarge)");
        Directory.Exists(Path.Combine(fixture.Destination, "big")).ShouldBeFalse();

        var many = new List<BundleZipEntry>();
        for (var index = 0; index < BundleValidator.MaxFileCount + 1; index++)
        {
            many.Add(new BundleZipEntry("f" + index + ".txt", [1]));
        }

        var tooMany = BundleFixtureBuilder.WriteZip(fixture.Work, "many.zip", many);
        Should.Throw<BundleValidationException>(() => fixture.Installer.Install(tooMany, fixture.Destination, BundleInstallScope.User))
            .Message.ShouldBe("BundleInvalid(BundleTooManyFiles)");

        var bomb = WriteBundleOverTotalCap(Path.Combine(fixture.Work, "bomb.zip"));
        Should.Throw<BundleValidationException>(() => fixture.Installer.Install(bomb, fixture.Destination, BundleInstallScope.User))
            .Message.ShouldBe("BundleInvalid(BundleTooBig)");
        Directory.Exists(Path.Combine(fixture.Destination, "bomb")).ShouldBeFalse();

        var bad = BundleFixtureBuilder.WriteZip(
            fixture.Work,
            "bad.zip",
            [new BundleZipEntry(".claude-plugin/plugin.json", BundleFixtureBuilder.Text("{"))]);
        Should.Throw<BundleValidationException>(() => fixture.Installer.Install(bad, fixture.Destination, BundleInstallScope.User))
            .Message.ShouldBe("BundleInvalid(BundleBadManifest)");
        Directory.GetDirectories(fixture.Destination).ShouldBeEmpty();
    }

    /// <summary>Enabling then disabling a bundle and a skill survives a new store reading the same files.</summary>
    [Fact]
    public void StateStoresRoundTrip()
    {
        using var fixture = new Fixture();
        var plugins = Path.Combine(fixture.Work, "plugins-state.json");
        var skills = Path.Combine(fixture.Work, "skills-state.json");
        var bundles = new BundleStateStore(plugins);
        bundles.SetEnabled("demo", true);
        bundles.SetEnabled("demo", false);
        new BundleStateStore(plugins).TryGetEnabled("demo", out var enabled).ShouldBeTrue();
        enabled.ShouldBeFalse();

        var skillStore = new SkillStateStore(skills);
        skillStore.SetEnabled("demo", "foo", false);
        new SkillStateStore(skills).IsEnabled("demo", "foo").ShouldBeFalse();
        skillStore.SetEnabled("demo", "foo", true);
        new SkillStateStore(skills).IsEnabled("demo", "foo").ShouldBeTrue();
    }

    private static string WriteBundleOverTotalCap(string path)
    {
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        var buffer = new byte[BundleValidator.MaxFileBytes];
        var remaining = BundleValidator.MaxBundleBytes + 1;
        var index = 0;
        while (remaining > 0)
        {
            var count = (int)Math.Min(buffer.Length, remaining);
            var entry = archive.CreateEntry("f" + index + ".bin", CompressionLevel.NoCompression);
            using var stream = entry.Open();
            stream.Write(buffer, 0, count);
            remaining -= count;
            index++;
        }

        return path;
    }

    private sealed class Fixture : IDisposable
    {
        public Fixture()
        {
            Work = Path.Combine(Path.GetTempPath(), "ag-bundle-" + Guid.NewGuid().ToString("N"));
            Destination = Path.Combine(Work, "plugins");
            Directory.CreateDirectory(Destination);
            Installer = new BundleInstaller();
        }

        public string Work { get; }

        public string Destination { get; }

        public BundleInstaller Installer { get; }

        public void Dispose() => Directory.Delete(Work, recursive: true);
    }
}
