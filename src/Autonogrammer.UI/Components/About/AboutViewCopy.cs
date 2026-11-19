namespace Autonogrammer.UI.Components.About;

/// <summary>The texts of the About view that the shared <c>AboutCopy</c> of the presentation layer does not hold, in one place.</summary>
public static class AboutViewCopy
{
    /// <summary>The label of the version row.</summary>
    public const string VersionLabel = "Version";

    /// <summary>The label of the revision row.</summary>
    public const string RevisionLabel = "Revision";

    /// <summary>The label of the licence row.</summary>
    public const string LicenceLabel = "Licence";

    /// <summary>The heading of the licence section.</summary>
    public const string LicenceHeading = "Licence and source code";

    /// <summary>The visible label of the source link.</summary>
    public const string SourceLinkLabel = "Source code of this version";

    /// <summary>The visible label of the licence text link.</summary>
    public const string LicenceLinkLabel = "Read the licence";

    /// <summary>The heading of the accessibility section.</summary>
    public const string AccessibilityHeading = "Accessibility";

    /// <summary>The first accessibility paragraph: the target.</summary>
    public const string AccessibilityTarget =
        "Autonogrammer aims to meet the Web Content Accessibility Guidelines (WCAG) 2.2 at level AA. Every screen is meant to work with a keyboard, "
        + "a screen reader, text zoom up to 400 percent, reduced motion and forced-colours modes.";

    /// <summary>The sentence before the issue link.</summary>
    public const string AccessibilityReport = "If something does not work for you, tell us. Say which page you were on, which browser and assistive technology you use, and what happened.";

    /// <summary>The visible label of the issue link.</summary>
    public const string IssueLinkLabel = "Report a problem on GitHub";

    /// <summary>The words added to the accessible name of a link that opens in a new tab.</summary>
    public const string NewTab = "(opens in a new tab)";
}
