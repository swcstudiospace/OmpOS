using Autonogrammer.Presentation.Commands;
using Autonogrammer.Presentation.Settings;
using Autonogrammer.Presentation.Settings.Customize;
using Autonogrammer.Presentation.Settings.Models;
using Autonogrammer.UI.Components.Settings;

namespace Autonogrammer.Web.Services.Settings;

/// <summary>
/// Registers the Settings pages of the web head that need the API: Runtime, Models, Rules, Skills, Commands, Hooks, MCP and Plugins, with the
/// read-only information pages (Agents, Run mode, Workspaces, Remote environments, Cloud agents, Privacy). The pages are registered as
/// <see cref="ISettingsPage"/> and reach the Settings window through <see cref="WebSettingsFactory"/>, which orders them with
/// <see cref="WebSettingsOrder"/>. Every page is built per scope, so one browser tab never shares a page model with another.
/// </summary>
internal static class WebSettingsPages
{
    /// <summary>Registers the clients, sources and pages.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddWebSettingsPages(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped(static sp => new WorkspaceSettingsClient(sp.GetRequiredService<ApiHttp>().Client));
        services.AddScoped<IWorkspaceSettingsReader>(static sp => new WorkspaceSettingsReader(sp.GetRequiredService<WorkspaceSettingsClient>()));
        services.AddScoped(static sp => new ApiModelsSettingsSource(sp.GetRequiredService<ApiHttp>().Client, sp.GetRequiredService<WebWorkbenchOptions>().IsDemo));
        services.AddScoped<ISettingsPage>(static sp => new RuntimeValuesPage(sp.GetRequiredService<IWorkspaceSettingsReader>()));
        services.AddScoped<ISettingsPage>(static _ => WebInfoPages.Agents());
        services.AddScoped<ISettingsPage>(static _ => WebInfoPages.RunMode());
        services.AddScoped<ISettingsPage>(static sp => new ModelsSettingsPage(sp.GetRequiredService<ApiModelsSettingsSource>()));
        services.AddScoped<ISettingsPage>(static sp => new RulesSettingsPage(new WorkspaceRulesSettingsSource(sp.GetRequiredService<WorkspaceSettingsClient>())));
        services.AddScoped<ISettingsPage>(static sp => new SkillsSettingsPage(new WorkspaceSkillsSettingsSource(sp.GetRequiredService<WorkspaceSettingsClient>())));
        services.AddScoped<ISettingsPage>(static sp => new CommandsSettingsPage(new SlashCommandsSettingsSource(
            SlashCommandCatalogue.Shipped,
            new WorkspaceSlashDiscoverySource(sp.GetRequiredService<WorkspaceSettingsClient>()))));
        services.AddScoped<ISettingsPage>(static sp => new HooksListPage(sp.GetRequiredService<IWorkspaceSettingsReader>()));
        services.AddScoped<ISettingsPage>(static sp => new McpFilesPage(sp.GetRequiredService<IWorkspaceSettingsReader>()));
        services.AddScoped<ISettingsPage>(static sp => new PluginsPage(new WorkspacePluginsSettingsSource(sp.GetRequiredService<WorkspaceSettingsClient>())));
        services.AddScoped<ISettingsPage>(static _ => WebInfoPages.Workspaces());
        services.AddScoped<ISettingsPage>(static _ => WebInfoPages.Remote());
        services.AddScoped<ISettingsPage>(static _ => WebInfoPages.Cloud());
        services.AddScoped<ISettingsPage>(static sp =>
        {
            var options = sp.GetRequiredService<WebWorkbenchOptions>();
            return WebInfoPages.Privacy(options.IsDemo, options.ApiHost);
        });
        return services;
    }
}
