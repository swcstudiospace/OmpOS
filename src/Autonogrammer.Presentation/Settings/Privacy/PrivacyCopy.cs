namespace Autonogrammer.Presentation.Settings.Privacy;

/// <summary>
/// Every privacy text the app shows: the General page sentence and each statement of the Privacy page, in one place so a view carries no literal.
/// The Privacy page is read-only. Each statement says what Autonogrammer itself does or does not do; it never promises anything about what a provider does with the data it receives.
/// </summary>
public static class PrivacyCopy
{
    /// <summary>The navigation label of the Privacy page.</summary>
    public const string Title = "Privacy";

    /// <summary>The heading of the Privacy page, and of the privacy block on the General page.</summary>
    public const string Heading = "Privacy";

    /// <summary>The sentence under the heading. It says that the page only describes behaviour and has nothing to switch.</summary>
    public const string Description = "What Autonogrammer does with your code and prompts. This page only describes it: there is nothing to switch on or off.";

    /// <summary>The privacy sentence on the General page. Details of each request are on the Privacy page.</summary>
    public const string Summary =
        "Autonogrammer has no telemetry. Requests to model providers are made by omp, with the providers and credentials you configure for omp. Its own network requests are listed on the Privacy page.";

    /// <summary>The heading of the statement about where data goes.</summary>
    public const string LeavesHeading = "What leaves your machine";

    /// <summary>The statement about where data goes. It names the provider the user chose and the Autonogrammer API that cloud agents send to.</summary>
    public const string LeavesText =
        "The agent engine sends the code context and prompts of a turn to the provider you selected. A cloud agent sends its task and repository to the Autonogrammer API named by the AUTONOGRAMMER_API_BASE_URL variable; without that variable no cloud agent runs. What a provider does with what it receives is set by that provider.";

    /// <summary>The heading of the statement about ignore files.</summary>
    public const string IgnoreHeading = "Ignore files are honoured";

    /// <summary>The statement about ignore files. It says what Autonogrammer itself offers and attaches, and that the engine's own file tools follow the engine's rules.</summary>
    public const string IgnoreText =
        "Files matched by the global ignore list, .cursorignore, .autonogrammer/ignore and .gitignore are not offered as @ mentions and are not attached to a prompt through a mention. The engine's own file tools follow the engine's rules, not these lists. See the Ignore page.";

    /// <summary>The heading of the statement about telemetry.</summary>
    public const string TelemetryHeading = "Telemetry is off";

    /// <summary>The statement about telemetry.</summary>
    public const string TelemetryText =
        "The Autonogrammer desktop app has no telemetry and does not upload usage data. Its own network requests are for features you use, such as the Network check, sign-in and cloud agents, and one check each time it starts for a newer omp it manages, which it installs when one is found. Requests to a model provider, including its model list, are made by omp.";

    /// <summary>The heading of the statement about remote and cloud work.</summary>
    public const string RemoteHeading = "Cloud agents and remote environments";

    /// <summary>The statement about remote and cloud work.</summary>
    public const string RemoteText =
        "When you use a cloud agent or a remote environment, your code runs on the host you chose. See the Cloud and Remote pages.";

    /// <summary>The heading of the statement about Privacy Mode.</summary>
    public const string PrivacyModeHeading = "No Privacy Mode to switch";

    /// <summary>The statement about Privacy Mode. It states what Autonogrammer does not have, not what a provider does.</summary>
    public const string PrivacyModeText =
        "Autonogrammer has no hosted inference and does not train on your data, so it has no Privacy Mode to turn on. Signing in is optional. A signed-in account stores a user id, an email address and a refresh token on this machine. Signing out clears them. The choice of provider decides who sees your prompts.";

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
