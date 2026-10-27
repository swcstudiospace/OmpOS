namespace OmpOS.Presentation.Settings.Models;

/// <summary>The part of the world a provider listing belongs to; the Models page groups and filters by it.</summary>
public enum ProviderListingRegion
{
    /// <summary>A European provider (the listing's country says which one).</summary>
    Europe = 0,

    /// <summary>A provider of the United States or Canada.</summary>
    UnitedStates = 1,

    /// <summary>A Chinese provider.</summary>
    China = 2,

    /// <summary>A vendor-neutral or multi-region cloud gateway.</summary>
    Global = 3,

    /// <summary>A runtime on the user's own machine.</summary>
    Local = 4,
}
