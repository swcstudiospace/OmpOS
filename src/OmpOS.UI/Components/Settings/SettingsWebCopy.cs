namespace OmpOS.UI.Components.Settings;

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
    public const string ThemeHelp = "Chooses the colours OmpOS draws in. The choice applies at once in this browser.";

    /// <summary>The help text of the motion setting in the web head.</summary>
    public const string MotionHelp = "Controls the transitions and animations OmpOS plays. The choice applies at once in this browser.";

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
