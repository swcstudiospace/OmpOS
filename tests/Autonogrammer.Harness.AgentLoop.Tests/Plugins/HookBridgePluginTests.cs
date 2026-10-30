using Autonogrammer.Harness.AgentLoop.Plugins;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.AgentLoop.Tests.Plugins;

/// <summary>Resolving the hooks bridge extension: a package under the plugins root is ready, a missing or empty folder is reported, and the extension in this repository resolves.</summary>
public sealed class HookBridgePluginTests
{
    /// <summary>A package that declares its extension resolves to its directory, which is what omp is given with <c>-e</c>.</summary>
    [Fact]
    public void APackageResolvesToItsDirectory()
    {
        using var fixture = new PluginFixtureDirectory();
        fixture.WritePackage("plugins/autonogrammer-hooks-omp", "omp", "./src/index.mjs");
        fixture.WriteFile("plugins/autonogrammer-hooks-omp/src/index.mjs");

        var resolved = HookBridgePlugin.Resolve(fixture.PluginsRoot);

        resolved.Health.ShouldBe(PluginHealth.Ready);
        resolved.EntryPaths.ShouldBe([fixture.PathOf("plugins/autonogrammer-hooks-omp")]);
    }

    /// <summary>A plugins root without the extension folder is reported as missing, with the fix, and nothing is thrown.</summary>
    [Fact]
    public void AMissingFolderIsReportedNotThrown()
    {
        using var fixture = new PluginFixtureDirectory();
        Directory.CreateDirectory(fixture.PluginsRoot);

        var resolved = HookBridgePlugin.Resolve(fixture.PluginsRoot);

        resolved.Health.ShouldBe(PluginHealth.EntryMissing);
        resolved.EntryPaths.ShouldBeEmpty();
        resolved.Message.ShouldContain("does not exist");
    }

    /// <summary>A package whose declared extension file is missing is not ready.</summary>
    [Fact]
    public void ADeclaredExtensionThatDoesNotExistIsNotReady()
    {
        using var fixture = new PluginFixtureDirectory();
        fixture.WritePackage("plugins/autonogrammer-hooks-omp", "omp", "./src/index.mjs");

        HookBridgePlugin.Resolve(fixture.PluginsRoot).Health.ShouldBe(PluginHealth.EntryMissing);
    }

    /// <summary>The extension that ships in this repository resolves as ready: its package.json declares an extension file that exists.</summary>
    [Fact]
    public void TheExtensionInThisRepositoryResolves()
    {
        var plugins = FindPluginsRoot();

        var resolved = HookBridgePlugin.Resolve(plugins);

        resolved.Health.ShouldBe(PluginHealth.Ready, resolved.Message);
        resolved.EntryPaths.ShouldHaveSingleItem().ShouldEndWith("autonogrammer-hooks-omp");
    }

    internal static string FindPluginsRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "plugins");
            if (File.Exists(Path.Combine(candidate, "autonogrammer-hooks-omp", "package.json")))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException("The plugins folder of the repository was not found above the test output.");
    }
}
