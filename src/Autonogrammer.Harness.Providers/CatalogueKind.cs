using System.Text.Json.Serialization;

namespace Autonogrammer.Harness.Providers;

/// <summary>What a catalogue row sells or runs.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<CatalogueKind>))]
public enum CatalogueKind
{
    /// <summary>A model API billed per use with the vendor's own key.</summary>
    Api = 0,

    /// <summary>A gateway or router that serves models of several vendors behind one key.</summary>
    Aggregator,

    /// <summary>A subscription plan with its own key or sign-in and published usage rules.</summary>
    CodingPlan,

    /// <summary>A runtime on the user's own machine.</summary>
    Local,
}
