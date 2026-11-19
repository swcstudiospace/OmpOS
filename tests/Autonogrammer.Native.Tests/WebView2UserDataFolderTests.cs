using Autonogrammer.Native.Views.Cowork.Artifacts;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests;

/// <summary>
/// Where the artifact surface keeps its WebView2 data. The default is a folder beside the executable, which cannot be created once the
/// application is installed under Program Files, so the surface names a per-user folder instead.
/// </summary>
public sealed class WebView2UserDataFolderTests
{
    /// <summary>The folder is <c>Autonogrammer/webview2</c> below the per-user local application data folder.</summary>
    [Fact]
    public void TheFolderIsBelowTheLocalApplicationData()
    {
        var local = Path.Combine(Path.GetTempPath(), "local-app-data");

        NativeWebViewArtifactSurface.WebView2UserDataFolder(local).ShouldBe(Path.Combine(local, "Autonogrammer", "webview2"));
    }

    /// <summary>The production overload reads the real per-user folder and never the application directory.</summary>
    [Fact]
    public void TheProductionFolderIsNotBesideTheExecutable()
    {
        var folder = NativeWebViewArtifactSurface.WebView2UserDataFolder();
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        folder.ShouldBe(Path.Combine(localAppData, "Autonogrammer", "webview2"));
        folder.StartsWith(AppContext.BaseDirectory, StringComparison.OrdinalIgnoreCase).ShouldBeFalse();
    }

    /// <summary>A null, empty or blank local application data folder falls back to the temporary folder instead of throwing or yielding a relative path.</summary>
    [Fact]
    public void ABlankLocalApplicationDataFallsBackToTheTemporaryFolder()
    {
        var expected = Path.Combine(Path.GetTempPath(), "Autonogrammer", "webview2");

        NativeWebViewArtifactSurface.WebView2UserDataFolder(null).ShouldBe(expected);
        NativeWebViewArtifactSurface.WebView2UserDataFolder(string.Empty).ShouldBe(expected);
        NativeWebViewArtifactSurface.WebView2UserDataFolder("   ").ShouldBe(expected);
        Path.IsPathRooted(expected).ShouldBeTrue();
    }
}
