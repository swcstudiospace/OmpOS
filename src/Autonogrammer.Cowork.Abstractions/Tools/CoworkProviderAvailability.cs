namespace Autonogrammer.Cowork.Abstractions;

/// <summary>Whether a provider can serve the session.</summary>
public enum CoworkProviderAvailability
{
    /// <summary>The provider can serve calls.</summary>
    Available = 0,

    /// <summary>The provider needs a one-time setup before it can serve calls.</summary>
    NeedsSetup = 1,

    /// <summary>The provider cannot serve calls.</summary>
    Unavailable = 2,
}
