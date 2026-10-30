namespace Autonogrammer.Presentation.Settings.Models;

/// <summary>What a provider listing sells or runs; the Models page filters by it.</summary>
public enum ProviderListingKind
{
    /// <summary>A model API billed per use with the vendor's own key.</summary>
    Api = 0,

    /// <summary>A gateway or router that serves models of several vendors behind one key.</summary>
    Aggregator = 1,

    /// <summary>A subscription coding plan with its own key or sign-in and published usage rules.</summary>
    CodingPlan = 2,

    /// <summary>A runtime on the user's own machine.</summary>
    Local = 3,
}
