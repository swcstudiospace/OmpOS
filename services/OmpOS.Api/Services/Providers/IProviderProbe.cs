namespace OmpOS.Api.Services.Providers;

/// <summary>Asks an address whether something answers. A probe sends no key and no body.</summary>
internal interface IProviderProbe
{
    /// <summary>Calls the address once.</summary>
    /// <param name="url">The address of a provider.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>What was found. A refused address is a blocked result, not an exception.</returns>
    Task<ProviderProbeResult> ProbeAsync(Uri url, CancellationToken cancellationToken);
}
