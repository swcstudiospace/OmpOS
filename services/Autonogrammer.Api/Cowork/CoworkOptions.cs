namespace Autonogrammer.Api.Cowork;

/// <summary>
/// The <c>Cowork</c> configuration section. The summary routes are always on. This section only bounds how many
/// summaries one owner may store, how long a title may be, and how often one caller may call the routes.
/// </summary>
internal sealed class CoworkOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "Cowork";

    /// <summary>The default number of Cowork summaries one owner may store.</summary>
    public const int DefaultMaxSessionsPerOwner = 200;

    /// <summary>The default title length, in characters. It matches <c>CoworkSessionSummary.MaxTitleLength</c>.</summary>
    public const int DefaultMaxTitleLength = 200;

    /// <summary>The default number of summary calls one caller may make per minute.</summary>
    public const int DefaultRequestsPerMinute = 60;

    /// <summary>Gets or sets how many Cowork summaries one owner may store, 1 to 200. A later one answers 409.</summary>
    public int MaxSessionsPerOwner { get; set; } = DefaultMaxSessionsPerOwner;

    /// <summary>Gets or sets the longest stored title, in characters, 1 to 200. A longer title is cut, never rejected.</summary>
    public int MaxTitleLength { get; set; } = DefaultMaxTitleLength;

    /// <summary>Gets or sets how many summary calls one caller may make per minute, 1 to 100000.</summary>
    public int RequestsPerMinute { get; set; } = DefaultRequestsPerMinute;
}
