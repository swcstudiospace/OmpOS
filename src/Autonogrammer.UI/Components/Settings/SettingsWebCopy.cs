using Autonogrammer.Contracts.WorkspaceSettings;

namespace Autonogrammer.UI.Components.Settings;

/// <summary>
/// The sentences of the web Settings dialog that the shared presentation copy (<c>SettingsCopy</c>, <c>ModelsCopy</c>, <c>NetworkCopy</c>,
/// <c>KeyboardCopy</c>, <c>CustomizeCopy</c>) does not supply: the dialog chrome and the states that only the web head has.
/// </summary>
public static class SettingsWebCopy
{
    /// <summary>The label of the dialog's close button.</summary>
    public const string Close = "Close";

    /// <summary>The accessible name of the dialog's close button.</summary>
    public const string CloseName = "Close settings";

    /// <summary>The accessible name of the page region.</summary>
    public const string PageRegionName = "Settings page";

    /// <summary>The line shown after a successful save.</summary>
    public const string Saved = "Saved.";

    /// <summary>The line shown while a save runs.</summary>
    public const string Saving = "Saving.";

    /// <summary>The line shown while entries are invalid and block the save.</summary>
    public const string FixEntries = "Some entries need fixing before they can be saved.";

    /// <summary>The help text of the theme setting in the web head: the choice previews at once.</summary>
    public const string ThemeHelp = "Chooses the colours Autonogrammer draws in. The choice applies at once in this browser.";

    /// <summary>The help text of the motion setting in the web head.</summary>
    public const string MotionHelp = "Controls the transitions and animations Autonogrammer plays. The choice applies at once in this browser.";

    /// <summary>The heading of the Tab page in the web head.</summary>
    public const string TabUnavailableHeading = "Inline completion is not available in the web head";

    /// <summary>The body of the Tab page in the web head.</summary>
    public const string TabUnavailableBody = "Inline completion asks a completion endpoint on your own machine, which a web page cannot reach. Use the desktop head to see and configure it.";

    /// <summary>The note shown on a page whose view the web head does not have.</summary>
    public const string GenericUnavailable = "The web head has no view for this page yet.";

    /// <summary>The label of the state line of the generic page.</summary>
    public const string GenericState = "State";

    /// <summary>The heading of the list of configured providers.</summary>
    public const string ProvidersHeading = "Your providers";

    /// <summary>The heading of the provider directory.</summary>
    public const string DirectoryHeading = "Provider directory";

    /// <summary>The label of the search box of the provider directory.</summary>
    public const string DirectorySearchLabel = "Search the provider directory";

    /// <summary>The label of the link to the page that states a coding plan's rule.</summary>
    public const string PlanRuleSource = "Plan rule source";

    /// <summary>The label of the custom provider form's Cancel button.</summary>
    public const string CancelProvider = "Cancel";

    /// <summary>The accessible name of the custom provider form's Cancel button.</summary>
    public const string CancelProviderName = "Cancel the custom provider form";

    /// <summary>The label of the link to a vendor page that opens in a new tab.</summary>
    public const string OpensInNewTab = "opens in a new tab";

    /// <summary>The bold label of the notice that says a setting lives in the desktop app.</summary>
    public const string DesktopOnlyLabel = "Configure in the desktop app.";

    /// <summary>The sentence under the heading of the web Runtime page.</summary>
    public const string RuntimeDescription = "The values this service runs with: engine adapter, stores, run lease, request limits and the workspace switches.";

    /// <summary>The note of the web Runtime page that says it cannot be edited.</summary>
    public const string RuntimeReadOnly = "These values are read-only. The operator of the service sets them in its configuration; idle stop and retention belong to the desktop app.";

    /// <summary>The navigation label and heading of the Hooks page.</summary>
    public const string HooksTitle = "Hooks";

    /// <summary>The sentence under the heading of the Hooks page.</summary>
    public const string HooksDescription = "Scripts the workspace asks omp to run at fixed points of a session, such as before a tool call or after a file edit.";

    /// <summary>The note of the Hooks page that says it only lists.</summary>
    public const string HooksReadOnly = "Read-only. This page lists the hook files found in the workspace and what they define; it does not run or change a hook. Review and consent are decisions of the desktop app.";

    /// <summary>The line shown when no hook is defined.</summary>
    public const string HooksEmpty = "The workspace defines no hooks.";

    /// <summary>The reason shown when the service cannot read hooks and gives none.</summary>
    public const string HooksUnavailable = "The service has no workspace to read hooks from.";

    /// <summary>The label of a hook's matcher.</summary>
    public const string HookMatcherLabel = "Matches";

