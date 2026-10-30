using System.Text.Json.Serialization;

namespace Autonogrammer.Harness.Providers;

/// <summary>How far the research verified a catalogue row.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<CatalogueStatus>))]
public enum CatalogueStatus
{
    /// <summary>A vendor page that shows the base URL and the auth scheme was opened.</summary>
    Verified = 0,

    /// <summary>Some fact comes from a search excerpt, a third-party page or an inference.</summary>
    Partial,

    /// <summary>No usable vendor page was found: the row says so and the user must verify it before use.</summary>
    Unverified,
}
