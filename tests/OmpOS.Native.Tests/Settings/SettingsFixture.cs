using Avalonia.Controls;
using Avalonia.Threading;
using OmpOS.Native.Views.Settings;
using OmpOS.Presentation.Settings;
using OmpOS.Presentation.Settings.Customize;
using OmpOS.Presentation.Settings.Models;

namespace OmpOS.Native.Tests.Settings;

/// <summary>
/// A real <see cref="SettingsWindow"/> over real page models whose sources are scripted. Only the sources are fake: the nine pages, the window
/// model and the window are the product's. The window is shown, and every page is loaded unless the test asks to keep them loading.
/// </summary>
internal sealed class SettingsFixture : IDisposable
{
    private SettingsFixture()
    {
        Runtime = new RuntimePage(new RuntimeSettingsModel());
        Models = new ModelsSettingsPage(ModelsSource);
        General = new GeneralPage(GeneralSource);
        Agents = new AgentsPage(AgentsSource);
        Tab = new TabPage(TabSource);
        Plugins = new PluginsPage(PluginsSource);
        Rules = new RulesSettingsPage(RulesSource);
        Skills = new SkillsSettingsPage(SkillsSource);
        Commands = new CommandsSettingsPage(CommandsSource);
        Model = new SettingsWindowModel([Runtime, Models, General, Agents, Tab, Plugins, Rules, Skills, Commands]);
        Window = new SettingsWindow(Model);
        Runtime.Saved += (_, args) => RuntimeSaved.Add(args.Values);
    }

    /// <summary>Gets the source of the General page.</summary>
    internal FakeGeneralSource GeneralSource { get; } = new();

    /// <summary>Gets the source of the Agents page.</summary>
    internal FakeAgentsSource AgentsSource { get; } = new();

    /// <summary>Gets the source of the Tab page.</summary>
    internal FakeTabSource TabSource { get; } = new();

    /// <summary>Gets the source of the Plugins page.</summary>
    internal FakePluginsSource PluginsSource { get; } = new();

    /// <summary>Gets the source of the Rules page.</summary>
    internal FakeCatalogSource RulesSource { get; } = new();

    /// <summary>Gets the source of the Skills page.</summary>
    internal FakeCatalogSource SkillsSource { get; } = new();

    /// <summary>Gets the source of the Commands page.</summary>
    internal FakeCatalogSource CommandsSource { get; } = new();

    /// <summary>Gets the source of the Models page.</summary>
    internal FakeModelsSource ModelsSource { get; } = new();

    /// <summary>Gets the values the Runtime page announced as saved, in order.</summary>
    internal List<RuntimeSettingsValues> RuntimeSaved { get; } = [];

    /// <summary>Gets the Runtime page.</summary>
    internal RuntimePage Runtime { get; }

    /// <summary>Gets the Models page.</summary>
    internal ModelsSettingsPage Models { get; }

    /// <summary>Gets the General page.</summary>
    internal GeneralPage General { get; }

    /// <summary>Gets the Agents page.</summary>
    internal AgentsPage Agents { get; }

    /// <summary>Gets the Tab page.</summary>
    internal TabPage Tab { get; }

    /// <summary>Gets the Plugins page.</summary>
    internal PluginsPage Plugins { get; }

    /// <summary>Gets the Rules page.</summary>
    internal RulesSettingsPage Rules { get; }

    /// <summary>Gets the Skills page.</summary>
    internal SkillsSettingsPage Skills { get; }

    /// <summary>Gets the Commands page.</summary>
    internal CommandsSettingsPage Commands { get; }

    /// <summary>Gets the window model.</summary>
    internal SettingsWindowModel Model { get; }

    /// <summary>Gets the window.</summary>
    internal SettingsWindow Window { get; }

    /// <summary>
    /// Builds the fixture on the dispatcher thread of a headless session. The sources are configured first, so a test can script a failure or
    /// a gate before anything loads.
    /// </summary>
    /// <param name="cancellationToken">Cancels the loads.</param>
    /// <param name="configure">Scripts the sources. Null keeps the defaults.</param>
    /// <param name="load">True to load every page before the window is shown; false to leave the pages loading.</param>
    /// <returns>The fixture. The caller disposes it.</returns>
    internal static async Task<SettingsFixture> OpenAsync(CancellationToken cancellationToken, Action<SettingsFixture>? configure = null, bool load = true)
    {
        var fixture = new SettingsFixture();
        configure?.Invoke(fixture);
        if (load)
        {
            await fixture.Model.LoadAllAsync(cancellationToken).ConfigureAwait(true);
        }

        fixture.Window.Show();
        fixture.Settle();
        return fixture;
    }

    /// <summary>Lets the dispatcher and layout settle after a change.</summary>
    internal void Settle() => SettingsTree.SettleSafe(Window);

    /// <summary>Selects a section the way the user does and lets the window settle.</summary>
    /// <param name="section">The section to show.</param>
    /// <returns>The view of the page.</returns>
    internal Control Show(SettingsSection section)
    {
        Model.SelectSection(section);
        Settle();
        return Window.CurrentView ?? throw new InvalidOperationException("The window shows no page view.");
    }

    /// <summary>Drops every unsaved edit and closes the window.</summary>
    public void Dispose()
    {
        foreach (var page in Model.Pages)
        {
            page.Discard();
        }

        _ = Model.RequestClose();
        Dispatcher.UIThread.RunJobs();
    }
}
