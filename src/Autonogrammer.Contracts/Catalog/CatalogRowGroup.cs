using System.Text.Json.Serialization;

namespace Autonogrammer.Contracts.Catalog;

/// <summary>The rail group of a catalog row, in display order. The members and their order mirror the desktop rail groups.</summary>
public enum CatalogRowGroup
{
    /// <summary>Sessions that wait for the user.</summary>
    [JsonStringEnumMemberName("needs-you")]
    NeedsYou,

    /// <summary>Sessions that run or are queued.</summary>
    [JsonStringEnumMemberName("running")]
    Running,

    /// <summary>Sessions that are running or awaiting tool approval but have had no activity for longer than a run lease lives. The group is assigned from staleness alone, with no engine check.</summary>
    [JsonStringEnumMemberName("lost")]
    Lost,

    /// <summary>Sessions that are ready, paused or closed.</summary>
    [JsonStringEnumMemberName("idle")]
    Idle,

    /// <summary>Sessions whose last run ended.</summary>
    [JsonStringEnumMemberName("finished")]
    Finished,
}
