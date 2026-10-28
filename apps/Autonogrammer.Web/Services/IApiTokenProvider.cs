namespace Autonogrammer.Web.Services;

/// <summary>
/// Supplies the bearer token the API client sends. The default provider supplies none: the API then treats the caller as anonymous (a local
/// development API maps that to its development principal; a hosted API answers 401). Sign-in (OD-03) replaces this registration. A provider
/// must never log, display or persist the token it returns.
/// </summary>
internal interface IApiTokenProvider
{
    /// <summary>Gets the token for the next request.</summary>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The access token, or null when the caller has none.</returns>
    ValueTask<string?> GetTokenAsync(CancellationToken cancellationToken);
}