    /// <summary>The chip of an enabled hook.</summary>
    public const string HookEnabled = "Enabled";

    /// <summary>The chip of a disabled hook.</summary>
    public const string HookDisabled = "Disabled";

    /// <summary>The heading of the list of hook files.</summary>
    public const string HookFilesHeading = "Hook files";

    /// <summary>The line shown when no hook file was found.</summary>
    public const string HookFilesEmpty = "No hook file was found.";

    /// <summary>The heading of a list of problems found in files.</summary>
    public const string ProblemsHeading = "Problems found";

    /// <summary>The navigation label and heading of the MCP page.</summary>
    public const string McpTitle = "MCP";

    /// <summary>The sentence under the heading of the MCP page.</summary>
    public const string McpDescription = "The configuration files that connect external tools to the agent through the Model Context Protocol.";

    /// <summary>The note of the MCP page that says it only lists files.</summary>
    public const string McpReadOnly = "Read-only. This page lists the MCP configuration files found in the workspace and the problems found with them; the servers a file declares are started by the engine host, not by this page.";

    /// <summary>The line shown when no MCP file was found.</summary>
    public const string McpEmpty = "The workspace holds no MCP configuration file.";

    /// <summary>The reason shown when the service cannot read conventions and gives none.</summary>
    public const string McpUnavailable = "The service has no workspace to read MCP files from.";

    /// <summary>The chip of a project-scope file.</summary>
    public const string ScopeProject = "Project";

    /// <summary>The chip of a user-scope file.</summary>
    public const string ScopeUser = "User";

    /// <summary>Builds the detail line of a hook: where it comes from and how it behaves.</summary>
    /// <param name="hook">The hook.</param>
    /// <returns>The line.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="hook"/> is null.</exception>
    public static string HookDetail(WorkspaceHookInfo hook)
    {
        ArgumentNullException.ThrowIfNull(hook);
        var parts = new List<string>
        {
            $"Type {hook.Type}",
            $"timeout {hook.TimeoutSeconds.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)} s",
            hook.Blocking ? "can block the step" : "does not block the step",
            hook.FailClosed ? "fails closed" : "fails open",
        };
        if (hook.PreConsented)
        {
            parts.Add("pre-consented by its plugin");
        }

        parts.Add("from " + hook.SourceFile);
        return string.Join(", ", parts) + ".";
    }

    /// <summary>Builds the detail line of a hook file.</summary>
    /// <param name="source">The hook file.</param>
    /// <returns>The line.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    public static string HookSourceLine(WorkspaceHookSourceInfo source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return $"{source.Scope} scope, {source.Format} format, priority {source.Priority}.";
    }

    /// <summary>Builds the line of a problem found in a hook file.</summary>
    /// <param name="problem">The problem.</param>
    /// <returns>The line.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="problem"/> is null.</exception>
    public static string HookProblemLine(WorkspaceHookDiagnosticInfo problem)
    {
        ArgumentNullException.ThrowIfNull(problem);
        return $"{problem.Severity}: {problem.File} ({problem.Location}): {problem.Message}";
    }

    /// <summary>Builds the detail line of an MCP file.</summary>
    /// <param name="file">The file.</param>
    /// <returns>The line.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="file"/> is null.</exception>
    public static string McpFileLine(ConventionConfigFileInfo file)
    {
        ArgumentNullException.ThrowIfNull(file);
        return file.SourceDirectory.Length == 0 ? "At the workspace root." : $"In the {file.SourceDirectory} folder.";
    }

    /// <summary>Builds the line of a problem found with a configuration file.</summary>
    /// <param name="problem">The problem.</param>
    /// <returns>The line.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="problem"/> is null.</exception>
    public static string McpProblemLine(ConventionDiagnosticInfo problem)
    {
        ArgumentNullException.ThrowIfNull(problem);
        return $"{problem.Origin}: {problem.Reason}";
    }

    /// <summary>Builds the accessible name of a nav entry's unsaved dot.</summary>
    /// <param name="title">The page title.</param>
    /// <returns>The words that say the page has unsaved edits.</returns>
    public static string UnsavedDotName(string title) => $"{title}, unsaved changes";

    /// <summary>Builds the accessible name of a link that opens a vendor page in a new tab.</summary>
    /// <param name="label">The visible label of the link.</param>
    /// <param name="subject">What the link is about, usually the provider name.</param>
    /// <returns>The name; it contains the visible label.</returns>
    public static string ExternalLinkName(string label, string subject) => $"{label}, {subject}, {OpensInNewTab}";
}
