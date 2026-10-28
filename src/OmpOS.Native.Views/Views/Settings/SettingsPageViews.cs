using Avalonia.Controls;
using OmpOS.Presentation.Hooks;
using OmpOS.Presentation.Safety;
using OmpOS.Presentation.Settings;
using OmpOS.Presentation.Settings.Customize;
using OmpOS.Presentation.Settings.Ignore;
using OmpOS.Presentation.Settings.Models;
using OmpOS.Presentation.Settings.Privacy;
using OmpOS.Presentation.Settings.Workspaces;
using OmpOS.Presentation.Settings.Worktrees;

namespace OmpOS.Native.Views.Settings;

/// <summary>Chooses the view of each settings page and says what each kind of page offers, so the window carries no per-page branch.</summary>
internal static class SettingsPageViews
{
    /// <summary>Creates the view of a page and sets the page as its data context. One view type draws each page type.</summary>
    /// <param name="page">The page.</param>
    /// <returns>The view.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="page"/> is null.</exception>
    /// <exception cref="ArgumentException">No view draws this kind of page.</exception>
    public static Control Create(ISettingsPage page)
    {
        ArgumentNullException.ThrowIfNull(page);

        Control view = page switch
        {
            RuntimePage => new RuntimePageView(),
            GeneralPage => new GeneralPageView(),
            DisplaySettingsPage => new DisplayPageView(),
            AgentsPage => new AgentsPageView(),
            TabPage => new TabPageView(),
            PluginsPage => new PluginsPageView(),
            NetworkSettingsPage => new NetworkPageView(),
            KeyboardSettingsPage => new KeyboardPageView(),
            ModelsSettingsPage => new ModelsPageView(),
            CustomizeCatalogPage => new CustomizeCatalogView(),
            HooksSettingsPage => new OmpOS.Native.Views.Hooks.HooksPageView(),
            RunModeSettingsPage => new OmpOS.Native.Views.Safety.RunModePageView(),
            McpSettingsPage => new OmpOS.Native.Views.Mcp.McpPageView(),
            RemoteEnvironmentsSettingsPage => new OmpOS.Native.Views.Remote.RemoteEnvironmentsPageView(),
            CloudSettingsPage => new OmpOS.Native.Views.Cloud.CloudAgentsPageView(),
            WorkspacesSettingsPage => new WorkspacesPageView(),
            WorktreesSettingsPage => new WorktreesPageView(),
            IgnoreSettingsPage => new IgnorePageView(),
            PrivacySettingsPage => new PrivacyPageView(),
            AutomationsSettingsPage => new OmpOS.Native.Views.Automations.AutomationsPageView(),
            _ => throw new ArgumentException("No view draws this kind of settings page.", nameof(page)),
        };
        view.DataContext = ContextOf(page);
        return view;
    }

    // The MCP and Cloud views draw a model the page holds; every other view draws the page.
    private static object ContextOf(ISettingsPage page) => page switch
    {
        McpSettingsPage mcp => mcp.Model,
        CloudSettingsPage cloud => cloud.Agents,
        AutomationsSettingsPage automations => automations.Model,
        _ => page,
    };

    /// <summary>Returns whether the page has something to save: its footer then shows Save and Discard. A read-only page shows neither.</summary>
    /// <param name="page">The page.</param>
    /// <returns>True for the Runtime, General, Agents, Models, Display, Run mode, Ignore and Workspaces pages.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="page"/> is null.</exception>
    public static bool IsEditable(ISettingsPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        return page is RuntimePage or GeneralPage or AgentsPage or ModelsSettingsPage or DisplaySettingsPage or RunModeSettingsPage or IgnoreSettingsPage or WorkspacesSettingsPage;
    }

    /// <summary>Returns whether the page has defaults the footer's Reset returns its draft to. The Models page has none: its draft is a form to close.</summary>
    /// <param name="page">The page.</param>
    /// <returns>True for the Runtime, General, Agents, Display, Run mode, Ignore and Workspaces pages.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="page"/> is null.</exception>
    public static bool HasDefaults(ISettingsPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        return page is RuntimePage or GeneralPage or AgentsPage or DisplaySettingsPage or RunModeSettingsPage or IgnoreSettingsPage or WorkspacesSettingsPage;
    }

    /// <summary>Returns whether the page is still loading its values.</summary>
    /// <param name="page">The page.</param>
    /// <returns>True while the page is in its loading state.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="page"/> is null.</exception>
    public static bool IsLoading(ISettingsPage page) => StatusOf(page).Kind == PageStatusKind.Loading;

    /// <summary>Returns whether the page can be edited at the moment: it loaded and is not showing a failure.</summary>
    /// <param name="page">The page.</param>
    /// <returns>True when the page shows its controls.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="page"/> is null.</exception>
    public static bool IsReady(ISettingsPage page) => StatusOf(page).Kind == PageStatusKind.Ready;

    /// <summary>Maps the state of any page to what its view shows, with the words from the copy classes.</summary>
    /// <param name="page">The page.</param>
    /// <returns>The state and its words.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="page"/> is null.</exception>
    public static PageStatus StatusOf(ISettingsPage page)
    {
        ArgumentNullException.ThrowIfNull(page);

        return page switch
        {
            ModelsSettingsPage models => models.State switch
            {
                ModelsPageState.Loading => new PageStatus(PageStatusKind.Loading, string.Empty, ModelsCopy.LoadingText),
                ModelsPageState.Error => new PageStatus(PageStatusKind.Error, ModelsCopy.ErrorTitle, models.LoadFailure ?? ModelsCopy.LoadFailed),

                // The Models page draws its own empty state, because its Add provider button and templates stay available.
                _ => ReadyStatus,
            },
            CustomizeCatalogPage catalog => catalog.State switch
            {
                CustomizePageState.Loading => new PageStatus(PageStatusKind.Loading, string.Empty, CustomizeCopy.LoadingText),
                CustomizePageState.Error => new PageStatus(PageStatusKind.Error, CustomizeCopy.ErrorTitle, catalog.LoadFailure ?? CustomizeCopy.LoadFailed),
                _ => ReadyStatus,
            },
            SettingsPageBase basePage => basePage.State switch
            {
                SettingsPageState.Loading => new PageStatus(PageStatusKind.Loading, string.Empty, SettingsCopy.LoadingText),
                SettingsPageState.Error => new PageStatus(PageStatusKind.Error, SettingsCopy.LoadFailedHeading, basePage.LoadError),
                _ => ReadyStatus,
            },
            _ => ReadyStatus,
        };
    }

    private static PageStatus ReadyStatus => new(PageStatusKind.Ready, string.Empty, string.Empty);
}
