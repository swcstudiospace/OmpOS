namespace OmpOS.Harness.Providers;

/// <summary>One purchasable tier of a coding plan, worded as the research reports it.</summary>
/// <param name="Name">The tier name as the vendor publishes it.</param>
/// <param name="PriceText">The price as published, or exactly <c>price unverified</c> when the research could not verify it.</param>
/// <param name="PriceVerified">True when <paramref name="PriceText"/> was read on a primary vendor page.</param>
/// <param name="QuotaText">The published usage limits as text, or null when the vendor publishes none.</param>
public sealed record CataloguePlanTier(string Name, string PriceText, bool PriceVerified, string? QuotaText = null)
{
    /// <summary>The text shown for a price the research did not verify.</summary>
    public const string UnverifiedPrice = "price unverified";
}
