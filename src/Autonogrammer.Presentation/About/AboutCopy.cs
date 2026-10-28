namespace Autonogrammer.Presentation.About;

/// <summary>Every text of the About window, in one place so no view carries a literal. The source offer states the AGPL-3.0 section 13 duty as ADR-0018 and <c>docs/licensing.md</c> describe it.</summary>
public static class AboutCopy
{
    /// <summary>The product name.</summary>
    public const string ProductName = "Autonogrammer";

    /// <summary>The title of the About window.</summary>
    public const string WindowTitle = "About Autonogrammer";

    /// <summary>The sentence under the product name.</summary>
    public const string Tagline = "A workspace for coding agents, built on omp.";

    /// <summary>The copyright line, the same as <c>NOTICE</c>.</summary>
    public const string Copyright = "Copyright (C) 2026 SWC Studio";

    /// <summary>The accessible name of the brand mark.</summary>
    public const string MarkName = "Autonogrammer mark";

    /// <summary>The label of the version row.</summary>
    public const string VersionLabel = "Version";

    /// <summary>The label of the revision row.</summary>
    public const string RevisionLabel = "Revision";

    /// <summary>The label of the licence row.</summary>
    public const string LicenceLabel = "Licence";

    /// <summary>The label of the source row.</summary>
    public const string SourceLabel = "Source code";

    /// <summary>The heading of the notices list.</summary>
    public const string NoticesLabel = "Notices";

    /// <summary>The heading of the links list.</summary>
    public const string LinksLabel = "Links";

    /// <summary>The accessible name of the list of links.</summary>
    public const string LinksName = "Source code, licence and notices";

    /// <summary>The label of the licence text link.</summary>
    public const string LicenceLinkLabel = "GNU Affero General Public License";

    /// <summary>The label of the source link.</summary>
    public const string SourceLinkLabel = "Source code on GitHub";

    /// <summary>The label of the button that copies the version information.</summary>
    public const string CopyLabel = "Copy version info";

    /// <summary>The label of the button that closes the window.</summary>
    public const string CloseLabel = "Close";

    /// <summary>The accessible name of the polite live region that announces the copy result.</summary>
    public const string StatusName = "Copy result";

    /// <summary>The announcement after the version information was copied.</summary>
    public const string Copied = "Version information copied.";

    /// <summary>The announcement after the version information could not be copied.</summary>
    public const string CopyFailed = "Version information could not be copied.";

    /// <summary>Builds the announcement for an address the system browser could not open.</summary>
    /// <param name="label">The visible label of the link.</param>
    /// <returns>A sentence that names the link and says where else to find it.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="label"/> is null.</exception>
    public static string OpenFailed(string label)
    {
        ArgumentNullException.ThrowIfNull(label);
        return label + " could not be opened. Use Copy version info to take the source address with you.";
    }

    /// <summary>The text of the revision when the build did not record one.</summary>
    public const string Unknown = "unknown";

    /// <summary>The file names of the notices that travel with every build, in reading order.</summary>
    public static IReadOnlyList<string> NoticeFiles { get; } = ["LICENSE", "NOTICE", "THIRD-PARTY-NOTICES.md"];

    /// <summary>
    /// The source offer. It says what the licence is, that the user may have the source of this build at no charge, and that the revision names
    /// the code. Autonogrammer makes the offer for every build, modified or not (<c>docs/licensing.md</c>).
    /// </summary>
    public const string SourceOffer =
        "Autonogrammer is free software under the GNU Affero General Public License, version 3 or, at your option, any later version. "
        + "Everyone who uses this build, on this computer or through a network, can get its complete source code at no charge from the source address below. "
        + "The revision names the exact code this build was made from.";

    /// <summary>Builds the accessible name of a control that opens an address in the browser.</summary>
    /// <param name="label">The visible label of the link.</param>
    /// <returns>The label followed by what pressing the control does.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="label"/> is null.</exception>
    public static string OpenInBrowser(string label)
    {
        ArgumentNullException.ThrowIfNull(label);
        return label + ", opens in your browser";
    }
}
