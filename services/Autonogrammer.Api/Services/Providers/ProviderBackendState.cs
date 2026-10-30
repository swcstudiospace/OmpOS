namespace Autonogrammer.Api.Services.Providers;

/// <summary>What the service can see of omp: whether it is installed, which providers hold a credential and which Autonogrammer blocks exist.</summary>
/// <param name="OmpInstalled">True when the omp executable was found.</param>
/// <param name="CredentialedProviderIds">The omp provider ids for which omp lists models, which it does only for a provider it holds a credential for.</param>
/// <param name="Blocks">The Autonogrammer blocks of <c>models.yml</c> by provider id, with the name of the key variable each uses or null for a keyless block.</param>
/// <param name="DefaultModel">The default model as <c>provider/model</c>, or null.</param>
/// <param name="Complete">False when omp could not list its models, so a missing id proves nothing.</param>
internal sealed record ProviderBackendState(
    bool OmpInstalled,
    IReadOnlySet<string> CredentialedProviderIds,
    IReadOnlyDictionary<string, string?> Blocks,
    string? DefaultModel,
    bool Complete)
{
    /// <summary>Gets the state of a service that has no omp.</summary>
    public static ProviderBackendState NoOmp { get; } = new(
        OmpInstalled: false,
        new HashSet<string>(StringComparer.Ordinal),
        new Dictionary<string, string?>(StringComparer.Ordinal),
        DefaultModel: null,
        Complete: false);
}
