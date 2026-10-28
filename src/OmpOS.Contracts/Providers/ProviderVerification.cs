using System.Text.Json.Serialization;

namespace OmpOS.Contracts.Providers;

/// <summary>How far the research verified a catalogue provider.</summary>
public enum ProviderVerification
{
    /// <summary>A vendor page that shows the base URL and the auth scheme was opened.</summary>
    [JsonStringEnumMemberName("verified")]
    Verified,

    /// <summary>Some fact comes from a search excerpt, a third-party page or an inference.</summary>
    [JsonStringEnumMemberName("partial")]
    Partial,

    /// <summary>No usable vendor page was found: the user must verify the provider before use.</summary>
    [JsonStringEnumMemberName("unverified")]
    Unverified,
}
