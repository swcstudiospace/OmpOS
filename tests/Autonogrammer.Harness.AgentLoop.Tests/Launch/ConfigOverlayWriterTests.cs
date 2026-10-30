using System.Runtime.Versioning;
using Autonogrammer.Harness.AgentLoop.Launch;
using Autonogrammer.Harness.AgentLoop.Tests.Plugins;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.AgentLoop.Tests.Launch;

/// <summary>
/// Verifies when the overlay is written, what it holds, its owner-only modes, and that disposing deletes it.
/// </summary>
public sealed class ConfigOverlayWriterTests
{
    private static AgentLoopSettings Settings(bool disableProjectMcp) =>
        new() { PluginsRoot = "/p", StateRoot = "/s", DisableProjectMcpConfig = disableProjectMcp, SubstrateToken = "super-secret-token" };

    [Fact]
    public void NothingIsWrittenWhenNoOverlayKeyIsAsked()
    {
        using var fixture = new PluginFixtureDirectory();
        var sessionDirectory = fixture.PathOf("state/sessions/s1");

        var writer = ConfigOverlayWriter.Write(Settings(disableProjectMcp: false), sessionDirectory);

        writer.ShouldBeNull();
        Directory.Exists(sessionDirectory).ShouldBeFalse();
    }

    [Fact]
    public void WritesTheYamlMappingAtTheLayoutPathAndNeverASecret()
    {
        using var fixture = new PluginFixtureDirectory();
        var sessionDirectory = fixture.PathOf("state/sessions/s1");

        using var writer = ConfigOverlayWriter.Write(Settings(disableProjectMcp: true), sessionDirectory);

        writer.ShouldNotBeNull();
        writer.Path.ShouldBe(SessionStateLayout.ConfigOverlayFile(sessionDirectory));
        var text = File.ReadAllText(writer.Path);
        text.ShouldBe("mcp:\n  enableProjectConfig: false\n");
        text.ShouldNotContain("super-secret-token");
    }

    [Fact]
    [SupportedOSPlatform("linux")]
    public void FileHasModeZeroSixHundredAndANewDirectoryZeroSevenHundred()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Unix file modes do not exist on Windows.");
        }

        using var fixture = new PluginFixtureDirectory();
        var sessionDirectory = fixture.PathOf("state/sessions/s1");

        using var writer = ConfigOverlayWriter.Write(Settings(disableProjectMcp: true), sessionDirectory);

        writer.ShouldNotBeNull();
        File.GetUnixFileMode(writer.Path).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite);
        File.GetUnixFileMode(sessionDirectory).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    [Fact]
    [SupportedOSPlatform("linux")]
    public void AnExistingWiderFileIsTightenedToZeroSixHundred()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Unix file modes do not exist on Windows.");
        }

        using var fixture = new PluginFixtureDirectory();
        var sessionDirectory = fixture.PathOf("state/sessions/s1");
        var overlay = SessionStateLayout.ConfigOverlayFile(sessionDirectory);
        fixture.WriteFile("state/sessions/s1/omp-overlay.yml", "stale: true\n");
        File.SetUnixFileMode(overlay, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);

        using var writer = ConfigOverlayWriter.Write(Settings(disableProjectMcp: true), sessionDirectory);

        writer.ShouldNotBeNull();
        File.GetUnixFileMode(overlay).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite);
        File.ReadAllText(overlay).ShouldBe("mcp:\n  enableProjectConfig: false\n");
    }

    [Fact]
    public void DisposeDeletesTheFileAndKeepsTheDirectoryAndIsRepeatable()
    {
        using var fixture = new PluginFixtureDirectory();
        var sessionDirectory = fixture.PathOf("state/sessions/s1");
        var writer = ConfigOverlayWriter.Write(Settings(disableProjectMcp: true), sessionDirectory);
        writer.ShouldNotBeNull();
        File.Exists(writer.Path).ShouldBeTrue();

        writer.Dispose();
        writer.Dispose();

        File.Exists(writer.Path).ShouldBeFalse();
        Directory.Exists(sessionDirectory).ShouldBeTrue();
    }

    [Fact]
    public void WriteYamlWritesExactlyTheCallersYamlAndDeletesItOnDispose()
    {
        using var fixture = new PluginFixtureDirectory();
        var sessionDirectory = fixture.PathOf("state/sessions/s1");
        const string yaml = "browser:\n  enabled: false\ncomputer:\n  enabled: false\n";

        var writer = ConfigOverlayWriter.WriteYaml(sessionDirectory, yaml);

        writer.Path.ShouldBe(SessionStateLayout.ConfigOverlayFile(sessionDirectory));
        File.ReadAllText(writer.Path).ShouldBe(yaml);
        File.ReadAllText(writer.Path).ShouldNotContain("mcp:");
        File.ReadAllText(writer.Path).ShouldNotContain("workspace:");

        writer.Dispose();
        writer.Dispose();
        File.Exists(writer.Path).ShouldBeFalse();
        Directory.Exists(sessionDirectory).ShouldBeTrue();
    }

    [Fact]
    [SupportedOSPlatform("linux")]
    public void WriteYamlIsOwnerOnly()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Unix file modes do not exist on Windows.");
        }

        using var fixture = new PluginFixtureDirectory();
        var sessionDirectory = fixture.PathOf("state/sessions/yaml");

        using var writer = ConfigOverlayWriter.WriteYaml(sessionDirectory, "browser:\n  enabled: false\n");

        File.GetUnixFileMode(writer.Path).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite);
        File.GetUnixFileMode(sessionDirectory).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    [Fact]
    public void WriteYamlRefusesEmptyYaml()
    {
        using var fixture = new PluginFixtureDirectory();
        var sessionDirectory = fixture.PathOf("state/sessions/empty");

        Should.Throw<ArgumentException>(() => ConfigOverlayWriter.WriteYaml(sessionDirectory, "  "));
        Directory.Exists(sessionDirectory).ShouldBeFalse();
    }
}
