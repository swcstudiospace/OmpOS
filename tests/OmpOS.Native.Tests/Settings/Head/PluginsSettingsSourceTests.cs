using OmpOS.Native.Settings;
using OmpOS.Native.Tests.AgentLoop;
using OmpOS.Presentation.Settings;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.Settings.Head;

/// <summary>The Plugins page's rows over a plugins root.</summary>
public sealed class PluginsSettingsSourceTests
{
    /// <summary>Four rows in composition order; with every plugin in place all are Found and say which submodule they were pinned from.</summary>
    [Fact]
    public async Task EveryPluginInPlaceIsFoundInCompositionOrder()
    {
        using var tree = new TempTree();
        tree.WriteReadyPlugins();

        var values = await new PluginsSettingsSource(tree.Plugins).LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        values.Rows.Select(static row => row.Id).ShouldBe(["ultrathink", "swarm", "substrate", "gsd"]);
        values.Rows.ShouldAllBe(row => row.IsFound);
        values.Rows.ShouldAllBe(row => row.Availability == PluginRow.FoundAvailability);
        values.Rows[0].Detail.ShouldBe("Pinned submodule plugins/claude-ultrathink.");
        values.Rows[1].Detail.ShouldBe("Pinned submodule plugins/agent-swarm.");
        values.Rows[2].Detail.ShouldBe("Pinned submodule plugins/agent-substrate.");
        values.Rows[3].Detail.ShouldNotBeNull().ShouldContain("plugins/gsd-core-omp", Case.Sensitive);
        values.Rows.ShouldAllBe(row => row.Role.Length > 0);
    }

    /// <summary>A plugins root that does not exist makes every plugin Missing, with where it is expected and the command that repairs it.</summary>
    [Fact]
    public async Task MissingPluginsAreMissingWithTheirFix()
    {
        using var tree = new TempTree();

        var values = await new PluginsSettingsSource(tree.Plugins).LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        values.Rows.Count.ShouldBe(4);
        values.Rows.ShouldAllBe(row => !row.IsFound);
        values.Rows.ShouldAllBe(row => row.Availability == PluginRow.MissingAvailability);
        values.Rows[0].Detail.ShouldNotBeNull().ShouldContain("plugins/claude-ultrathink", Case.Sensitive);
        values.Rows[0].Detail.ShouldNotBeNull().ShouldContain("git submodule update --init --recursive plugins/claude-ultrathink", Case.Sensitive);
        values.Rows.ShouldAllBe(row => row.Version == null);
    }

    /// <summary>Found and Missing plugins can be mixed, and the version comes from the plugin's own package.json.</summary>
    [Fact]
    public async Task AFoundPluginReportsTheVersionOfItsOwnPackage()
    {
        using var tree = new TempTree();
        tree.WritePackagePlugin("claude-ultrathink", "src/host/omp.ts");
        tree.Write("plugins/claude-ultrathink/package.json", "{\"name\":\"claude-ultrathink\",\"version\":\"2.4.1\",\"omp\":{\"extensions\":[\"./src/host/omp.ts\"]}}");

        var values = await new PluginsSettingsSource(tree.Plugins).LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        var ultrathink = values.Rows.Single(static row => string.Equals(row.Id, "ultrathink", StringComparison.Ordinal));
        ultrathink.IsFound.ShouldBeTrue();
        ultrathink.Version.ShouldBe("2.4.1");
        values.Rows.Single(static row => string.Equals(row.Id, "swarm", StringComparison.Ordinal)).IsFound.ShouldBeFalse();
    }

    /// <summary>A package.json without a version, or one that is not JSON, gives no version and does not fail the page.</summary>
    [Fact]
    public async Task AManifestWithoutAUsableVersionGivesNone()
    {
        using var tree = new TempTree();
        tree.WritePackagePlugin("claude-ultrathink", "src/host/omp.ts");
        tree.WritePackagePlugin("gsd-core-omp", "src/index.ts");
        tree.Write("plugins/gsd-core-omp/package.json", "{ not json");

        var page = new PluginsPage(new PluginsSettingsSource(tree.Plugins));
        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        page.State.ShouldBe(SettingsPageState.Ready);
        page.Rows.Single(static row => string.Equals(row.Id, "ultrathink", StringComparison.Ordinal)).Version.ShouldBeNull();
        page.Rows.Single(static row => string.Equals(row.Id, "gsd", StringComparison.Ordinal)).Version.ShouldBeNull();
    }
}
