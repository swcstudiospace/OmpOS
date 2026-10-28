namespace OmpOS.Web.Services.Settings;

/// <summary>The sentences of the web Models source. None quotes a key or a reply of a provider.</summary>
internal static class ModelsApiCopy
{
    /// <summary>The operator did not enable key writes. It is also the reason shown beside the disabled key field.</summary>
    public const string CredentialWritesDisabled = "Credential writes are disabled by the operator of this service. Provider keys are set in the service's environment.";

    /// <summary>The sentence the key field shows in the Demo, where no key can be entered because no service holds one.</summary>
    public const string CredentialWritesDisabledInDemo = "Key entry is disabled by the operator in this Demo. A real service takes provider keys in its own environment, and nothing typed here would be stored.";

    /// <summary>The operator did not enable configuration writes.</summary>
    public const string ConfigWritesDisabled = "Changing providers and the default model is disabled by the operator of this service.";

    /// <summary>The service could not be reached.</summary>
    public const string Unreachable = "The service could not be reached. Check your connection and try again.";

    /// <summary>The caller is not signed in.</summary>
    public const string SignInRequired = "Sign in again, then try again.";

    /// <summary>The service answered 429.</summary>
    public const string TooManyRequests = "Too many requests. Wait a minute, then try again.";

    /// <summary>The provider is not in the list the page read last.</summary>
    public const string UnknownProvider = "This provider is not in the list the page loaded. Reload the providers, then try again.";

    /// <summary>A catalogue provider that omp does not know cannot be set up from the web.</summary>
    public const string CatalogueNotSupported = "The operator of this service sets up this provider in the service's environment. To use your own endpoint, add it as a custom provider.";

    /// <summary>omp is not installed on the service, so the page cannot tell which providers have a key.</summary>
    public const string OmpNotInstalled = "omp is not installed on this service, so it cannot tell which providers have a key.";

    /// <summary>The answer was not what the page expects.</summary>
    public const string UnexpectedAnswer = "The service answered in a way this page does not understand.";
}
