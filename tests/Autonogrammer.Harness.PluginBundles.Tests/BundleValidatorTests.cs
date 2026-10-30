using Autonogrammer.Harness.PluginBundles.Tests.Support;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.PluginBundles.Tests;

/// <summary>Hostile archives are refused by name before any install directory is committed.</summary>
public sealed class BundleValidatorTests
{
    /// <summary>A zip entry whose name contains <c>..</c> is <c>BundleInvalid(BundleTraversal)</c>.</summary>
    [Fact]
    public void ZipSlipIsTraversal()
    {
        var root = Path.Combine(Path.GetTempPath(), "ag-bundle-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var zip = BundleFixtureBuilder.WriteZip(root, "demo.zip", [new BundleZipEntry("../escape.txt", BundleFixtureBuilder.Text("no"))]);
            var error = Should.Throw<BundleValidationException>(() => BundleValidator.ValidateZip(zip, "demo"));
            error.Code.ShouldBe(BundleInvalidCode.BundleTraversal);
            error.Message.ShouldBe("BundleInvalid(BundleTraversal)");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>A zip entry marked as a symbolic link is <c>BundleInvalid(BundleSymlink)</c>.</summary>
    [Fact]
    public void SymlinkEntryIsRefused()
    {
        var root = Path.Combine(Path.GetTempPath(), "ag-bundle-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var zip = BundleFixtureBuilder.WriteZip(root, "demo.zip", [new BundleZipEntry("link", BundleFixtureBuilder.Text("target"), Symlink: true)]);
            var error = Should.Throw<BundleValidationException>(() => BundleValidator.ValidateZip(zip, "demo"));
            error.Code.ShouldBe(BundleInvalidCode.BundleSymlink);
            error.Message.ShouldBe("BundleInvalid(BundleSymlink)");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
