namespace OmpOS.Contracts.Providers;

/// <summary>The answer of a credential write. It repeats no part of the key.</summary>
public sealed record CredentialResponse
{
    /// <summary>Gets the provider the key was written for.</summary>
    public required string ProviderId { get; init; }

    /// <summary>Gets whether omp now holds a credential for the provider.</summary>
    public required ProviderConfiguredState Configured { get; init; }
}
