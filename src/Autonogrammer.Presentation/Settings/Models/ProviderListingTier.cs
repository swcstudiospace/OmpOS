namespace Autonogrammer.Presentation.Settings.Models;

/// <summary>One purchasable tier of a listed coding plan, worded as the research reports it.</summary>
/// <param name="Name">The tier name.</param>
/// <param name="PriceText">The price as published, or <c>price unverified</c>.</param>
/// <param name="PriceVerified">True when the price was read on a primary vendor page.</param>
/// <param name="QuotaText">The published usage limits, or null.</param>
public sealed record ProviderListingTier(string Name, string PriceText, bool PriceVerified, string? QuotaText);
