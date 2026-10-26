using System.Globalization;

namespace OmpOS.Presentation.Settings;

/// <summary>
/// Every text the settings window shows outside the model providers and customize pages, in one place so a view carries no
/// literal. Accessible names are plain sentences a screen reader can speak.
/// </summary>
public static class SettingsCopy
{
    /// <summary>The window title.</summary>
    public const string WindowTitle = "Settings";

    /// <summary>The accessible name of the left navigation list.</summary>
    public const string NavigationName = "Settings sections";

    /// <summary>The navigation label of the Runtime page.</summary>
    public const string RuntimeTitle = "Runtime";

    /// <summary>The navigation label of the General page.</summary>
    public const string GeneralTitle = "General";

    /// <summary>The navigation label of the Agents page.</summary>
    public const string AgentsTitle = "Agents";

    /// <summary>The navigation label of the Tab page.</summary>
    public const string TabTitle = "Tab";

    /// <summary>The navigation label of the Plugins page.</summary>
    public const string PluginsTitle = "Plugins";

    /// <summary>The words appended to a navigation entry's accessible name while its page has unsaved edits.</summary>
    public const string UnsavedNavigationSuffix = "unsaved changes";

    /// <summary>The label of the save button.</summary>
    public const string SaveLabel = "Save";

    /// <summary>The label of the button that returns a page to its defaults.</summary>
    public const string ResetLabel = "Reset to defaults";

    /// <summary>The label of the button that drops a page's unsaved edits.</summary>
    public const string DiscardLabel = "Discard changes";

    /// <summary>The label of the button that loads a page again after it failed to load.</summary>
    public const string RetryLabel = "Try again";

    /// <summary>The text shown while a page loads.</summary>
    public const string LoadingText = "Loading settings.";

    /// <summary>The heading shown when a page failed to load.</summary>
    public const string LoadFailedHeading = "These settings could not be loaded";

    /// <summary>The heading shown above a save failure.</summary>
    public const string SaveFailedHeading = "These settings could not be saved";

    /// <summary>The badge shown on a page that has unsaved edits.</summary>
    public const string UnsavedBadge = "Unsaved changes";

    /// <summary>The heading of the unsaved-changes prompt.</summary>
    public const string ClosePromptHeading = "Save your changes?";

    /// <summary>The body of the unsaved-changes prompt.</summary>
    public const string ClosePromptBody = "Some settings have changes that are not saved. Save them, throw them away, or keep editing.";

    /// <summary>The label of the prompt choice that saves and closes.</summary>
    public const string ClosePromptSaveLabel = "Save and close";

    /// <summary>The label of the prompt choice that discards and closes.</summary>
    public const string ClosePromptDiscardLabel = "Discard and close";

    /// <summary>The label of the prompt choice that keeps the window open.</summary>
    public const string ClosePromptKeepEditingLabel = "Keep editing";

    /// <summary>The heading of the Runtime page.</summary>
    public const string RuntimeHeading = "Runtime";

    /// <summary>The sentence under the Runtime heading.</summary>
    public const string RuntimeDescription = "How long a session may sit idle before it sleeps, and how long a completed session is kept.";

    /// <summary>The label of the idle stop entry.</summary>
    public const string IdleStopLabel = "Stop idle sessions after (hours)";

    /// <summary>The help text of the idle stop entry.</summary>
    public const string IdleStopHelp = "A session with no activity for this many whole hours sleeps. Default 24.";

    /// <summary>The label of the retention entry.</summary>
    public const string RetentionLabel = "Keep completed sessions for (days)";

    /// <summary>The help text of the retention entry.</summary>
    public const string RetentionHelp = "A completed session is removed after this many whole days. Default 7.";

    /// <summary>The heading of the General page.</summary>
    public const string GeneralHeading = "General";

    /// <summary>The label of the motion setting.</summary>
    public const string MotionLabel = "Motion";

    /// <summary>The help text of the motion setting.</summary>
    public const string MotionHelp = "Controls the transitions and animations OmpOS plays.";

    /// <summary>The label of the choice that follows the operating system.</summary>
    public const string MotionSystemLabel = "Follow system";

    /// <summary>The description of the choice that follows the operating system.</summary>
    public const string MotionSystemDescription = "Use the reduced-motion setting of the operating system.";

    /// <summary>The label of the choice that reduces motion.</summary>
    public const string MotionReducedLabel = "Reduce motion";

    /// <summary>The description of the choice that reduces motion.</summary>
    public const string MotionReducedDescription = "Remove non-essential transitions and animations.";

    /// <summary>The label of the choice that keeps all motion.</summary>
    public const string MotionFullLabel = "Full motion";

    /// <summary>The description of the choice that keeps all motion.</summary>
    public const string MotionFullDescription = "Keep every transition and animation.";

