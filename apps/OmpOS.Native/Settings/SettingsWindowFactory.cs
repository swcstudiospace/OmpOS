using OmpOS.Presentation.Commands;
using OmpOS.Presentation.Settings;
using OmpOS.Presentation.Settings.Customize;

namespace OmpOS.Native.Settings;

/// <summary>Builds the pages of the Settings window in navigation order and the window model over them.</summary>
internal static class SettingsWindowFactory
{
    /// <summary>Creates the pages: Runtime, General, Agents, Models (when the options supply it), Tab, Rules, Skills, Commands, Plugins, Network and Keyboard.</summary>
    /// <param name="options">Where the values live.</param>
    /// <returns>The pages, unloaded.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    public static IReadOnlyList<ISettingsPage> CreatePages(SettingsWindowOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var runtime = new RuntimeSettingsBinding(options.Store, options.ApplyRuntime ?? (static _ => { }));
        if (options.RuntimeSaveFailed is { } failed)
        {
            runtime.Failed += failed;
        }

        var tab = options.GetVariable is { } getVariable ? new TabSettingsSource(getVariable) : new TabSettingsSource();
        var pages = new List<ISettingsPage>
        {
            runtime.CreatePage(),
            new GeneralPage(new GeneralSettingsSource(options.Store)),
            new AgentsPage(new AgentsSettingsSource(options.Store)),
        };
        if (options.ModelsPageFactory is { } models)
        {
            pages.Add(models());
        }

        pages.Add(new TabPage(tab));
        var userHome = options.UserHome ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        pages.Add(new RulesSettingsPage(new RulesSettingsSource(options.WorkspaceRoot, userHome)));
        pages.Add(new SkillsSettingsPage(new SkillsSettingsSource(options.WorkspaceRoot, userHome)));
        pages.Add(new CommandsSettingsPage(new SlashCommandsSettingsSource(
            options.Commands ?? SlashCommandCatalogue.Shipped,
            new ConventionsCommandsSource(options.WorkspaceRoot, userHome))));
        pages.Add(new PluginsPage(new PluginsSettingsSource(options.PluginsRoot)));
        pages.Add(new NetworkSettingsPage(options.NetworkSource ?? new NetworkDiagnosticsSource()));
        pages.Add(new KeyboardSettingsPage(new KeyboardSettingsSource()));
        return pages;
    }

    /// <summary>Creates the window model over a new set of pages. The first page, Runtime, is selected.</summary>
    /// <param name="options">Where the values live.</param>
    /// <returns>The model, unloaded.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    public static SettingsWindowModel CreateModel(SettingsWindowOptions options) => new(CreatePages(options));
}
