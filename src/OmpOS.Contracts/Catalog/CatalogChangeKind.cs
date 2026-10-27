using System.Text.Json.Serialization;

namespace OmpOS.Contracts.Catalog;

/// <summary>How a catalog row changed, as carried by a <c>catalog_changed</c> event.</summary>
public enum CatalogChangeKind
{
    /// <summary>The session appeared in the catalog.</summary>
    [JsonStringEnumMemberName("created")]
    Created,

    /// <summary>The session's row changed in a way other than the cases below.</summary>
    [JsonStringEnumMemberName("updated")]
    Updated,

    /// <summary>The operator closed the session.</summary>
    [JsonStringEnumMemberName("closed")]
    Closed,

    /// <summary>The operator reopened the session.</summary>
    [JsonStringEnumMemberName("reopened")]
    Reopened,
}
