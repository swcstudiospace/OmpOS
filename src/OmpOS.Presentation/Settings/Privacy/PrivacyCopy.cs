namespace OmpOS.Presentation.Settings.Privacy;

/// <summary>
/// Every text of the Privacy page, in one place so the view carries no literal. The page is read-only. Each statement says what OmpOS itself does
/// or does not do; it never promises anything about what a provider does with the data it receives.
/// </summary>
public static class PrivacyCopy
{
    /// <summary>The navigation label of the Privacy page.</summary>
    public const string Title = "Privacy";

    /// <summary>The heading of the Privacy page.</summary>
    public const string Heading = "Privacy";

    /// <summary>The sentence under the heading. It says that the page only describes behaviour and has nothing to switch.</summary>
    public const string Description = "What OmpOS does with your code and prompts. This page only describes it: there is nothing to switch on or off.";

    /// <summary>The heading of the statement about where data goes.</summary>
    public const string LeavesHeading = "What leaves your machine";

    /// <summary>The statement about where data goes. It names the provider the user chose and says OmpOS has no server of its own that receives the data.</summary>
    public const string LeavesText =
        "The agent engine sends the code context and prompts of a turn to the provider you selected, and to no one else. OmpOS has no server of its own that receives your code or prompts. What a provider does with what it receives is set by that provider.";

    /// <summary>The heading of the statement about ignore files.</summary>
    public const string IgnoreHeading = "Ignore files are honoured";

    /// <summary>The statement about ignore files. It says what OmpOS itself offers and attaches, and that the engine's own file tools follow the engine's rules.</summary>
    public const string IgnoreText =
        "Files matched by the global ignore list, .cursorignore, .ompos/ignore and .gitignore are not offered as @ mentions and are not attached to a prompt through a mention. The engine's own file tools follow the engine's rules, not these lists. See the Ignore page.";

    /// <summary>The heading of the statement about telemetry.</summary>
    public const string TelemetryHeading = "Telemetry is off";

    /// <summary>The statement about telemetry.</summary>
    public const string TelemetryText =
        "The OmpOS desktop app has no telemetry and does not upload usage data. Network requests it makes are for features you use, such as the Network check or a provider's model list.";

    /// <summary>The heading of the statement about remote and cloud work.</summary>
    public const string RemoteHeading = "Cloud agents and remote environments";

    /// <summary>The statement about remote and cloud work.</summary>
    public const string RemoteText =
        "When you use a cloud agent or a remote environment, your code runs on the host you chose. See the Cloud and Remote pages.";

    /// <summary>The heading of the statement about Privacy Mode.</summary>
    public const string PrivacyModeHeading = "No Privacy Mode to switch";

    /// <summary>The statement about Privacy Mode. It states what OmpOS does not have, not what a provider does.</summary>
    public const string PrivacyModeText =
        "OmpOS has no accounts, no hosted inference and does not train on your data, so it has no Privacy Mode to turn on. The choice of provider decides who sees your prompts.";

    /// <summary>Gets the statements in the order the page draws them.</summary>
    public static IReadOnlyList<PrivacyRow> Rows { get; } =
    [
        new(LeavesHeading, LeavesText),
        new(IgnoreHeading, IgnoreText),
        new(TelemetryHeading, TelemetryText),
        new(RemoteHeading, RemoteText),
        new(PrivacyModeHeading, PrivacyModeText),
    ];
}
