using System.Text.Json.Serialization;

namespace Autonogrammer.Contracts.Catalog;

/// <summary>The result of the workspace plugin check.</summary>
public enum PluginCheckStatus
{
    /// <summary>Every expected plugin is present and healthy.</summary>
    [JsonStringEnumMemberName("ok")]
    Ok,

    /// <summary>Some plugin is missing or unhealthy; the summary says which.</summary>
    [JsonStringEnumMemberName("degraded")]
    Degraded,

    /// <summary>This API cannot run the plugin check.</summary>
    [JsonStringEnumMemberName("unavailable")]
    Unavailable,
}
