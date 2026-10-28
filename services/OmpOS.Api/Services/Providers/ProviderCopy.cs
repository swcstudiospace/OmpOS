namespace OmpOS.Api.Services.Providers;

/// <summary>The sentences of the provider endpoints. None quotes a key, a path or a reply of a provider.</summary>
internal static class ProviderCopy
{
    /// <summary>omp is not installed on the service.</summary>
    public const string OmpNotInstalled = "omp is not installed on this service, so the change was not made.";

    /// <summary>omp could not be asked to check a change.</summary>
    public const string OmpUnavailable = "The service could not ask omp to check the change, so nothing was changed.";

    /// <summary>omp refused a change.</summary>
    public const string OmpRejected = "omp refused the change.";

    /// <summary>A block exists that OmpOS does not own.</summary>
    public const string NotOwned = "A provider with this id already exists in omp's configuration and OmpOS does not own it. Remove it there first.";

    /// <summary>Credential writes are off.</summary>
    public const string CredentialWritesDisabled = "Credential writes are disabled by the operator. Provider keys are set in the service's environment.";

    /// <summary>Configuration writes are off.</summary>
    public const string ConfigWritesDisabled = "Changes to omp's provider configuration are disabled by the operator.";

    /// <summary>Live tests are off.</summary>
    public const string LiveTestsDisabled = "Live tests are disabled by the operator. No call was made.";

    /// <summary>Returns the sentence that names the variable an operator sets for a provider whose key is not written through the API.</summary>
    /// <param name="envName">The variable name, or null when the provider has no key variable.</param>
    /// <returns>The sentence.</returns>
    public static string OperatorSetsKey(string? envName) =>
        envName is null
            ? "This provider's key is set by the operator, not through the API."
            : $"This provider's key is set by the operator in the environment variable {envName}, not through the API.";
}
