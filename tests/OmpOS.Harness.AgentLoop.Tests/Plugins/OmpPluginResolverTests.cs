using OmpOS.Harness.AgentLoop.Plugins;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.AgentLoop.Tests.Plugins;

/// <summary>
/// Verifies resolving against temporary directory trees laid out like the submodules: every health, the guard against
/// entries that leave the plugin directory, and that resolving reads only.
/// </summary>
public sealed class OmpPluginResolverTests
{
    private static OmpPluginDescriptor Plugin(string id) => OmpPluginCatalog.BuiltIn.Single(plugin => string.Equals(plugin.Id, id, StringComparison.Ordinal));

    [Fact]
    public void EmptySubmoduleDirectoryIsNotInitialized()
    {
        using var fixture = new PluginFixtureDirectory();
        Directory.CreateDirectory(fixture.PathOf("plugins/agent-swarm"));

        var resolved = OmpPluginResolver.Resolve(fixture.PluginsRoot, Plugin("swarm"));

        resolved.Health.ShouldBe(PluginHealth.SubmoduleNotInitialized);
        resolved.EntryPaths.ShouldBeEmpty();
        resolved.Message.ShouldContain("git submodule update --init --recursive plugins/agent-swarm");
    }

    [Fact]
    public void MissingSubmoduleDirectoryIsNotInitialized()
    {
        using var fixture = new PluginFixtureDirectory();
        Directory.CreateDirectory(fixture.PluginsRoot);

        var resolved = OmpPluginResolver.Resolve(fixture.PluginsRoot, Plugin("ultrathink"));

        resolved.Health.ShouldBe(PluginHealth.SubmoduleNotInitialized);
        resolved.Message.ShouldContain("'ultrathink'");
        resolved.Message.ShouldContain(fixture.PathOf("plugins/claude-ultrathink"));
    }

    [Fact]
    public void NestedPluginReportsTheSubmoduleRootInTheFixCommand()
    {
        using var fixture = new PluginFixtureDirectory();
        Directory.CreateDirectory(fixture.PathOf("plugins/agent-substrate"));

        var resolved = OmpPluginResolver.Resolve(fixture.PluginsRoot, Plugin("substrate"));

        resolved.Health.ShouldBe(PluginHealth.SubmoduleNotInitialized);
        resolved.Message.ShouldEndWith("git submodule update --init --recursive plugins/agent-substrate");
        resolved.Message.ShouldNotContain("projector");
    }

    [Fact]
    public void InitializedSubmoduleWithoutTheNestedDirectoryIsEntryMissing()
    {
        using var fixture = new PluginFixtureDirectory();
        fixture.WriteFile("plugins/agent-swarm/.git", "gitdir: x\n");

        var resolved = OmpPluginResolver.Resolve(fixture.PluginsRoot, Plugin("swarm"));

        resolved.Health.ShouldBe(PluginHealth.EntryMissing);
        resolved.EntryPaths.ShouldBeEmpty();
    }

    [Fact]
    public void PackageWithoutPackageJsonIsEntryMissing()
    {
        using var fixture = new PluginFixtureDirectory();
        fixture.WriteFile("plugins/claude-ultrathink/README.md");

        var resolved = OmpPluginResolver.Resolve(fixture.PluginsRoot, Plugin("ultrathink"));

        resolved.Health.ShouldBe(PluginHealth.EntryMissing);
        resolved.Message.ShouldContain("package.json");
    }

    [Fact]
    public void DeclaredEntryThatDoesNotExistIsEntryMissing()
    {
        using var fixture = new PluginFixtureDirectory();
        fixture.WritePackage("plugins/claude-ultrathink", "omp", "./src/host/omp.ts");

        var resolved = OmpPluginResolver.Resolve(fixture.PluginsRoot, Plugin("ultrathink"));

        resolved.Health.ShouldBe(PluginHealth.EntryMissing);
        resolved.Message.ShouldContain("omp.ts");
        resolved.EntryPaths.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("../../outside.ts")]
    [InlineData("./src/../../escape.ts")]
    public void EntryThatClimbsOutOfThePluginDirectoryIsManifestInvalid(string entry)
    {
        using var fixture = new PluginFixtureDirectory();
        fixture.WritePackage("plugins/claude-ultrathink", "omp", entry);
        fixture.WriteFile("plugins/outside.ts");
        fixture.WriteFile("plugins/escape.ts");

        var resolved = OmpPluginResolver.Resolve(fixture.PluginsRoot, Plugin("ultrathink"));

        resolved.Health.ShouldBe(PluginHealth.ManifestInvalid);
        resolved.Message.ShouldContain("leaves the plugin directory");
        resolved.EntryPaths.ShouldBeEmpty();
    }

