namespace Autonogrammer.Presentation.Settings.Models;

/// <summary>
/// One provider, aggregator, coding plan or local runtime the Models page offers, ready to take a key. It is catalogue data: it holds
/// the name of the environment variable that carries a key, never a key.
/// </summary>
/// <param name="Id">The listing id.</param>
/// <param name="DisplayName">The name shown on the card.</param>
/// <param name="Region">The region the listing is grouped under.</param>
/// <param name="Country">The ISO 3166-1 alpha-2 country, or null.</param>
/// <param name="ResidencyNote">What the vendor states about where data is processed, or null. It is not verified by Autonogrammer.</param>
/// <param name="Kind">What the listing sells or runs.</param>
/// <param name="OmpProviderId">The id omp knows the provider by, or null when Autonogrammer writes a <c>models.yml</c> block.</param>
/// <param name="ApiKeyEnv">The environment variable name that carries the key, or null for keyless and sign-in listings.</param>
/// <param name="LoginCommand">The omp sign-in command of a listing whose credential Autonogrammer never stores, or null.</param>
/// <param name="Routes">The ways to reach the provider; the first is the default. Empty for a sign-in listing.</param>
/// <param name="Variables">The base URL placeholders the user fills in.</param>
/// <param name="DocsUrl">The vendor documentation, or null.</param>
/// <param name="KeyPageUrl">The page where a key is created, or null.</param>
/// <param name="SourceUrl">The page the listing was read on.</param>
/// <param name="Status">How far the research verified the listing.</param>
/// <param name="StatusDate">The date the status was established.</param>
/// <param name="Note">A short caveat, or null.</param>
/// <param name="Plan">The plan terms of a coding plan listing, or null.</param>
public sealed record ProviderListing(
    string Id,
    string DisplayName,
    ProviderListingRegion Region,
    string? Country,
    string? ResidencyNote,
    ProviderListingKind Kind,
    string? OmpProviderId,
    string? ApiKeyEnv,
    string? LoginCommand,
    IReadOnlyList<ProviderListingRoute> Routes,
    IReadOnlyList<ProviderListingVariable> Variables,
    Uri? DocsUrl,
    Uri? KeyPageUrl,
    Uri SourceUrl,
    ProviderListingStatus Status,
    DateOnly StatusDate,
    string? Note,
    ProviderListingPlan? Plan);