    /// <summary>The heading of the privacy statement.</summary>
    public const string PrivacyHeading = "Privacy";

    /// <summary>The privacy statement. It is text only: there is no telemetry and so no telemetry switch.</summary>
    public const string PrivacyStatement =
        "OmpOS sends nothing anywhere itself. Requests to model providers are made by omp, with the providers and credentials you configure for omp.";

    /// <summary>The heading of the Agents page.</summary>
    public const string AgentsHeading = "Agents";

    /// <summary>The label of the terminal auto-run switch.</summary>
    public const string AutoRunLabel = "Run terminal commands without asking";

    /// <summary>The help text of the terminal auto-run switch.</summary>
    public const string AutoRunHelp = "Off by default. When off, every terminal command an agent proposes waits for your approval. Changing it takes effect when you save.";

    /// <summary>The warning shown while the terminal auto-run switch is on.</summary>
    public const string AutoRunWarning = "Terminal commands an agent proposes will run as soon as it proposes them.";

    /// <summary>The note that every tool call follows omp's approval policy.</summary>
    public const string ApprovalPolicyNote = "Every tool call follows omp's approval policy. This page only changes how OmpOS runs terminal commands it proposes.";

    /// <summary>The heading of the Tab page.</summary>
    public const string TabHeading = "Tab";

    /// <summary>The status line while inline completion is on.</summary>
    public const string TabStatusOn = "Inline completion is on.";

    /// <summary>The status line while inline completion is off.</summary>
    public const string TabStatusOff = "Inline completion is off.";

    /// <summary>The label of the completion endpoint line.</summary>
    public const string TabEndpointLabel = "Endpoint";

    /// <summary>The text shown for the endpoint when none is configured.</summary>
    public const string TabEndpointNone = "No endpoint configured";

    /// <summary>The text shown for the endpoint when one is configured but its address cannot be shown safely.</summary>
    public const string TabEndpointHidden = "Configured; the address is not shown";

    /// <summary>The heading of the Plugins page.</summary>
    public const string PluginsHeading = "Plugins";

    /// <summary>The line that explains why there is no switch (OQ-80).</summary>
    public const string PluginsAlwaysOnNote =
        "These plugins are connected to every omp process OmpOS starts. There is deliberately no switch to turn one off.";

    /// <summary>The text shown when the plugin list is empty.</summary>
    public const string PluginsEmpty = "No plugins were reported.";

    /// <summary>The label shown for a plugin that was found.</summary>
    public const string PluginFoundLabel = "Found";

    /// <summary>The label shown for a plugin that is missing.</summary>
    public const string PluginMissingLabel = "Missing";

    /// <summary>The label shown for a plugin whose version is not known.</summary>
    public const string PluginVersionUnknown = "Version unknown";

    /// <summary>Builds the line that says how inline completion is configured.</summary>
    /// <param name="enabled">True when a provider is configured.</param>
    /// <param name="source">Where the configuration comes from, or empty when unknown.</param>
    /// <returns>The sentence.</returns>
    public static string TabHowConfigured(bool enabled, string? source)
    {
        var where = string.IsNullOrWhiteSpace(source) ? "outside OmpOS" : source.Trim();
        return enabled
            ? string.Create(CultureInfo.InvariantCulture, $"Configured through {where}. Change it there and restart OmpOS; this page only shows it.")
            : string.Create(CultureInfo.InvariantCulture, $"Inline completion turns on when a completion endpoint is configured through {where}. This page only shows it.");
    }

    /// <summary>Builds the one-sentence accessible summary of a plugin row.</summary>
    /// <param name="row">The plugin.</param>
    /// <returns>The sentence a screen reader speaks for the row.</returns>
    public static string PluginSummary(PluginRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        var availability = row.IsFound ? PluginFoundLabel : PluginMissingLabel;
        var version = string.IsNullOrWhiteSpace(row.Version) ? PluginVersionUnknown : row.Version.Trim();
        var detail = string.IsNullOrWhiteSpace(row.Detail) ? string.Empty : " " + row.Detail.Trim();
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{row.DisplayName}: {row.Role}. {availability}. {version}.{detail}");
    }

    /// <summary>Builds the accessible name of a page's navigation entry.</summary>
    /// <param name="title">The page title.</param>
    /// <param name="isDirty">True when the page has unsaved edits.</param>
    /// <returns>The name; it carries the unsaved state so it is not conveyed by colour alone.</returns>
    public static string NavigationEntryName(string title, bool isDirty)
    {
        ArgumentNullException.ThrowIfNull(title);
        return isDirty
            ? string.Create(CultureInfo.InvariantCulture, $"{title}, {UnsavedNavigationSuffix}")
            : title;
    }
}
