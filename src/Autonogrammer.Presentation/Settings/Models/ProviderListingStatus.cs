namespace Autonogrammer.Presentation.Settings.Models;

/// <summary>How far the research verified a provider listing.</summary>
public enum ProviderListingStatus
{
    /// <summary>A vendor page that shows the base URL and the auth scheme was opened.</summary>
    Verified = 0,

    /// <summary>Some fact comes from a search excerpt, a third-party page or an inference.</summary>
    Partial = 1,

    /// <summary>No usable vendor page was found: the card says to verify before use.</summary>
    Unverified = 2,
}
