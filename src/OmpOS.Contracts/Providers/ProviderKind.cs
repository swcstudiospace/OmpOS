using System.Text.Json.Serialization;

namespace OmpOS.Contracts.Providers;

/// <summary>What a catalogue provider sells or runs.</summary>
public enum ProviderKind
{
    /// <summary>A model API billed per use with the vendor's own key.</summary>
    [JsonStringEnumMemberName("api")]
    Api,

    /// <summary>A gateway or router that serves models of several vendors behind one key.</summary>
    [JsonStringEnumMemberName("aggregator")]
    Aggregator,

    /// <summary>A subscription plan with published usage rules.</summary>
    [JsonStringEnumMemberName("coding-plan")]
    CodingPlan,

    /// <summary>A runtime on the machine of the user.</summary>
    [JsonStringEnumMemberName("local")]
    Local,
}
