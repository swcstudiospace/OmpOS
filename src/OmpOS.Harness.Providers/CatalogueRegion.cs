using System.Text.Json.Serialization;

namespace OmpOS.Harness.Providers;

/// <summary>The part of the world a catalogue row belongs to. The region groups the Models page; it is not a residency claim.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<CatalogueRegion>))]
public enum CatalogueRegion
{
    /// <summary>A European provider. The row's country says which one; Switzerland and the United Kingdom are listed here by country.</summary>
    Eu = 0,

    /// <summary>A provider of the United States or Canada.</summary>
    Us,

    /// <summary>A Chinese provider, on its global or its mainland host.</summary>
    Cn,

    /// <summary>A vendor-neutral or multi-region cloud gateway that belongs to no single country.</summary>
    Global,

    /// <summary>A runtime on the user's own machine.</summary>
    Local,
}
