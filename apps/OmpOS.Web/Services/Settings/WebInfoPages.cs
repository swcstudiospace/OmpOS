using OmpOS.Presentation.Settings;
using OmpOS.UI.Components.Settings;

namespace OmpOS.Web.Services.Settings;

/// <summary>
/// The read-only information pages of the web head: the desktop settings the browser has no control for. Each states what the setting is
/// for and, where it lives in the desktop app, says so; the facts on them are ones this head can vouch for (what it stores, what it asks of
/// the service), never a claim about a desktop state it cannot see.
/// </summary>
internal static class WebInfoPages
{
    private const string DesktopNotice = "This setting belongs to the desktop app. The web head has no switch for it and shows only what it is for.";

    /// <summary>Creates the Agents page.</summary>
    /// <returns>The page.</returns>
    internal static InfoSettingsPage Agents() => new(
        SettingsSection.Agents,
        SettingsCopy.AgentsTitle,
        SettingsCopy.AgentsDescription,
        [
            new InfoSettingsFact("Terminal auto-run", "The desktop app can run the terminal commands an agent proposes without asking each time. The browser has no terminal host, so the web head has no such switch and every proposal waits for you."),
            new InfoSettingsFact("Tool approvals", SettingsCopy.ApprovalPolicyNote),
        ],
        DesktopNotice);

    /// <summary>Creates the Run mode page.</summary>
    /// <returns>The page.</returns>
    internal static InfoSettingsPage RunMode() => new(
        SettingsSection.RunMode,
        "Run mode",
        "How freely an agent may run commands and tools: ask for a review of each call, allow only a list, or run everything.",
        [
            new InfoSettingsFact("Modes", "Auto-review judges each call and asks when it is unsure, Allowlist runs only what you listed, and Unrestricted runs everything without asking. The names are the ones Cursor's command line uses."),
            new InfoSettingsFact("In the browser", "A tool call that needs a decision appears in the thread with Allow and Deny buttons; the web head keeps no run mode of its own."),
        ],
        DesktopNotice);

    /// <summary>Creates the Workspaces page.</summary>
    /// <returns>The page.</returns>
    internal static InfoSettingsPage Workspaces() => new(
        SettingsSection.Workspaces,
        "Workspaces",
        "The folders an agent works in and the sets of folders you keep together.",
        [
            new InfoSettingsFact("One folder per service", "The web head works in the single workspace folder the service is configured with. The Runtime page says whether one is configured."),
            new InfoSettingsFact("Sets of folders", "Saving, opening and removing workspace sets happens in the desktop app, which has the file system the sets point to."),
        ],
        DesktopNotice);

    /// <summary>Creates the Remote environments page.</summary>
    /// <returns>The page.</returns>
    internal static InfoSettingsPage Remote() => new(
        SettingsSection.Remote,
        "Remote environments",
        "Run the agent on another machine over your own ssh, so its commands, files and tools are the remote machine's.",
        [
            new InfoSettingsFact("Why not here", "A web page cannot open an ssh connection. Remote profiles, their ssh configuration and their diagnosis are handled by the desktop app."),
            new InfoSettingsFact("What stays on your side", "The desktop app uses your own ssh client and your own keys; OmpOS stores no remote password."),
        ],
        DesktopNotice);

    /// <summary>Creates the Cloud agents page.</summary>
    /// <returns>The page.</returns>
    internal static InfoSettingsPage Cloud() => new(
        SettingsSection.Cloud,
        "Cloud agents",
        "Agents that run on a host you chose instead of on this machine.",
        [
            new InfoSettingsFact("Where they are listed", "Sessions started on a cloud host appear in the agent list like any other session when the service reports them."),
            new InfoSettingsFact("Where they are set up", "The host, its sign-in and the start of a cloud agent are configured in the desktop app; this page has nothing to switch."),
        ],
        DesktopNotice);

    /// <summary>Creates the Privacy page.</summary>
    /// <param name="isDemo">True when the head runs the in-browser Demo.</param>
    /// <param name="apiHost">The host the head talks to, for display.</param>
    /// <returns>The page.</returns>
    internal static InfoSettingsPage Privacy(bool isDemo, string apiHost)
    {
        ArgumentNullException.ThrowIfNull(apiHost);
        return new InfoSettingsPage(
            SettingsSection.Privacy,
            "Privacy",
            "What this page does with your prompts and settings. It only describes it: there is nothing to switch on or off.",
            [
                new InfoSettingsFact(
                    "Where your prompts go",
                    isDemo
                        ? "Nothing leaves this browser tab in the Demo: every answer is produced by scripted fixtures inside the page, and no request is sent to a server."
                        : $"Prompts and settings requests go to the OmpOS service at {apiHost}, which passes a turn to the provider that service is set up with. This page sends nothing anywhere else."),
                new InfoSettingsFact("What this browser keeps", "Your theme and motion choices are kept in this browser's local storage. The page keeps no provider key and no account credential."),
                new InfoSettingsFact("Telemetry", "The web head runs no analytics and loads no third-party scripts."),
            ],
            null);
    }
}
