namespace Autonogrammer.Harness.Providers;

/// <summary>
/// One provider, aggregator, coding plan or local runtime of the catalogue: where it is, who runs it, how it is reached and
/// authenticated, what the vendor states about residency and plan terms, and the page the row was read on. A row holds no key: it
/// names the environment variable that carries one. Rows live in the embedded <c>Data/catalogue-*.json</c> files and are read once
/// by <see cref="CatalogueRows"/>.
/// </summary>
/// <param name="Id">The row slug, unique in the catalogue.</param>
/// <param name="DisplayName">The name shown on the card.</param>
/// <param name="Region">The group of the world the row belongs to.</param>
/// <param name="Country">The ISO 3166-1 alpha-2 country of the vendor or host, or null when none applies.</param>
/// <param name="ResidencyNote">What the vendor states about where data is processed, or null. Autonogrammer does not verify it.</param>
/// <param name="Kind">What the row sells or runs.</param>
/// <param name="OmpProviderId">The id omp knows the provider by, or null when omp has no native provider for this host and key kind. A null id means a <c>models.yml</c> block is written.</param>
/// <param name="ApiKeyEnv">The name of the environment variable that carries the key, or null for keyless and sign-in rows.</param>
/// <param name="Routes">The ways to reach the provider; the first is the default. Empty only for a sign-in row.</param>
/// <param name="UrlVariables">The placeholders the routes' base URLs hold.</param>
/// <param name="DocsUrl">The vendor documentation, or null.</param>
/// <param name="KeyPageUrl">The page where a key is created, or null when the research found no stable URL.</param>
/// <param name="SourceUrl">The page the row was read on.</param>
/// <param name="Status">How far the research verified the row.</param>
/// <param name="StatusDate">The date the status was established.</param>
/// <param name="Note">A short caveat for the card, or null. An unverified row always has one that starts with <c>Verify before use:</c>.</param>
/// <param name="LoginCommand">The omp command that signs in for a row whose credential is an omp sign-in that Autonogrammer never stores, or null.</param>
/// <param name="Plan">The terms of a coding plan row, or null for every other kind.</param>
public sealed record CatalogueRow(
    string Id,
    string DisplayName,
    CatalogueRegion Region,
    string? Country,
    string? ResidencyNote,
    CatalogueKind Kind,
    string? OmpProviderId,
    string? ApiKeyEnv,
    IReadOnlyList<CatalogueRoute> Routes,
    IReadOnlyList<CatalogueUrlVariable> UrlVariables,
    Uri? DocsUrl,
    Uri? KeyPageUrl,
    Uri SourceUrl,
    CatalogueStatus Status,
    DateOnly StatusDate,
    string? Note = null,
    string? LoginCommand = null,
    CataloguePlanTerms? Plan = null)
{
    /// <summary>Gets the default route, or null for a sign-in row.</summary>
    public CatalogueRoute? DefaultRoute => Routes.Count > 0 ? Routes[0] : null;

    /// <summary>Gets a value indicating whether the card must show the "verify before use" mark: the research found no usable vendor page.</summary>
    public bool VerifyBeforeUse => Status == CatalogueStatus.Unverified;

    /// <summary>Gets a value indicating whether the row takes an API key. Sign-in rows and keyless local rows do not.</summary>
    public bool TakesApiKey => ApiKeyEnv is not null && Routes.Count > 0 && Routes[0].Auth != CatalogueAuthStyle.None;

    /// <summary>Gets a value indicating whether omp already knows the provider, so a key goes to omp and no <c>models.yml</c> block is needed.</summary>
    public bool IsOmpNative => OmpProviderId is not null;

    /// <summary>
    /// Returns the provider id a <c>models.yml</c> block for <paramref name="route"/> uses: the row id for the default route and
    /// <c>row-route</c> for every other route, so two routes of one row never share a block.
    /// </summary>
    /// <param name="route">A route of this row.</param>
    /// <returns>The provider id.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="route"/> is null.</exception>
    public string BlockProviderId(CatalogueRoute route)
    {
        ArgumentNullException.ThrowIfNull(route);
        return Routes.Count > 0 && string.Equals(Routes[0].Id, route.Id, StringComparison.Ordinal) ? Id : $"{Id}-{route.Id}";
    }
}
