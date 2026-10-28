using Autonogrammer.Presentation.Settings;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Settings;

/// <summary>The Plugins page: the four plugins as a read-only list with no switch.</summary>
public sealed class PluginsPageTests
{
    /// <summary>The four plugins load in the order the source reports them, found and missing alike.</summary>
    [Fact]
    public async Task LoadsTheFourPluginsInOrder()
    {
        var page = new PluginsPage(new ScriptedPluginsSource());
        page.State.ShouldBe(SettingsPageState.Loading);

        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        page.State.ShouldBe(SettingsPageState.Ready);
        page.IsEmpty.ShouldBeFalse();
        page.Rows.Select(row => row.DisplayName).ToArray().ShouldBe(["Ultrathink", "Agent Swarm", "Agent Substrate", "GSD"]);
        page.Rows.Select(row => row.IsFound).ToArray().ShouldBe([true, true, false, true]);
        SettingsCopy.PluginSummary(page.Rows[2]).ShouldContain("Missing", Case.Sensitive);
        SettingsCopy.PluginSummary(page.Rows[2]).ShouldContain("submodule not initialised", Case.Sensitive);
        SettingsCopy.PluginsAlwaysOnNote.ShouldContain("every omp process Autonogrammer starts", Case.Sensitive);
    }

    /// <summary>A source that reports nothing leaves the page ready and empty.</summary>
    [Fact]
    public async Task AnEmptyListIsReadyAndEmpty()
    {
        var page = new PluginsPage(new ScriptedPluginsSource { Rows = [] });
        var announced = PropertyChangeRecorder.Record(page);

        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        page.State.ShouldBe(SettingsPageState.Ready);
        page.IsEmpty.ShouldBeTrue();
        page.Rows.ShouldBeEmpty();
        announced.ShouldAnnounce(nameof(PluginsPage.IsEmpty));
    }

    /// <summary>A load failure becomes the error state with the source's reason and no rows.</summary>
    [Fact]
    public async Task ALoadFailureBecomesTheErrorState()
    {
        var page = new PluginsPage(new ScriptedPluginsSource { LoadFailure = "plugin catalogue is unreadable" });

        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        page.State.ShouldBe(SettingsPageState.Error);
        page.LoadError.ShouldBe("plugin catalogue is unreadable");
        page.Rows.ShouldBeEmpty();
        page.IsEmpty.ShouldBeFalse();
    }

    /// <summary>Any availability text other than Found counts as missing.</summary>
    [Fact]
    public void UnknownAvailabilityCountsAsMissing()
    {
        new PluginRow("x", "X", "role", null, "Broken", null).IsFound.ShouldBeFalse();
        new PluginRow("x", "X", "role", null, PluginRow.FoundAvailability, null).IsFound.ShouldBeTrue();
    }

    /// <summary>There is deliberately no switch (OQ-80): the page is never dirty, saving writes nothing and its source has no write.</summary>
    [Fact]
    public async Task ThereIsNoSwitch()
    {
        var token = TestContext.Current.CancellationToken;
        var page = new PluginsPage(new ScriptedPluginsSource());
        await page.LoadAsync(token).ConfigureAwait(true);

        page.Reset();
        page.Discard();

        page.IsDirty.ShouldBeFalse();
        (await page.SaveAsync(token).ConfigureAwait(true)).ShouldBeTrue();
        page.Rows.Count.ShouldBe(4);
        typeof(IPluginsSettingsSource).GetMethods().Select(method => method.Name).ToArray().ShouldBe(["LoadAsync"]);
        typeof(PluginsPage).GetProperties().Select(property => property.Name)
            .ShouldNotContain(name => name.Contains("Enable", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>A page without a source is refused.</summary>
    [Fact]
    public void ConstructionRefusesAMissingSource() =>
        Should.Throw<ArgumentNullException>(() => new PluginsPage(null!));
}
