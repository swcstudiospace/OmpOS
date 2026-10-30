using System.Text.Json.Serialization;

namespace Autonogrammer.Contracts.Providers;

/// <summary>The part of the world a catalogue provider belongs to. It groups the list; it is not a residency claim.</summary>
public enum ProviderRegion
{
    /// <summary>A European provider.</summary>
    [JsonStringEnumMemberName("europe")]
    Europe,

    /// <summary>A provider of the United States or Canada.</summary>
    [JsonStringEnumMemberName("united-states")]
    UnitedStates,

    /// <summary>A Chinese provider.</summary>
    [JsonStringEnumMemberName("china")]
    China,

    /// <summary>A vendor-neutral or multi-region gateway.</summary>
    [JsonStringEnumMemberName("global")]
    Global,

    /// <summary>A runtime on the machine of the user.</summary>
    [JsonStringEnumMemberName("local")]
    Local,
}
