using System.Text.Json.Serialization;

namespace OmpOS.Contracts.Providers;

/// <summary>Whether the service already has what it needs to call a provider. It says a key is present, never that it works.</summary>
public enum ProviderConfiguredState
{
    /// <summary>omp holds a credential for the provider, or the environment variable of a configured block is set.</summary>
    [JsonStringEnumMemberName("configured")]
    Configured,

    /// <summary>The provider needs a key or a sign-in that the service does not have.</summary>
    [JsonStringEnumMemberName("not-configured")]
    NotConfigured,

    /// <summary>The provider needs no key.</summary>
    [JsonStringEnumMemberName("not-required")]
    NotRequired,

    /// <summary>The service could not find out, for example because omp is not installed on it.</summary>
    [JsonStringEnumMemberName("unknown")]
    Unknown,
}
