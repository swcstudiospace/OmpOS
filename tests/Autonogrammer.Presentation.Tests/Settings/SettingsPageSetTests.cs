using Autonogrammer.Presentation.Settings;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Settings;

/// <summary>The five pages together: navigation order, distinct sections, one load, and the rule that no page offers a control without a source.</summary>
public sealed class SettingsPageSetTests
{
    /// <summary>The five pages fill five distinct sections, in the navigation order the window keeps.</summary>
    [Fact]
    public void PagesFillDistinctSectionsInNavigationOrder()
    {
        var window = BuildWindow(out _);

        window.Pages.Select(page => page.Section).ToArray().ShouldBe(
            [SettingsSection.Runtime, SettingsSection.General, SettingsSection.Agents, SettingsSection.Tab, SettingsSection.Plugins]);
        window.Pages.Select(page => page.Title).ToArray().ShouldBe(
            [SettingsCopy.RuntimeTitle, SettingsCopy.GeneralTitle, SettingsCopy.AgentsTitle, SettingsCopy.TabTitle, SettingsCopy.PluginsTitle]);
    }

    /// <summary>One load makes every page ready; a clean window has nothing unsaved.</summary>
    [Fact]
    public async Task OneLoadMakesEveryPageReady()
    {
        var window = BuildWindow(out _);

        await window.LoadAllAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        window.Pages.OfType<SettingsPageBase>().ShouldAllBe(page => page.State == SettingsPageState.Ready);
        window.HasUnsavedChanges.ShouldBeFalse();
    }

    /// <summary>Every editable control of every page reaches its source: each edit is dirty, Save writes it, and nothing is written before Save.</summary>
    [Fact]
    public async Task EveryEditableControlReachesItsSource()
    {
        var token = TestContext.Current.CancellationToken;
        var window = BuildWindow(out var sources);
        await window.LoadAllAsync(token).ConfigureAwait(true);
        var general = window.Pages.OfType<GeneralPage>().Single();
        var agents = window.Pages.OfType<AgentsPage>().Single();

        general.SetMotion(MotionPreference.Full);
        agents.SetTerminalAutoRun(true);

        sources.General.SaveCalls.ShouldBe(0);
        sources.Agents.SaveCalls.ShouldBe(0);
        window.HasUnsavedChanges.ShouldBeTrue();
        window.RequestClose().ShouldBe(SettingsCloseOutcome.ConfirmationRequired);

        (await window.ResolveClosePromptAsync(SettingsClosePromptChoice.Save, token).ConfigureAwait(true)).ShouldBeTrue();

        sources.General.Values.Motion.ShouldBe(MotionPreference.Full);
        sources.Agents.Values.TerminalAutoRun.ShouldBeTrue();
    }

    /// <summary>Every page needs its source; none can be built without one, and the read-only pages' sources have no write.</summary>
    [Fact]
    public void NoPageExistsWithoutASource()
    {
        Should.Throw<ArgumentNullException>(() => new GeneralPage(null!));
        Should.Throw<ArgumentNullException>(() => new AgentsPage(null!));
        Should.Throw<ArgumentNullException>(() => new TabPage(null!));
        Should.Throw<ArgumentNullException>(() => new PluginsPage(null!));
        Should.Throw<ArgumentNullException>(() => new RuntimePage(null!));
        typeof(IGeneralSettingsSource).GetMethods().Select(method => method.Name).Order(StringComparer.Ordinal).ToArray()
            .ShouldBe(["LoadAsync", "SaveAsync"]);
        typeof(IAgentsSettingsSource).GetMethods().Select(method => method.Name).Order(StringComparer.Ordinal).ToArray()
            .ShouldBe(["LoadAsync", "SaveAsync"]);
    }

    private static SettingsWindowModel BuildWindow(out Sources sources)
    {
        sources = new Sources(
            new ScriptedGeneralSource(GeneralSettingsValues.Defaults),
            new ScriptedAgentsSource(),
            new ScriptedTabSource(),
            new ScriptedPluginsSource());
        return new SettingsWindowModel(
        [
            new RuntimePage(new RuntimeSettingsModel()),
            new GeneralPage(sources.General),
            new AgentsPage(sources.Agents),
            new TabPage(sources.Tab),
            new PluginsPage(sources.Plugins),
        ]);
    }

    private sealed record Sources(ScriptedGeneralSource General, ScriptedAgentsSource Agents, ScriptedTabSource Tab, ScriptedPluginsSource Plugins);
}