    [Fact]
    public void EntryThroughASymbolicLinkOutOfThePluginDirectoryIsManifestInvalid()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Creating symbolic links needs a privilege on Windows.");
        }

        using var fixture = new PluginFixtureDirectory();
        fixture.WritePackage("plugins/claude-ultrathink", "omp", "./src/index.ts");
        fixture.WriteFile("elsewhere/index.ts");
        Directory.CreateSymbolicLink(fixture.PathOf("plugins/claude-ultrathink/src"), fixture.PathOf("elsewhere"));

        var resolved = OmpPluginResolver.Resolve(fixture.PluginsRoot, Plugin("ultrathink"));

        resolved.Health.ShouldBe(PluginHealth.ManifestInvalid);
        resolved.Message.ShouldContain("leaves the plugin directory");
    }

    [Fact]
    public void EntryThroughASymbolicLinkInsideThePluginDirectoryIsAccepted()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Creating symbolic links needs a privilege on Windows.");
        }

        using var fixture = new PluginFixtureDirectory();
        fixture.WritePackage("plugins/claude-ultrathink", "omp", "./link/index.ts");
        fixture.WriteFile("plugins/claude-ultrathink/real/index.ts");
        Directory.CreateSymbolicLink(fixture.PathOf("plugins/claude-ultrathink/link"), fixture.PathOf("plugins/claude-ultrathink/real"));

        var resolved = OmpPluginResolver.Resolve(fixture.PluginsRoot, Plugin("ultrathink"));

        resolved.Health.ShouldBe(PluginHealth.Ready);
    }

    [Fact]
    public void AbsoluteEntryIsManifestInvalid()
    {
        using var fixture = new PluginFixtureDirectory();
        var outside = fixture.WriteFile("outside.ts");
        fixture.WritePackage("plugins/claude-ultrathink", "omp", outside);

        var resolved = OmpPluginResolver.Resolve(fixture.PluginsRoot, Plugin("ultrathink"));

        resolved.Health.ShouldBe(PluginHealth.ManifestInvalid);
        resolved.Message.ShouldContain("absolute");
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("[]")]
    [InlineData("{ \"name\": \"x\" }")]
    [InlineData("{ \"omp\": { \"extensions\": [] } }")]
    [InlineData("{ \"omp\": { \"extensions\": [1] } }")]
    [InlineData("{ \"omp\": { \"extensions\": \"./a.ts\" } }")]
    public void UnreadableOrEmptyPackageManifestIsManifestInvalid(string json)
    {
        using var fixture = new PluginFixtureDirectory();
        fixture.WriteFile("plugins/claude-ultrathink/package.json", json);

        var resolved = OmpPluginResolver.Resolve(fixture.PluginsRoot, Plugin("ultrathink"));

        resolved.Health.ShouldBe(PluginHealth.ManifestInvalid);
        resolved.EntryPaths.ShouldBeEmpty();
    }

    [Fact]
    public void ValidPackageIsReadyAndIsLoadedAsItsDirectory()
    {
        using var fixture = new PluginFixtureDirectory();
        fixture.WritePackage("plugins/claude-ultrathink", "omp", "./src/host/omp.ts");
        fixture.WriteFile("plugins/claude-ultrathink/src/host/omp.ts");

        var resolved = OmpPluginResolver.Resolve(fixture.PluginsRoot, Plugin("ultrathink"));

        resolved.Health.ShouldBe(PluginHealth.Ready);
        resolved.AbsoluteDirectory.ShouldBe(fixture.PathOf("plugins/claude-ultrathink"));
        resolved.EntryPaths.ShouldBe([fixture.PathOf("plugins/claude-ultrathink")]);
    }

    [Fact]
    public void PiExtensionsAreUsedWhenThePackageDeclaresNoOmpSection()
    {
        using var fixture = new PluginFixtureDirectory();
        fixture.WritePackage("plugins/claude-ultrathink", "pi", "./src/pi.ts");
        fixture.WriteFile("plugins/claude-ultrathink/src/pi.ts");

        OmpPluginResolver.Resolve(fixture.PluginsRoot, Plugin("ultrathink")).Health.ShouldBe(PluginHealth.Ready);
    }

    [Fact]
    public void SubstrateResolvesToItsTwoProjectorFilesInOrder()
    {
        using var fixture = new PluginFixtureDirectory();
        fixture.CreateAllPlugins();

        var resolved = OmpPluginResolver.Resolve(fixture.PluginsRoot, Plugin("substrate"));

        resolved.Health.ShouldBe(PluginHealth.Ready);
        resolved.EntryPaths.ShouldBe(
        [
            fixture.PathOf("plugins/agent-substrate/packages/projector/omp/substrate-status.ts"),
            fixture.PathOf("plugins/agent-substrate/packages/projector/omp/substrate-skills.ts"),
        ]);
    }

    [Fact]
    public void SubstrateWithOneProjectorFileMissingIsEntryMissing()
    {
        using var fixture = new PluginFixtureDirectory();
        fixture.WriteFile("plugins/agent-substrate/.git", "gitdir: x\n");
        fixture.WriteFile("plugins/agent-substrate/packages/projector/omp/substrate-status.ts");

        var resolved = OmpPluginResolver.Resolve(fixture.PluginsRoot, Plugin("substrate"));

        resolved.Health.ShouldBe(PluginHealth.EntryMissing);
        resolved.Message.ShouldContain("substrate-skills.ts");
        resolved.EntryPaths.ShouldBeEmpty();
    }

    [Fact]
    public void FileEntryThatLeavesThePluginDirectoryIsManifestInvalid()
    {
        using var fixture = new PluginFixtureDirectory();
        fixture.WriteFile("plugins/pack/a/ok.ts");
        fixture.WriteFile("plugins/pack/secret.ts");
        var descriptor = new OmpPluginDescriptor
        {
            Id = "custom",
            DisplayName = "Custom",
            Role = OmpPluginRole.SessionSubstrate,
            RelativeDirectory = "pack/a",
            EntryRelativePaths = ["ok.ts", "../secret.ts"],
        };

        var resolved = OmpPluginResolver.Resolve(fixture.PluginsRoot, descriptor);

        resolved.Health.ShouldBe(PluginHealth.ManifestInvalid);
        resolved.EntryPaths.ShouldBeEmpty();
    }

    [Fact]
    public void MissingTrackedPluginIsEntryMissingWithItsOwnFixCommand()
    {
        using var fixture = new PluginFixtureDirectory();
        Directory.CreateDirectory(fixture.PluginsRoot);

        var resolved = OmpPluginResolver.Resolve(fixture.PluginsRoot, Plugin("gsd"));

        resolved.Health.ShouldBe(PluginHealth.EntryMissing);
        resolved.Message.ShouldContain("git restore plugins/gsd-core-omp");
        resolved.Message.ShouldNotContain("submodule");
    }

    [Fact]
    public void ResolvingEveryBuiltInPluginOfACompleteTreeIsReadyAndChangesNothing()
    {
        using var fixture = new PluginFixtureDirectory();
        fixture.CreateAllPlugins();
        var before = Directory.EnumerateFileSystemEntries(fixture.Path, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToList();

        var results = OmpPluginCatalog.BuiltIn.Select(plugin => OmpPluginResolver.Resolve(fixture.PluginsRoot, plugin)).ToList();

        results.ShouldAllBe(result => result.Health == PluginHealth.Ready);
        Directory.EnumerateFileSystemEntries(fixture.Path, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToList().ShouldBe(before);
    }

    [Fact]
    public void ThrowIfSubmoduleNotInitializedThrowsOnlyForThatHealth()
    {
        using var fixture = new PluginFixtureDirectory();
        Directory.CreateDirectory(fixture.PluginsRoot);
        var missing = OmpPluginResolver.Resolve(fixture.PluginsRoot, Plugin("swarm"));
        var disabled = ResolvedPlugin.CreateDisabled(fixture.PluginsRoot, Plugin("swarm"));

        var thrown = Should.Throw<SubmoduleNotInitializedException>(missing.ThrowIfSubmoduleNotInitialized);
        thrown.Message.ShouldBe(missing.Message);
        Should.NotThrow(disabled.ThrowIfSubmoduleNotInitialized);
    }

    [Fact]
    public void DisabledResultKeepsThePathMathAndLoadsNothing()
    {
        using var fixture = new PluginFixtureDirectory();

        var disabled = ResolvedPlugin.CreateDisabled(fixture.PluginsRoot, Plugin("swarm"));

        disabled.Health.ShouldBe(PluginHealth.Disabled);
        disabled.EntryPaths.ShouldBeEmpty();
        disabled.AbsoluteDirectory.ShouldBe(fixture.PathOf("plugins/agent-swarm/omp"));
        Directory.Exists(fixture.PluginsRoot).ShouldBeFalse();
    }
}
