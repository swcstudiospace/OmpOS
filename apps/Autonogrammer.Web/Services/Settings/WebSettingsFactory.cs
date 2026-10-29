using Autonogrammer.Presentation.Settings;
using Microsoft.JSInterop;

namespace Autonogrammer.Web.Services.Settings;

/// <summary>
/// Builds the <see cref="SettingsWindowModel"/> of the web head. It supplies the pages whose data the browser or the API can serve: General
/// (theme and motion in <c>localStorage</c>), Tab (reports it is a desktop feature), Plugins (the workspace plugin check), Network (the API
/// readiness check) and Keyboard (the fixed web shortcuts). Every other page (Runtime, Agents, Run mode, Models, Rules, Skills, Commands, Hooks, MCP,
/// Workspaces, Remote environments, Cloud agents, Privacy) is registered by <see cref="WebSettingsPages"/> and passed in as <c>extraPages</c>; an
/// extra page replaces the built-in page of its section. The pages are ordered by <see cref="WebSettingsOrder"/>.
/// </summary>
internal static class WebSettingsFactory
{
    /// <summary>Builds the settings window model.</summary>
    /// <param name="api">The API client.</param>
    /// <param name="js">The JavaScript runtime (the theme module).</param>
    /// <param name="apiHost">The host of the API, for the network page.</param>
    /// <param name="time">The clock.</param>
    /// <param name="extraPages">Pages registered by other services.</param>
    /// <returns>The model, with its pages ordered by section.</returns>
    internal static SettingsWindowModel Create(
        IOmpApiClient api,
        IJSRuntime js,
        string apiHost,
        TimeProvider time,
        IEnumerable<ISettingsPage> extraPages)
    {
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(js);
        ArgumentNullException.ThrowIfNull(extraPages);
        var pages = new Dictionary<SettingsSection, ISettingsPage>
        {
            [SettingsSection.General] = new GeneralPage(new WebGeneralSettingsSource(js)),
            [SettingsSection.Tab] = new TabPage(new WebTabSettingsSource()),
            [SettingsSection.Plugins] = new PluginsPage(new WebPluginsSettingsSource(api)),
            [SettingsSection.Network] = new NetworkSettingsPage(new WebNetworkDiagnosticsSource(api, apiHost, time)),
            [SettingsSection.Keyboard] = new KeyboardSettingsPage(new WebKeyboardSettingsSource()),
        };
        foreach (var page in extraPages)
        {
            pages[page.Section] = page;
        }

        return new SettingsWindowModel([.. pages.OrderBy(static pair => WebSettingsOrder.KeyOf(pair.Key)).Select(static pair => pair.Value)]);
    }
}
